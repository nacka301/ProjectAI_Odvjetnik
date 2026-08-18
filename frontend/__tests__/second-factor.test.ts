import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { checkSecondFactor, classifySecondFactor } from "@/lib/second-factor";

/**
 * This function decides who gets into the application, so every branch gets a
 * test. The case that mattered: a backend answer that is *not* "2FA is on" must
 * never be treated as if it were — that is what asked users with no authenticator
 * for a code, and left them nowhere to go.
 */
describe("classifySecondFactor", () => {
  describe("the backend gave a real answer", () => {
    it("requires a code when 2FA is enabled", () => {
      expect(classifySecondFactor(200, { enabled: true })).toEqual({ outcome: "required" });
    });

    it("lets the user through when 2FA is disabled", () => {
      expect(classifySecondFactor(200, { enabled: false })).toEqual({ outcome: "not-enrolled" });
    });

    it("accepts any 2xx, not only 200", () => {
      expect(classifySecondFactor(204, { enabled: false })).toEqual({ outcome: "not-enrolled" });
    });
  });

  describe("the backend could not be asked", () => {
    it("treats a rejected token as unknown, not as 'code required'", () => {
      // The lockout: Supabase moved to ES256, the backend still verified HS256,
      // so every token came back 401 and every user was sent to the code prompt.
      expect(classifySecondFactor(401, null)).toEqual({
        outcome: "unknown",
        reason: "rejected",
      });
    });

    it("treats a forbidden response as unknown", () => {
      expect(classifySecondFactor(403, null)).toEqual({
        outcome: "unknown",
        reason: "rejected",
      });
    });

    it.each([500, 502, 503, 429, 404])("treats %i as unknown", (status) => {
      expect(classifySecondFactor(status, null)).toEqual({
        outcome: "unknown",
        reason: "backend",
      });
    });

    it("treats the backend's own 'I cannot tell' (503) as unknown", () => {
      expect(classifySecondFactor(503, { error: "Provjera 2FA trenutno nije moguća." })).toEqual({
        outcome: "unknown",
        reason: "backend",
      });
    });
  });

  describe("the answer was unreadable", () => {
    it.each([
      ["null body", null],
      ["undefined body", undefined],
      ["empty object", {}],
      ["a string", "enabled"],
      ["enabled as a string", { enabled: "true" }],
      ["enabled as a number", { enabled: 1 }],
      ["enabled as null", { enabled: null }],
    ])("treats %s as unknown rather than guessing", (_label, body) => {
      expect(classifySecondFactor(200, body)).toEqual({
        outcome: "unknown",
        reason: "malformed",
      });
    });

    it("never reads a truthy non-boolean as 'enabled'", () => {
      // A sloppy `enabled == true` here would let "false" (a non-empty string)
      // demand a code, or a 1 slip through as a boolean.
      expect(classifySecondFactor(200, { enabled: "false" }).outcome).toBe("unknown");
    });
  });

  describe("fail-closed invariant", () => {
    it("only ever lets a user in on an explicit enabled:false", () => {
      const cases: Array<[number, unknown]> = [
        [401, { enabled: false }],
        [403, { enabled: false }],
        [500, { enabled: false }],
        [503, { enabled: false }],
        [200, {}],
        [200, null],
      ];

      for (const [status, body] of cases) {
        expect(classifySecondFactor(status, body).outcome).not.toBe("not-enrolled");
      }

      expect(classifySecondFactor(200, { enabled: false }).outcome).toBe("not-enrolled");
    });
  });
});

/**
 * The retry policy. What went wrong in production was not only the verdict but
 * how few chances the backend got to give a good one: a cold Render instance
 * needs longer than a single attempt, and a backend that answers "I cannot
 * check right now" was never asked twice.
 */
