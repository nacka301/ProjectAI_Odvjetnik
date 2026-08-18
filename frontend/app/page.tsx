import { createSupabaseServer } from "@/lib/supabase/server";
import LandingPage from "@/components/LandingPage";
import Dashboard from "@/components/Dashboard";

/**
 * `/` serves two different pages depending on who is asking.
 *
 * It used to serve only the dashboard, with the middleware bouncing anyone
 * without a session to the login form. That meant the product had no public
 * face at all: every link to pravotek.com landed on a password prompt.
 *
 * The decision is made on the server, from the session cookie, so a signed-out
 * visitor never downloads the dashboard and a signed-in one never sees the
 * landing page flash first — which is what a client-side check would produce,
 * since `useUserId()` cannot tell "still loading" from "not signed in".
 *
 * `getUser()` rather than `getSession()`: it revalidates against the auth
 * server instead of trusting a cookie that can be forged. Access control still
 * lives in the middleware and in the row-level policies; this call only decides
 * which of two pages to render.
 */
export default async function Home() {
  const supabase = await createSupabaseServer();

  const {
    data: { user },
  } = await supabase.auth.getUser();

  return user ? <Dashboard /> : <LandingPage />;
}
