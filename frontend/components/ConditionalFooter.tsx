"use client";

import { usePathname } from "next/navigation";
import { SecurityFooter } from "@/components/TrustComponents";

/**
 * Renders the SecurityFooter on all pages EXCEPT /auth/* routes (which have
 * their own dark-themed footer) and "/" — the public landing page there
 * (components/LandingPage.tsx) ships its own full Podnožje section per the
 * Modernist design spec, and stacking this rounded white trust strip below
 * it would collide with that page's flush-left, square-corner design.
 *
 * "/" also renders Dashboard for signed-in users, and Dashboard has no
 * footer of its own — it renders <SecurityFooter/> directly instead, so the
 * trust strip is not lost for that view.
 */
export function ConditionalSecurityFooter() {
  const pathname = usePathname();

  if (pathname?.startsWith("/auth") || pathname === "/") return null;

  return <SecurityFooter />;
}
