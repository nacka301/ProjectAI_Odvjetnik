/**
 * The module registry — one list that decides which features a non-admin user
 * can see and reach.
 *
 * Before this file, the same route list was written out four times (the feature
 * cards on the dashboard, the tour steps beside them, the header nav, the
 * command palette) and each copy could drift from the others. Marking a module
 * "in development" now means editing one `status` field here.
 *
 * Two things this file does NOT do, on purpose:
 *
 *  - It carries no JSX. `frontend/middleware.ts` runs on the Edge runtime and
 *    imports `findLockedModuleForPath` from here; anything React-shaped would
 *    follow it into that bundle.
 *  - It is not the security boundary. Everything here is a hint the browser can
 *    lie about. A locked module is only actually locked because
 *    `Backend/Middleware/AdminGuardMiddleware.cs` refuses its API prefixes for
 *    anyone who is not the ADMIN_EMAIL account, and the backend decides that
 *    from a verified JWT, never from this list or from the `is-admin` cookie.
 *
 * `apiPrefixes` is mirrored in `Backend/Middleware/ModuleRegistry.cs`, because
 * C# cannot import TypeScript. `Backend.Tests/AdminGuardMiddlewareTests.cs`
 * parses this file and fails if the two ever disagree.
 */

export type ModuleStatus = "spreman" | "u-razvoju";

export interface ModuleDefinition {
  /** Slug. The dashboard card's DOM id is `card-${id}`, which the tour targets. */
  id: string;
  /** Shown to the user in the "still in development" notice. */
  title: string;
  /** Every frontend route belonging to this module. */
  routes: string[];
  /** Backend route prefixes. Mirrored in Backend/Middleware/ModuleRegistry.cs. */
  apiPrefixes: string[];
  status: ModuleStatus;
}

export const MODULES: ModuleDefinition[] = [
  {
    id: "troskovnik",
    title: "Odvjetnički Troškovnik",
    routes: ["/troskovnik"],
    apiPrefixes: ["/api/troskovnik"],
    status: "spreman",
  },
  {
    id: "stranke",
    title: "Stranke",
    routes: ["/klijenti", "/predmeti"],
    apiPrefixes: ["/api/client", "/api/case", "/api/case-event"],
    status: "spreman",
  },
  {
    id: "procjena",
    title: "Tržišna Procjena",
    // /valuation is a second, unlinked entry point into the same module. It is
    // reachable only by typing the URL, and it posts to /api/evaluate directly.
    routes: ["/wizard", "/valuation"],
    // /wizard calls /api/valuation/draft before it ever reaches /api/evaluate,
    // so locking only /api/evaluate would leave half the flow alive.
    apiPrefixes: ["/api/evaluate", "/api/valuation"],
    status: "u-razvoju",
  },
  {
    id: "ugovor",
    title: "Analiza Ugovora",
    routes: ["/ugovor"],
    apiPrefixes: ["/api/contract"],
    status: "u-razvoju",
  },
  {
    id: "zk",
    title: "ZK Izvadak",
    routes: ["/zk-procjena"],
    apiPrefixes: ["/api/zk"],
    status: "u-razvoju",
  },
  {
    id: "prijevod",
    title: "Sudski Tumač",
    routes: ["/prijevod"],
    apiPrefixes: ["/api/translation"],
    status: "u-razvoju",
  },
];

/**
 * True when `pathname` is `prefix`, or a path nested under it.
 *
 * A bare `startsWith` would make `/api/zk` swallow a future `/api/zk-nesto`,
 * and `/wizard` swallow `/wizardx`. Locking a route you did not mean to lock
 * fails quietly — the page just disappears — so the match is deliberate.
 */
export function matchesRoute(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

/** The module owning this route, whatever its status, or null if none does. */
export function findModuleForPath(pathname: string): ModuleDefinition | null {
  return (
    MODULES.find((m) => m.routes.some((r) => matchesRoute(pathname, r))) ?? null
  );
}

/**
 * The in-development module owning this route, or null.
 *
 * Returns the module rather than a boolean so the caller can name it: the
 * middleware passes the id along to the dashboard, which tells the user which
 * module it was instead of dropping them on "/" with no explanation.
 */
export function findLockedModuleForPath(
  pathname: string,
): ModuleDefinition | null {
  const found = findModuleForPath(pathname);
  return found?.status === "u-razvoju" ? found : null;
}

/** Whether a route should be hidden or blocked for this viewer. */
export function isPathLocked(pathname: string, isAdmin: boolean): boolean {
  if (isAdmin) return false;
  return findLockedModuleForPath(pathname) !== null;
}

/** Whether a dashboard card / tour step should be locked for this viewer. */
export function isModuleLocked(moduleId: string, isAdmin: boolean): boolean {
  if (isAdmin) return false;
  return MODULES.find((m) => m.id === moduleId)?.status === "u-razvoju";
}

/** Title for the "still in development" notice, or null for an unknown id. */
export function moduleTitle(moduleId: string): string | null {
  return MODULES.find((m) => m.id === moduleId)?.title ?? null;
}
