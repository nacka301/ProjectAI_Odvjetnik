import SectionGrid, { SectionNumber } from "./SectionGrid";

const KORACI = [
  {
    naziv: "Otvorite predmet",
    opis: "Stranka, vrsta postupka i vrijednost predmeta spora. Predmet ostaje u evidenciji ureda.",
  },
  {
    naziv: "Odaberete radnje",
    opis: "Uz svaku radnju stoji tarifni broj, broj bodova i datum. Ništa se ne računa iza zavjese.",
  },
  {
    naziv: "Dobijete troškovnik",
    opis: "Nagrada, sudske pristojbe i PDV slože se u troškovnik koji se sprema uz predmet i izvozi u PDF.",
  },
];

export default function HowItWorks() {
  return (
    <SectionGrid left={<SectionNumber n="01" />}>
      <h2 id="kako-radi" className="max-w-[22ch] text-[54px] leading-[1] tracking-[-0.03em] m-0">
        Tri koraka od predmeta do troškovnika.
      </h2>
      <div className="mt-12 grid grid-cols-1 min-[720px]:grid-cols-3 border-t-2 border-[var(--ptk-divider)]">
        {KORACI.map((k, i) => (
          <div
            key={k.naziv}
            className={i > 0 ? "border-l border-[var(--ptk-divider)]" : ""}
            style={{ padding: i === 0 ? "28px 32px 8px 0" : "28px 32px 8px" }}
          >
            <div className="font-[family-name:var(--font-archivo)] font-extrabold text-[64px] leading-none" style={{ color: "var(--ptk-accent)" }}>
              {String(i + 1).padStart(2, "0")}
            </div>
            <h3 className="text-[22px] mt-5 mb-2">{k.naziv}</h3>
            <p className="text-[15px] leading-[1.55] opacity-80 m-0">{k.opis}</p>
          </div>
        ))}
      </div>
    </SectionGrid>
  );
}
