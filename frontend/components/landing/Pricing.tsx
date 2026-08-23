import SectionGrid, { SectionNumber } from "./SectionGrid";
import CtaLink from "./CtaLink";

const PROBA = [
  "Troškovnik bez ograničenja tijekom probe",
  "Stranke, predmeti i kronologija",
  "AI krediti za isprobavanje modula",
  "Izvoz u PDF",
];

const PRETPLATA = [
  "Neograničen broj troškovnika",
  "Stranke, predmeti i kronologija",
  "Dvofaktorska prijava i evidencija pristupa",
  "Novi moduli čim izađu, bez doplate",
  "Cijena zaključana za rane korisnike",
];

const DRUSTVA = [
  "Više korisnika na jednom uredu",
  "Prava pristupa po korisniku",
  "Jedan račun za cijelo društvo",
  "Uvođenje uz prijenos postojećih predmeta",
];

export default function Pricing() {
  return (
    <SectionGrid left={<SectionNumber n="05" />}>
      <h2 id="cijene" className="text-[54px] leading-[1] tracking-[-0.03em] m-0">
        Jedna pretplata po računu.
      </h2>
      <p className="mt-4 max-w-[56ch] text-[16px] opacity-80">
        Cjenik vrijedi od izlaska u rujnu 2026.: sedam dana probe bez unosa kartice, zatim 19 €
        mjesečno po računu. Odvjetnička društva s više korisnika ugovaraju se posebno.
      </p>

      <div className="mt-11 grid grid-cols-1 min-[720px]:grid-cols-3 border-t-2 border-[var(--ptk-divider)]">
        {/* Proba */}
        <div style={{ padding: "32px 32px 32px 0" }}>
          <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Proba</div>
          <div className="flex items-baseline gap-2 mt-3">
            <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[64px] leading-none">7</span>
            <span className="text-[14px] opacity-60">dana, 0 €</span>
          </div>
          <p className="mt-4 text-[15px] opacity-80">Puni pristup na stvarnom predmetu, bez unosa kartice.</p>
          <div className="mt-6 border-t border-[var(--ptk-divider)]">
            {PROBA.map((stavka) => (
              <div key={stavka} className="py-[10px] border-b border-[var(--ptk-divider)] text-[14px]">
                {stavka}
              </div>
            ))}
          </div>
          <CtaLink className="ptk-btn ptk-btn-secondary ptk-btn-block mt-7" style={{ padding: "14px 18px" }}>
            Prijavite se
          </CtaLink>
        </div>

        {/* Pretplata — istaknuto */}
        <div
          className="border-l-2 border-[var(--ptk-divider)] p-8"
          style={{ background: "var(--ptk-text)", color: "var(--ptk-bg)" }}
        >
          <div className="flex items-center justify-between gap-4">
            <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Pretplata</div>
            <span className="text-[11px] px-[10px] py-[3px]" style={{ background: "var(--ptk-accent)", color: "var(--ptk-bg)" }}>
              RANI PRISTUP
            </span>
          </div>
          <div className="flex items-baseline gap-2 mt-3">
            <span
              className="font-[family-name:var(--font-archivo)] font-extrabold text-[64px] leading-none"
              style={{ color: "var(--ptk-accent-400)" }}
            >
              19 €
            </span>
            <span className="text-[14px] opacity-60">/ mjesečno po računu</span>
          </div>
          <p className="mt-4 text-[15px] opacity-85">ili 190 € godišnje — dva mjeseca manje.</p>
          <div className="mt-6 border-t" style={{ borderColor: "rgba(243,242,242,.25)" }}>
            {PRETPLATA.map((stavka) => (
              <div
                key={stavka}
                className="py-[10px] text-[14px] border-b"
                style={{ borderColor: "rgba(243,242,242,.25)" }}
              >
                {stavka}
              </div>
            ))}
          </div>
          <CtaLink className="ptk-btn ptk-btn-primary ptk-btn-block mt-7" style={{ padding: "14px 18px" }}>
            Prijavite se →
          </CtaLink>
        </div>

        {/* Odvjetnička društva */}
        <div className="border-l-2 border-[var(--ptk-divider)]" style={{ padding: "32px 0 32px 32px" }}>
          <div className="text-[12px] uppercase tracking-[0.14em] opacity-60">Odvjetnička društva</div>
          <div className="flex items-baseline gap-2 mt-3">
            <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[44px] leading-[1.2]">
              Po dogovoru
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
          <CtaLink className="ptk-btn ptk-btn-secondary ptk-btn-block mt-7" style={{ padding: "14px 18px" }}>
            Javite nam se
          </CtaLink>
        </div>
      </div>
    </SectionGrid>
  );
}
