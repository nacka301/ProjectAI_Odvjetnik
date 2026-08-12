import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";
import { API_BASE } from "@/lib/api";

/**
 * Starts enrolment: asks the backend for a fresh TOTP secret and its QR payload.
 *
 * Nothing is persisted by this call and no cookie is minted — the secret only
 * takes effect once /api/2fa/enroll confirms a code generated from it. Proxied
 * through the server so the gate screen, which runs before the user is allowed
 * anywhere, never has to hold an access token in the browser.
 */
export async function POST() {
  const supabase = await createSupabaseServer();

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

  let backendResponse: Response;
  try {
    backendResponse = await fetch(`${API_BASE}/api/2fa/setup`, {
      method: "POST",
      headers: { Authorization: `Bearer ${session.access_token}` },
      cache: "no-store",
    });
  } catch {
    return NextResponse.json(
      { error: "Poslužitelj nije dostupan. Pokušajte ponovo." },
      { status: 503 },
    );
  }

  const result = await backendResponse.json().catch(() => null);

  if (!backendResponse.ok || !result?.otpauth_url) {
    return NextResponse.json(
      { error: "Priprema dvofaktorske provjere nije uspjela." },
      { status: 502 },
    );
  }

  return NextResponse.json(result, { headers: { "Cache-Control": "no-store" } });
}
