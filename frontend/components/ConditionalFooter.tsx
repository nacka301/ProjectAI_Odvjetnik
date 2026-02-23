"use client";

import { usePathname } from "next/navigation";
import { SecurityFooter } from "@/components/TrustComponents";

/**
 * Renders the SecurityFooter on all pages EXCEPT /auth/* routes,
 * where the auth layout already has its own dark-themed footer.
 */
export function ConditionalSecurityFooter() {
  const pathname = usePathname();

  if (pathname?.startsWith("/auth")) return null;

  return <SecurityFooter />;
}
