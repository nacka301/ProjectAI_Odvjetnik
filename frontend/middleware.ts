import { NextResponse, type NextRequest } from "next/server";
import { createSupabaseMiddleware } from "@/lib/supabase/middleware";

/**
 * Next.js Middleware — runs on every matched request.
 *
 * Security responsibilities:
 * 1. Refresh auth tokens (HttpOnly cookies) on every navigation
 * 2. Redirect unauthenticated users away from protected routes
 * 3. Redirect authenticated users away from auth pages (login/register)
 */
export async function middleware(request: NextRequest) {
  const { user, supabaseResponse } = await createSupabaseMiddleware(request);

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
  if (user && isAuthRoute && !pathname.startsWith("/auth/callback") && !pathname.startsWith("/auth/reset-password")) {
    const homeUrl = request.nextUrl.clone();
    homeUrl.pathname = "/";
    return NextResponse.redirect(homeUrl);
  }

  // ── Inject cache-busting headers on every HTML response ──
  // Prevents browsers from serving stale pages after a deploy.
  // _next/static/* is content-hashed and stays long-cached by default.
  const response = supabaseResponse;
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
