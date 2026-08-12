import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";
import { checkSecondFactor } from "@/lib/second-factor";

/**
 * The same three-way answer the middleware gate acts on, exposed to the gate
 * screen so it can show the right thing.
 *
 * It re-asks the backend rather than trusting a query parameter: the page must
 * not be talked into offering enrolment by a crafted URL.
 */
export async function GET() {
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

  const result = await checkSecondFactor(session?.access_token ?? null);

  return NextResponse.json(result, {
    headers: { "Cache-Control": "no-store" },
  });
}
