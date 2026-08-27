import CtaLink from "./CtaLink";

const LINKS = [
  { href: "#kako-radi", label: "Kako radi" },
  { href: "#moduli", label: "Moduli" },
  { href: "#sigurnost", label: "Sigurnost" },
  { href: "#cijene", label: "Cijene" },
];

export default function Nav() {
  return (
    <div className="border-b-2 border-[var(--ptk-divider)]">
      <div className="ptk-shell flex flex-wrap items-center gap-x-10 gap-y-2 px-5 py-4 min-[720px]:px-8 min-[1100px]:px-16 min-[1100px]:py-5">
        <div className="flex items-baseline gap-[10px] mr-auto">
          <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[22px] tracking-[-0.02em]">
            PRAVOTEK
          </span>
          <span className="w-[10px] h-[10px] bg-[var(--ptk-accent)]" aria-hidden />
        </div>

        {LINKS.map((l) => (
          <a
            key={l.href}
            href={l.href}
            className="inline-flex items-center min-h-[44px] text-[14px] text-inherit no-underline"
          >
            {l.label}
          </a>
        ))}
        <a href="#" className="inline-flex items-center min-h-[44px] text-[14px] text-inherit no-underline opacity-60">
          Kontakt
        </a>

        <CtaLink href="/auth/login" className="ptk-btn ptk-btn-primary text-[14px]" style={{ padding: "10px 18px" }}>
          Prijavite se
        </CtaLink>
      </div>
    </div>
  );
}
