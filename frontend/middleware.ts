import { NextResponse, type NextRequest } from "next/server";
import { createSupabaseMiddleware } from "@/lib/supabase/middleware";
import { checkSecondFactor } from "@/lib/second-factor";
import {
  MFA_COOKIE,
  mfaCookieOptions,
  signMfaCookie,
  verifyMfaCookie,
} from "@/lib/mfa-cookie";

/**
 * Next.js Middleware — runs on every matched request.
 *
 * Security responsibilities:
 * 1. Refresh auth tokens (HttpOnly cookies) on every navigation
 * 2. Redirect unauthenticated users away from protected routes
 * 3. Enforce the second factor before any protected page renders
 * 4. Redirect authenticated users away from auth pages (login/register)
 */

/** Auth pages an already-signed-in user is still allowed to reach. */
const SIGNED_IN_AUTH_ROUTES = [
  "/auth/callback",
  "/auth/reset-password",
  "/auth/2fa",
  // Without this the gate would redirect here and the signed-in rule below would
  // bounce the user straight back to "/", looping forever.
  "/auth/nedostupno",
];

export async function middleware(request: NextRequest) {
  const { user, accessToken, supabaseResponse } = await createSupabaseMiddleware(request);

  const { pathname } = request.nextUrl;

  // ── Protected routes ──
  // Everything EXCEPT: /auth/*, /api/*, static files, _next
  const isAuthRoute =
    pathname.startsWith("/auth/");

  const isPublicRoute =
    isAuthRoute ||
    pathname.startsWith("/api/") ||
    pathname.startsWith("/_next/") ||
    pathname === "/favicon.ico";

  // If not authenticated and trying to access a protected page → redirect to login
  if (!user && !isPublicRoute) {
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/auth/login";
    loginUrl.searchParams.set("next", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // If authenticated and on an auth page (login/register) → redirect to home
  if (user && isAuthRoute && !SIGNED_IN_AUTH_ROUTES.some((p) => pathname.startsWith(p))) {
    const homeUrl = request.nextUrl.clone();
    homeUrl.pathname = "/";
    return NextResponse.redirect(homeUrl);
  }

  // ── Second factor ──
  // This lives here, not on the login screen, because the session cookie exists
  // the moment the password is accepted. A check inside the login component is
  // skipped by simply navigating elsewhere — and it never ran at all for Google
  // sign-in, which lands on /auth/callback.
  if (user && !isPublicRoute) {
    const alreadyVerified = await verifyMfaCookie(
      request.cookies.get(MFA_COOKIE)?.value,
      user.id,
    );

    if (!alreadyVerified) {
      const check = await checkSecondFactor(accessToken);

      if (check.outcome === "required") {
        const url = request.nextUrl.clone();
        url.pathname = "/auth/2fa";
        url.search = "";
        url.searchParams.set("next", pathname);
        return NextResponse.redirect(url);
      }

      if (check.outcome === "unknown") {
        // Still fail-closed: no cookie is minted and the user does not get in.
        // But we do not demand a code either — we could not establish that one
        // exists, and asking for one the account may never have had is what
        // locked people out with no way forward.
        const url = request.nextUrl.clone();
        url.pathname = "/auth/nedostupno";
        url.search = "";
        url.searchParams.set("next", pathname);
        url.searchParams.set("razlog", check.reason);
        return NextResponse.redirect(url);
      }

      // 2FA is off for this account — remember it so the next navigation does
      // not ask the backend again.
      supabaseResponse.cookies.set(
        MFA_COOKIE,
        await signMfaCookie(user.id),
        mfaCookieOptions(),
      );
    }
  }

  // ── Admin detection ──
  const adminEmail = process.env.ADMIN_EMAIL;
  const isAdmin =
    !!user &&
    !!adminEmail &&
    user.email?.toLowerCase() === adminEmail.toLowerCase();

  // Only admin email may access /admin/*
  if (pathname.startsWith("/admin") && !isAdmin) {
    const denyUrl = request.nextUrl.clone();
    denyUrl.pathname = "/";
    denyUrl.searchParams.delete("next");
    return NextResponse.redirect(denyUrl);
  }

  // Set is-admin cookie for client-side UI (navbar link)
  const response = supabaseResponse;

  if (isAdmin) {
    response.cookies.set("is-admin", "1", { path: "/", httpOnly: false, sameSite: "lax", maxAge: 60 * 60 * 24 });
  } else {
    response.cookies.delete("is-admin");
  }

  // ── Inject cache-busting headers on every HTML response ──
  // Prevents browsers from serving stale pages after a deploy.
  // _next/static/* is content-hashed and stays long-cached by default.
  response.headers.set("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
  response.headers.set("Pragma", "no-cache");
  response.headers.set("Expires", "0");
  return response;
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - favicon.ico
     * - public assets (svg, png, jpg, etc.)
     */
    "/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)",
  ],
};
