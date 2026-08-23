"use client";

import type { ReactNode } from "react";

const TARGET_ID = "lista-cekanja-email";

/**
 * Every CTA on the landing scrolls to the hero waitlist form and focuses its
 * email field (README "Interactions & Behavior"). The `href` is a real
 * fragment link so it still works with JavaScript disabled or before
 * hydration — the click handler only upgrades it to a smooth scroll + focus.
 */
export default function CtaLink({
  children,
  className = "",
  style,
}: {
  children: ReactNode;
  className?: string;
  style?: React.CSSProperties;
}) {
  return (
    <a
      href={`#${TARGET_ID}`}
      className={className}
      style={style}
      onClick={(e) => {
        const el = document.getElementById(TARGET_ID);
        if (!el) return;
        e.preventDefault();
        el.scrollIntoView({ behavior: "smooth", block: "center" });
        el.focus({ preventScroll: true });
      }}
    >
      {children}
    </a>
  );
}

export { TARGET_ID as WAITLIST_EMAIL_ID };
