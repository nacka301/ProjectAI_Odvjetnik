import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";
import {
  MFA_COOKIE,
  MFA_TTL_SECONDS,
  signMfaCookie,
} from "@/lib/mfa-cookie";

/**
 * Validates a TOTP (or recovery) code and, only on success, issues the signed
 * marker cookie the middleware looks for.
 *
 * This is the single place that can mint that cookie. The browser never sees the
 * signing secret, so a client cannot grant itself the marker — the whole point
 * of moving the gate off the login screen.
 */
export async function POST(request: Request) {
  const supabase = await createSupabaseServer();

  // getUser(), not getSession(): the identity must be revalidated server-side
  // before we hand out anything based on it.
  const {
    data: { user },
  } = await supabase.auth.getUser();

  if (!user) {
    return NextResponse.json({ error: "Niste prijavljeni." }, { status: 401 });
  }

  const {
    data: { session },
  } = await supabase.auth.getSession();

  if (!session) {
    return NextResponse.json({ error: "Sesija je istekla." }, { status: 401 });
  }

  let token: string;
  try {
    const body = await request.json();
    token = String(body?.token ?? "").trim();
  } catch {
    return NextResponse.json({ error: "Neispravan zahtjev." }, { status: 400 });
  }

  if (!token) {
    return NextResponse.json({ error: "Unesite kod." }, { status: 400 });
  }

  const apiBase = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

  let backendResponse: Response;
  try {
    backendResponse = await fetch(`${apiBase}/api/2fa/validate`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${session.access_token}`,
      },
      body: JSON.stringify({ token }),
      cache: "no-store",
    });
  } catch {
    return NextResponse.json(
      { error: "Poslužitelj nije dostupan. Pokušajte ponovo." },
      { status: 503 },
    );
  }

  const result = await backendResponse.json().catch(() => null);

  if (!backendResponse.ok || result?.success !== true) {
    // One message for a wrong code and for a spent recovery code — knowing which
    // one failed tells an attacker whether the code shape was right.
    return NextResponse.json({ error: "Neispravan kod." }, { status: 401 });
  }

  const response = NextResponse.json({ success: true });
  response.cookies.set(MFA_COOKIE, await signMfaCookie(user.id), {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: MFA_TTL_SECONDS,
  });

  return response;
}
