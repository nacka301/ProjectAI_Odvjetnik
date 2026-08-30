/**
 * SessionGuard — idle odjava.
 *
 * Bitno je jedno: dok korisnik nešto radi, sesija se NE gasi. Ranija verzija je
 * preko zastarjelih closure vrijednosti znala prestati resetirati tajmer, pa je
 * odvjetnik usred rada završio na prijavi. Testovi zato mjere ponašanje kroz
 * dulji niz aktivnosti, ne samo jedan reset.
 */
import { render, screen, cleanup, act } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/** Pomakni lažne tajmere unutar act() da React state-update ne baca upozorenja. */
const advance = (ms: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });

const push = vi.fn();
const refresh = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, refresh }),
}));

const signOutCompletely = vi.fn().mockResolvedValue(undefined);
vi.mock("@/lib/sign-out", () => ({
  signOutCompletely: () => signOutCompletely(),
}));

const getSession = vi.fn().mockResolvedValue({ data: { session: null } });
const refreshSession = vi.fn().mockResolvedValue({ data: { session: null } });
const unsubscribe = vi.fn();
const onAuthStateChange = vi.fn(() => ({ data: { subscription: { unsubscribe } } }));
vi.mock("@/lib/supabase/client", () => ({
  createSupabaseBrowser: () => ({
    auth: { getSession, refreshSession, onAuthStateChange },
  }),
}));

import SessionGuard from "@/components/SessionGuard";
import {
  ABSOLUTE_SESSION_MS,
  IDLE_TIMEOUT_MS,
  WARNING_COUNTDOWN_SEC,
} from "@/lib/session-timeout";

const WARNING = /Uskoro ćete biti odjavljeni zbog neaktivnosti/;
const realFetch = globalThis.fetch;

function activity() {
  window.dispatchEvent(new KeyboardEvent("keydown", { key: "a" }));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-08-30T10:00:00.000Z"));
  push.mockClear();
  refresh.mockClear();
  signOutCompletely.mockClear();
  getSession.mockClear();
  refreshSession.mockClear();
  onAuthStateChange.mockClear();
  unsubscribe.mockClear();
  localStorage.clear();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  globalThis.fetch = realFetch;
});

describe("SessionGuard", () => {
  it("ne prikazuje upozorenje dok je korisnik aktivan kroz vrijeme dulje od idle prozora", async () => {
    render(<SessionGuard>app</SessionGuard>);

    // Četiri 20-minutna razmaka (ukupno 80 min > 60 min), s aktivnošću između.
    for (let i = 0; i < 4; i++) {
      await advance(20 * 60 * 1000);
      activity();
    }

    expect(screen.queryByText(WARNING)).not.toBeInTheDocument();
    expect(signOutCompletely).not.toHaveBeenCalled();
  });

  it("prikaže upozorenje nakon idle prozora bez aktivnosti", async () => {
    render(<SessionGuard>app</SessionGuard>);

    await advance(IDLE_TIMEOUT_MS + 1000);

    expect(screen.getByText(WARNING)).toBeInTheDocument();
    expect(signOutCompletely).not.toHaveBeenCalled();
  });

  it("gumb Ostani prijavljen zatvori upozorenje i ponovno naoruža tajmer", async () => {
    render(<SessionGuard>app</SessionGuard>);

    await advance(IDLE_TIMEOUT_MS + 1000);
    await act(async () => {
      screen.getByRole("button", { name: "Ostani prijavljen" }).click();
    });

    expect(screen.queryByText(WARNING)).not.toBeInTheDocument();

    // Prošlo pola prozora — još uvijek prijavljen.
    await advance(IDLE_TIMEOUT_MS / 2);
    expect(screen.queryByText(WARNING)).not.toBeInTheDocument();
    expect(signOutCompletely).not.toHaveBeenCalled();
  });

  it("odbrojavanje do nule odjavljuje i vodi na /auth/login?reason=idle", async () => {
    render(<SessionGuard>app</SessionGuard>);

    await advance(IDLE_TIMEOUT_MS + 1000);
    expect(screen.getByText(WARNING)).toBeInTheDocument();

    await advance((WARNING_COUNTDOWN_SEC + 1) * 1000);

    expect(signOutCompletely).toHaveBeenCalled();
    expect(push).toHaveBeenCalledWith("/auth/login?reason=idle");
  });

  it("uspješan mrežni poziv resetira idle tajmer", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(new Response("ok", { status: 200 }));
    render(<SessionGuard>app</SessionGuard>);

    // Tri poziva u razmacima od 40 min — bez njih bi upozorenje palo na 60 min.
    for (let i = 0; i < 3; i++) {
      await advance(40 * 60 * 1000);
      await window.fetch("https://api.example.test/api/troskovnik/izracunaj");
      await advance(0);
    }

    expect(screen.queryByText(WARNING)).not.toBeInTheDocument();
    expect(signOutCompletely).not.toHaveBeenCalled();
  });

  it("apsolutni maksimum sesije odjavljuje bez obzira na aktivnost", async () => {
    localStorage.setItem(
      "pravotek_session_start",
      String(Date.now() - (ABSOLUTE_SESSION_MS + 60_000)),
    );

    render(<SessionGuard>app</SessionGuard>);
    await advance(0);

    expect(signOutCompletely).toHaveBeenCalled();
    expect(push).toHaveBeenCalledWith("/auth/login?reason=idle");
  });
});
