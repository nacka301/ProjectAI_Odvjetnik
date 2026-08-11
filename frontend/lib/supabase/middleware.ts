import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Creates a Supabase client designed for use inside Next.js middleware.
 * It reads/writes auth tokens via the request/response cookie pair,
 * ensuring secure HttpOnly token rotation on every navigation.
 */
export async function createSupabaseMiddleware(request: NextRequest) {
  let supabaseResponse = NextResponse.next({ request });

  const supabase = createServerClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    {
      cookies: {
        getAll() {
          return request.cookies.getAll();
        },
        setAll(cookiesToSet) {
          // 1. Set on the request (so downstream server code sees them)
          cookiesToSet.forEach(({ name, value }) =>
            request.cookies.set(name, value),
          );
          // 2. Rebuild response with updated request
          supabaseResponse = NextResponse.next({ request });
          // 3. Set on the response (so the browser receives them)
          cookiesToSet.forEach(({ name, value, options }) =>
            supabaseResponse.cookies.set(name, value, options),
          );
        },
      },
    },
  );

  // IMPORTANT: getUser() first, always. It asks the Supabase Auth server to
  // revalidate the token; getSession() only reads the cookie, which can be forged.
  const {
    data: { user },
  } = await supabase.auth.getUser();

  // Only now, on an already-validated identity, read the raw access token —
  // the middleware forwards it to the backend to ask about this user's 2FA.
  let accessToken: string | null = null;
  if (user) {
    const {
      data: { session },
    } = await supabase.auth.getSession();
    accessToken = session?.access_token ?? null;
  }

  return { user, accessToken, supabaseResponse };
}
