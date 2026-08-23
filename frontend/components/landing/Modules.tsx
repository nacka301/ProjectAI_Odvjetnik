import SectionGrid, { SectionNumber } from "./SectionGrid";

type Status = "Dostupno" | "U razvoju";

const MODULI: { kod: string; naziv: string; opis: string; status: Status }[] = [
  {
    kod: "M01",
    naziv: "Odvjetnički troškovnik",
    opis: "Bodovanje radnji uz tarifni broj, sudske pristojbe po Uredbi, izvoz u PDF.",
    status: "Dostupno",
  },
  {
    kod: "M02",
    naziv: "Stranke i predmeti",
    opis: "Kartica stranke s poviješću predmeta i kronologijom događaja, odvojeno po uredu.",
    status: "Dostupno",
  },
  {
    kod: "M03",
    naziv: "Analiza ugovora",
    opis: "Rizične i nedostajuće odredbe u nacrtu kupoprodajnog ili zakupnog ugovora, s obrazloženjem.",
    status: "U razvoju",
  },
  {
    kod: "M04",
    naziv: "ZK izvadak",
    opis: "Vlasnici, suvlasnički dijelovi, teretovi, hipoteke i plombe izdvojeni u pregledan popis.",
    status: "U razvoju",
  },
  {
    kod: "M05",
    naziv: "Tržišna procjena",
    opis: "Raspon vrijednosti nekretnine iz usporedivih podataka i obilježja mikrolokacije, s obrazloženjem.",
    status: "U razvoju",
  },
  {
    kod: "M06",
    naziv: "Sudski tumač",
    opis: "Ćirilica ↔ latinica, engleski i njemački ↔ hrvatski, uz zadržavanje pravne terminologije.",
    status: "U razvoju",
  },
];

export default function Modules() {
  return (
    <SectionGrid left={<SectionNumber n="02" />}>
      <div className="flex flex-wrap items-end justify-between gap-12">
        <h2 id="moduli" className="text-[54px] leading-[1] tracking-[-0.03em] m-0">
          Što aplikacija radi.
        </h2>
        <p className="max-w-[34ch] text-[14px] opacity-70 m-0">
          Dva modula rade danas. Ostali su u razvoju i nisu dostupni u računu — pisati drugačije
          bilo bi obećanje na tuđi račun.
        </p>
      </div>

      <div
        className="mt-11 grid grid-cols-1 min-[720px]:grid-cols-2 min-[1100px]:grid-cols-3 border-t-2 border-l border-[var(--ptk-divider)]"
      >
        {MODULI.map((m) => (
          <div key={m.kod} className="p-6 border-r border-b border-[var(--ptk-divider)]">
            <div className="flex items-center justify-between gap-3">
              <span className="font-[family-name:var(--font-archivo)] font-extrabold text-[12px]" style={{ color: "var(--ptk-accent)" }}>
                {m.kod}
              </span>
              <span className={`ptk-tag ${m.status === "Dostupno" ? "ptk-tag-accent" : "ptk-tag-neutral"}`}>
                {m.status}
              </span>
            </div>
            <h3 className="text-[21px] mt-[18px] mb-2">{m.naziv}</h3>
            <p className="text-[14px] leading-[1.5] opacity-80 m-0">{m.opis}</p>
          </div>
        ))}
      </div>
    </SectionGrid>
  );
}
