import SectionGrid, { SectionNumber } from "./SectionGrid";
import CtaLink from "./CtaLink";

// Isti kontakt kao u pravnim tekstovima i unutar aplikacije (lib/useSubscription.ts).
// Ne uvozi se odande: taj modul je "use client", a ova stranica je server komponenta.
const KONTAKT_EMAIL = "vackadavid@gmail.com";

// Nema iznosa: naplata nije uključena i ne smije se prikazivati kao da vrijedi
// (vidi docs/plan-pravna-zastita.md i Uvjete korištenja, čl. 4).

const POCETNI = [
  "Troškovnik bez ograničenja",
  "Stranke, predmeti i kronologija",
  "Ispis troškovnika i izvoz u Excel",
];

const PRODULJENJE = [
  "Podaci ostaju sačuvani",
  "Dvofaktorska prijava i evidencija pristupa",
  "Novi moduli kad budu spremni",
  "Naplata najavljena najmanje 30 dana unaprijed",
];

const DRUSTVA = [
  "Više korisnika na jednom uredu",
  "Prava pristupa po korisniku",
  "Uvođenje uz prijenos postojećih predmeta",
];

export default function Pricing() {
  return (
    <SectionGrid left={<SectionNumber n="05" />}>
      <h2 id="proba" className="text-[54px] leading-[1] tracking-[-0.03em] m-0">
        Besplatno tijekom probnog razdoblja.
      </h2>
      <p className="mt-4 max-w-[56ch] text-[16px] opacity-80">
        PravoTek je u ranoj fazi. Dok traje probno razdoblje, korištenje se ne naplaćuje i ne traži
        se kartica. Prije uvođenja naplate svaki korisnik dobiva obavijest najmanje 30 dana unaprijed.
      </p>

      <div className="mt-11 grid grid-cols-1 min-[720px]:grid-cols-3 border-t-2 border-[var(--ptk-divider)]">
        {/* Početni pristup */}
        <div style={{ padding: "32px 32px 32px 0" }}>
          <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Početni pristup</div>
          <div className="flex items-baseline gap-2 mt-3">
            <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[64px] leading-none">7</span>
            <span className="text-[14px] opacity-60">dana, 0 €</span>
          </div>
          <p className="mt-4 text-[15px] opacity-80">Puni pristup na stvarnom predmetu, bez unosa kartice.</p>
          <div className="mt-6 border-t border-[var(--ptk-divider)]">
            {POCETNI.map((stavka) => (
              <div key={stavka} className="py-[10px] border-b border-[var(--ptk-divider)] text-[14px]">
                {stavka}
              </div>
            ))}
          </div>
          <CtaLink
            href="/auth/register"
            className="ptk-btn ptk-btn-secondary ptk-btn-block mt-7"
            style={{ padding: "14px 18px" }}
          >
            Registrirajte se
          </CtaLink>
        </div>

        {/* Produljenje — istaknuto */}
        <div
          className="border-l-2 border-[var(--ptk-divider)] p-8"
          style={{ background: "var(--ptk-text)", color: "var(--ptk-bg)" }}
        >
          <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Nakon sedam dana</div>
          <div className="flex items-baseline gap-2 mt-3">
            <span
              className="font-[family-name:var(--font-archivo)] font-extrabold text-[64px] leading-none"
              style={{ color: "var(--ptk-accent-400)" }}
            >
              0 €
            </span>
            <span className="text-[14px] opacity-60">produljenje pristupa</span>
          </div>
          <p className="mt-4 text-[15px] opacity-85">
            Javite se e-poštom i pristup se produljuje bez naknade, obično isti dan.
          </p>
          <div className="mt-6 border-t" style={{ borderColor: "rgba(243,242,242,.25)" }}>
            {PRODULJENJE.map((stavka) => (
              <div
                key={stavka}
                className="py-[10px] text-[14px] border-b"
                style={{ borderColor: "rgba(243,242,242,.25)" }}
              >
                {stavka}
              </div>
            ))}
          </div>
          <CtaLink
            href="/auth/register"
            className="ptk-btn ptk-btn-primary ptk-btn-block mt-7"
            style={{ padding: "14px 18px" }}
          >
            Registrirajte se →
          </CtaLink>
        </div>

        {/* Odvjetnička društva */}
        <div className="border-l-2 border-[var(--ptk-divider)]" style={{ padding: "32px 0 32px 32px" }}>
          <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Odvjetnička društva</div>
          <div className="flex items-baseline gap-2 mt-3">
            <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[44px] leading-[1.2]">
              Više korisnika
            </span>
          </div>
          <p className="mt-4 text-[15px] opacity-80">Za društva s više odvjetnika i zajedničkim uredom.</p>
          <div className="mt-6 border-t border-[var(--ptk-divider)]">
            {DRUSTVA.map((stavka) => (
              <div key={stavka} className="py-[10px] border-b border-[var(--ptk-divider)] text-[14px]">
                {stavka}
              </div>
            ))}
          </div>
          <CtaLink
            href={`mailto:${KONTAKT_EMAIL}?subject=${encodeURIComponent("PravoTek — odvjetničko društvo")}`}
            className="ptk-btn ptk-btn-secondary ptk-btn-block mt-7"
            style={{ padding: "14px 18px" }}
          >
            Javite se
          </CtaLink>
        </div>
      </div>
    </SectionGrid>
  );
}
