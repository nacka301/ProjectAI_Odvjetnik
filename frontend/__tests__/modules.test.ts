import { describe, it, expect } from "vitest";
import {
  MODULES,
  findLockedModuleForPath,
  isPathLocked,
  isModuleLocked,
  moduleTitle,
} from "@/lib/modules";

// ═══════════════════════════════════════════
//  Module registry — which features are open
// ═══════════════════════════════════════════
//
// This list decides what an ordinary user sees and can navigate to. It is not
// the lock itself (that is AdminGuardMiddleware on the backend), but if it is
// wrong the whole product is wrong: either finished modules go dark, or
// unfinished ones look ready.

const lockedIds = () =>
  MODULES.filter((m) => m.status === "u-razvoju").map((m) => m.id).sort();

describe("which modules are open", () => {
  it("ships exactly Troškovnik and Stranke", () => {
    const ready = MODULES.filter((m) => m.status === "spreman").map((m) => m.id);
    expect(ready.sort()).toEqual(["stranke", "troskovnik"]);
  });

  it("holds back the four AI modules and Fiskalizacija", () => {
    expect(lockedIds()).toEqual([
      "fiskalizacija",
      "prijevod",
      "procjena",
      "ugovor",
      "zk",
    ]);
  });

  it("gives every module a distinct id", () => {
    expect(new Set(MODULES.map((m) => m.id)).size).toBe(MODULES.length);
  });

  it("keeps /valuation with /wizard — both open the same module", () => {
    const procjena = MODULES.find((m) => m.id === "procjena");
    expect(procjena?.routes).toContain("/wizard");
    expect(procjena?.routes).toContain("/valuation");
    // /wizard calls this before it ever reaches /api/evaluate.
    expect(procjena?.apiPrefixes).toContain("/api/valuation");
  });
});

describe("route matching", () => {
  it.each([
    "/wizard",
    "/valuation",
    "/ugovor",
    "/zk-procjena",
    "/prijevod",
    // Not an AI module, but unfinished all the same. It had no dashboard card
    // and no entry here, so it stayed reachable by URL long after the rest
    // were locked.
    "/fiskalizacija",
  ])("blocks %s for an ordinary user", (path) => {
    expect(isPathLocked(path, false)).toBe(true);
  });

  it.each(["/troskovnik", "/klijenti", "/predmeti"])(
    "leaves %s open to everyone",
    (path) => {
      expect(isPathLocked(path, false)).toBe(false);
    },
  );

  it.each(["/history", "/postavke", "/"])(
    "does not touch %s, which belongs to no module",
    (path) => {
      expect(isPathLocked(path, false)).toBe(false);
    },
  );

  it("blocks nested routes of a locked module", () => {
    expect(isPathLocked("/wizard/korak-2", false)).toBe(true);
    expect(isPathLocked("/klijenti/abc/predmeti", false)).toBe(false);
  });

  it("does not swallow a neighbouring route that merely shares a prefix", () => {
    // "/wizardx" is not "/wizard". Over-matching here takes a working page
    // offline, and the failure is silent — the route simply redirects.
    expect(isPathLocked("/wizardx", false)).toBe(false);
    expect(isPathLocked("/predmeti-arhiva", false)).toBe(false);
  });

  it("lets the admin everywhere", () => {
    for (const m of MODULES) {
      for (const route of m.routes) {
        expect(isPathLocked(route, true)).toBe(false);
      }
    }
  });
});

describe("naming the module that was blocked", () => {
  it("returns the module so the redirect can say which one it was", () => {
    expect(findLockedModuleForPath("/ugovor")?.title).toBe("Analiza Ugovora");
  });

  it("returns null for an open route", () => {
    expect(findLockedModuleForPath("/troskovnik")).toBeNull();
  });

  it("returns null for an id nobody recognises", () => {
    // The id arrives in a query string, so it is user input.
    expect(moduleTitle("../../etc/passwd")).toBeNull();
    expect(moduleTitle("")).toBeNull();
  });
});

describe("card and tour locking", () => {
  it("locks a card by module id for ordinary users only", () => {
    expect(isModuleLocked("procjena", false)).toBe(true);
    expect(isModuleLocked("procjena", true)).toBe(false);
    expect(isModuleLocked("troskovnik", false)).toBe(false);
  });

  it("treats an unknown module id as open rather than crashing", () => {
    expect(isModuleLocked("nepostojeci", false)).toBe(false);
  });
});
