import CtaLink from "./CtaLink";

export default function AnnouncementBar() {
  return (
    <div
      className="border-b-2 border-[var(--ptk-divider)]"
      style={{ background: "var(--ptk-accent)", color: "var(--ptk-bg)" }}
    >
      <div className="ptk-shell flex flex-wrap items-center gap-4 px-5 py-3 min-[1100px]:px-16">
        <span className="w-2 h-2 shrink-0" style={{ background: "var(--ptk-bg)" }} aria-hidden />
        <span className="text-[13px] tracking-[0.06em]">
          Aplikacija je otvorena — besplatno tijekom probnog razdoblja, bez unosa kartice.
        </span>
        <CtaLink
          href="/auth/register"
          className="ml-auto min-h-[44px] inline-flex items-center text-[13px] underline"
          style={{ color: "var(--ptk-bg)" }}
        >
          Registrirajte se →
        </CtaLink>
      </div>
    </div>
  );
}
