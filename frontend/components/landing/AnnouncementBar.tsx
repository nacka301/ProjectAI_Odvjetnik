import CtaLink from "./CtaLink";

export default function AnnouncementBar() {
  return (
    <div
      className="flex flex-wrap items-center gap-4 border-b-2 border-[var(--ptk-divider)] px-5 py-3 min-[1100px]:px-16"
      style={{ background: "var(--ptk-accent)", color: "var(--ptk-bg)" }}
    >
      <span className="w-2 h-2 shrink-0" style={{ background: "var(--ptk-bg)" }} aria-hidden />
      <span className="text-[13px] tracking-[0.06em]">
        Aplikacija još nije otvorena za prijave — izlazak planiran u rujnu 2026.
      </span>
      <CtaLink
        className="ml-auto min-h-[44px] inline-flex items-center text-[13px] underline"
        style={{ color: "var(--ptk-bg)" }}
      >
        Javite mi kad bude gotovo
      </CtaLink>
    </div>
  );
}
