import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";
import { checkRateLimit, isWaitlistEmailValid, type RateLimitRecord } from "@/lib/waitlist";

/**
 * Best-effort per-IP rate limit. This Map lives in module scope (in
 * memory), so it only holds across requests served by the same warm
 * serverless instance — good enough to blunt a naive bot hitting a
 * pre-launch waitlist, not a hard guarantee across a multi-instance
 * deployment. If that turns out not to be enough, the next step is a
 * counter backed by the database itself rather than the process.
 */
const OKNO_MS = 10 * 60 * 1000; // 10 min
const MAX_PO_IP = 5;
const pokusaji = new Map<string, RateLimitRecord>();

function ipAdresa(request: Request): string {
  const naprijed = request.headers.get("x-forwarded-for");
  return naprijed?.split(",")[0]?.trim() || "nepoznato";
}

export async function POST(request: Request) {
  const ip = ipAdresa(request);
  if (checkRateLimit(pokusaji, ip, Date.now(), { windowMs: OKNO_MS, max: MAX_PO_IP })) {
    return NextResponse.json({ error: "Previše pokušaja. Pokušajte kasnije." }, { status: 429 });
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "Neispravan zahtjev." }, { status: 400 });
  }

  const { email, web } = (body ?? {}) as { email?: unknown; web?: unknown };

  // Honeypot — polje koje ljudi ne vide i ne popunjavaju. Botu koji ga
  // popuni tiho javljamo "uspjeh" bez ikakvog upisa, da ne uči da je otkriven.
  if (typeof web === "string" && web.trim() !== "") {
    return NextResponse.json({ success: true });
  }

  const vrijednost = typeof email === "string" ? email.trim() : "";
  if (!isWaitlistEmailValid(vrijednost)) {
    return NextResponse.json({ error: "Provjerite adresu e-pošte." }, { status: 400 });
  }

  const supabase = await createSupabaseServer();
  const { error } = await supabase.from("waitlist").insert({
    email: vrijednost,
    izvor: "landing", // fiksno server-side — klijentov izvor/vrijeme se ne prihvaća
  });

  if (error) {
    // 23505 = unique_violation (Postgres) — e-mail je već na listi.
    if (error.code === "23505") {
      return NextResponse.json({ success: true }, { status: 409 });
    }
    console.error("[waitlist] upis nije uspio", error.code, error.message);
    return NextResponse.json({ error: "Prijava nije prošla." }, { status: 500 });
  }

  return NextResponse.json({ success: true });
}
