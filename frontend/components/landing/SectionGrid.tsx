import type { ReactNode } from "react";

/**
 * The `88px 1fr` two-column shell used by every numbered section (and, with
 * different `left`/`padding`, by Hero and TroskovnikDemo too) — see README
 * "Layout (desktop, 1440px)". Kept in one place so seven section files don't
 * each re-type the grid, the divider and the responsive collapse.
 *
 * Two elements, deliberately: the outer one spans the full viewport and owns
 * the background and the 2px rule between sections, so both run edge to edge
 * on a monitor wider than 1440px; the inner one is the .ptk-shell that keeps
 * the grid itself at the authored 1440px.
 */
export default function SectionGrid({
  left,
  children,
  background,
  padding = "56px 64px 64px 40px",
  className = "",
}: {
  left: ReactNode;
  children: ReactNode;
  background?: "surface";
  padding?: string;
  className?: string;
}) {
  return (
    <div
      className={`border-b-2 border-[var(--ptk-divider)] ${className}`}
      style={background === "surface" ? { background: "var(--ptk-surface)" } : undefined}
    >
      <div className="ptk-section-grid ptk-shell">
        <div className="ptk-section-left">{left}</div>
        <div className="ptk-section-right" style={{ padding }}>
          {children}
        </div>
      </div>
    </div>
  );
}

/** The red "01"–"06" section number used in the left column. */
export function SectionNumber({ n }: { n: string }) {
  return (
    <div
      className="pt-[56px] text-center font-[family-name:var(--font-archivo)] font-extrabold text-[13px] tracking-[0.1em] text-[var(--ptk-accent)]"
      aria-hidden
    >
      {n}
    </div>
  );
}
