import CtaLink from "./CtaLink";

// Isti kontakt kao u pravnim tekstovima.
const KONTAKT_EMAIL = "vackadavid@gmail.com";

export default function PosterCta() {
  return (
    <div style={{ background: "var(--ptk-accent)", color: "var(--ptk-bg)" }}>
      <div className="ptk-shell px-6 py-16 min-[720px]:px-10 min-[720px]:py-20 min-[1100px]:px-16 min-[1100px]:py-[88px]">
        <h2 className="max-w-[20ch] text-[40px] min-[720px]:text-[56px] min-[1100px]:text-[88px] leading-[0.96] tracking-[-0.035em] m-0">
          Budite među prvim uredima koji koriste PravoTek.
        </h2>
        <div className="mt-10 flex flex-wrap items-center gap-4">
          <CtaLink
            href="/auth/register"
            className="ptk-btn text-[16px]"
            style={{ background: "var(--ptk-bg)", color: "var(--ptk-accent)", padding: "18px 26px" }}
          >
            Registrirajte se
          </CtaLink>
          <CtaLink
            href={`mailto:${KONTAKT_EMAIL}?subject=${encodeURIComponent("PravoTek — upit")}`}
            className="ptk-btn text-[16px]"
            style={{ borderColor: "rgba(243,242,242,.5)", color: "var(--ptk-bg)", padding: "18px 26px" }}
          >
            Pošaljite upit
          </CtaLink>
          <span className="max-w-[40ch] text-[13px] opacity-85">
            Izračun je pomoćno sredstvo i ne zamjenjuje prosudbu odvjetnika.
          </span>
        </div>
      </div>
    </div>
  );
}
