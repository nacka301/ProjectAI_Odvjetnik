import SectionGrid, { SectionNumber } from "./SectionGrid";

const REDCI = [
  {
    radnja: "Zbrajanje bodova po radnjama",
    rucno: "Prepisivanje iz Tarife, provjera dvaput",
    pravotek: "Bodovi uz tarifni broj, automatski",
  },
  {
    radnja: "Sudske pristojbe",
    rucno: "Traženje razreda u Uredbi",
    pravotek: "Izračun iz vrijednosti predmeta",
  },
  {
    radnja: "Izmjena propisa",
    rucno: "Nova tablica, stari predmeti neusklađeni",
    pravotek: "Iznosi odvojeni od izračuna",
  },
  {
    radnja: "Troškovnik za sud",
    rucno: "Ručno oblikovanje dokumenta",
    pravotek: "PDF uz predmet, jedan klik",
  },
  {
    radnja: "Provjera izračuna",
    rucno: "Ovisi o tome kako je tablica složena",
    pravotek: "Tarifni broj stoji uz svaku stavku",
  },
];

export default function Comparison() {
  return (
    <SectionGrid left={<SectionNumber n="03" />} background="surface">
      <h2 className="max-w-[24ch] text-[54px] leading-[1] tracking-[-0.03em] m-0">
        Isti posao, bez tablice u Excelu.
      </h2>
      <div className="mt-10 overflow-x-auto">
        <table className="ptk-table text-[15px] min-w-[560px]">
          <thead>
            <tr>
              <th style={{ width: "34%" }}>Radnja u uredu</th>
              <th style={{ width: "33%" }}>Ručno</th>
              <th style={{ width: "33%", color: "var(--ptk-accent-700)" }}>PravoTek</th>
            </tr>
          </thead>
          <tbody>
            {REDCI.map((r) => (
              <tr key={r.radnja}>
                <td>{r.radnja}</td>
                <td className="opacity-70">{r.rucno}</td>
                <td>
                  <strong>{r.pravotek}</strong>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionGrid>
  );
}