describe("checkSecondFactor", () => {
  const TOKEN = "a.token.value";

  let fetchMock: ReturnType<typeof vi.fn>;

  /** Queues one outcome per call: a status+body, or "throw" for a dead network. */
  function respondWith(...turns: Array<{ status: number; body: unknown } | "throw">) {
    let call = 0;
    fetchMock.mockImplementation(async () => {
      const turn = turns[Math.min(call++, turns.length - 1)];
      if (turn === "throw") throw new Error("network down");
      return {
        status: turn.status,
        json: async () => turn.body,
      } as Response;
    });
  }

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    // The gate logs every refusal; keep the test output readable.
    vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("asks nobody when there is no token", async () => {
    await expect(checkSecondFactor(null)).resolves.toEqual({
      outcome: "unknown",
      reason: "no-session",
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("takes a real verdict on the first answer", async () => {
    respondWith({ status: 200, body: { enabled: false } });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({ outcome: "not-enrolled" });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("asks again after a 503 and takes the second answer", async () => {
    // The case this exists for: the backend could not verify the signature yet
    // — its signing keys were not loaded — and said so. One retry is the whole
    // difference between a locked-out lawyer and a two-second delay.
    respondWith({ status: 503, body: { error: "nije moguce" } }, { status: 200, body: { enabled: false } });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({ outcome: "not-enrolled" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it.each([429, 502, 503, 504])("retries a %i", async (status) => {
    respondWith({ status, body: {} }, { status: 200, body: { enabled: true } });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({ outcome: "required" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("does not retry a 401 — the token answered for itself", async () => {
    // Now that the backend says 503 when it cannot verify, a 401 is a verdict on
    // the token. Asking again with the same token would only burn a second.
    respondWith({ status: 401, body: null });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({
      outcome: "unknown",
      reason: "rejected",
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("retries a network failure", async () => {
    respondWith("throw", { status: 200, body: { enabled: false } });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({ outcome: "not-enrolled" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("absorbs a cold start — two slow answers, then a real one", async () => {
    // The failure this exists for: Render's free tier stops the instance and
    // takes up to ~50 s to serve the first request. Two attempts covered 15 s,
    // so the first person through the door in the morning — a lawyer who had
    // just registered, most painfully — met the "provjera nije moguća" screen
    // for a machine that was only waking up.
    respondWith("throw", { status: 503, body: {} }, { status: 200, body: { enabled: false } });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({ outcome: "not-enrolled" });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("gives up after three attempts rather than blocking forever", async () => {
    // A middleware that waited out a real outage would be worse than the gate
    // screen, which keeps retrying on its own without holding the navigation.
    respondWith({ status: 503, body: {} });

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({
      outcome: "unknown",
      reason: "backend",
    });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("still refuses to let anyone in while waking up", async () => {
    // The extra patience must not become a way past the gate. Three transient
    // answers are still not an "enabled: false", so nobody gets through.
    respondWith({ status: 503, body: {} });

    const result = await checkSecondFactor(TOKEN);
    expect(result.outcome).not.toBe("not-enrolled");
  });

  it("reports unreachable only when no attempt landed", async () => {
    respondWith("throw", "throw");

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({
      outcome: "unknown",
      reason: "unreachable",
    });
  });

  it("keeps the first real answer when the retry gets nowhere", async () => {
    // A 503 followed by a dead network is still a backend that told us something.
    // Calling that "unreachable" would throw away the more accurate diagnosis.
    respondWith({ status: 503, body: { error: "nije moguce" } }, "throw");

    await expect(checkSecondFactor(TOKEN)).resolves.toEqual({
      outcome: "unknown",
      reason: "backend",
    });
  });

  it("logs every refusal, without the token", async () => {
    const logged = vi.spyOn(console, "error").mockImplementation(() => {});
    respondWith({ status: 401, body: null });

    await checkSecondFactor(TOKEN);

    expect(logged).toHaveBeenCalledOnce();
    const line = logged.mock.calls[0].join(" ");
    expect(line).toContain("rejected");
    expect(line).toContain("401");
    expect(line).not.toContain(TOKEN);
  });

  it("logs nothing when the user is simply let through", async () => {
    const logged = vi.spyOn(console, "error").mockImplementation(() => {});
    respondWith({ status: 200, body: { enabled: false } });

    await checkSecondFactor(TOKEN);

    expect(logged).not.toHaveBeenCalled();
  });
});
