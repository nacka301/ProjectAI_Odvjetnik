import Link from "next/link";
import type { ReactNode } from "react";

/**
 * Shared button/link styling for every call-to-action on the landing page.
 *
 * Used to scroll to a waitlist email field (pre-launch). The app is live now,
 * so this is a thin `next/link` wrapper — each call site passes the real
 * destination (`/auth/register`, `/auth/login`, or a `mailto:` for the
 * business-inquiry CTAs) instead of a shared waitlist target.
 */
export default function CtaLink({
  href,
  children,
  className = "",
  style,
}: {
  href: string;
  children: ReactNode;
  className?: string;
  style?: React.CSSProperties;
}) {
  return (
    <Link href={href} className={className} style={style}>
      {children}
    </Link>
  );
}
