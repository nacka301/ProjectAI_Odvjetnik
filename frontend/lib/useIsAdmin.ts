"use client";

import { useSyncExternalStore } from "react";

/**
 * Whether the current viewer is the admin, for UI purposes only.
 *
 * The signal is the `is-admin=1` cookie, which `frontend/middleware.ts` writes
 * after comparing the verified session email against ADMIN_EMAIL. The cookie is
 * deliberately not HttpOnly so the browser can read it — which also means
 * anyone can type it into their own devtools. Use it to decide what to *show*.
 * Never use it to decide what someone may *do*: routes are gated in the
 * middleware and endpoints in `Backend/Middleware/AdminGuardMiddleware.cs`, both
 * from a verified token.
 *
 * The cookie is an external store, not React state, so it is read through
 * useSyncExternalStore rather than inline during render. Reading it inline is
 * what the header used to do, and it makes the server's markup disagree with the
 * client's — React then throws the server render away. Hydration uses the server
 * snapshot below, which is `false`: if anything goes wrong, a module stays
 * blurred rather than briefly looking open.
 *
 * There is no subscription. Nothing notifies a page when a cookie changes, and
 * middleware only rewrites this one on a navigation — which remounts the tree
 * anyway.
 */
const subscribe = () => () => {};

const readCookie = () =>
  document.cookie.split(";").some((c) => c.trim() === "is-admin=1");

const notAdminOnServer = () => false;

export function useIsAdmin(): boolean {
  return useSyncExternalStore(subscribe, readCookie, notAdminOnServer);
}
