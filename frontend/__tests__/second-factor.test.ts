import { describe, it, expect } from "vitest";
import { classifySecondFactor } from "@/lib/second-factor";

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
