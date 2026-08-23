import Link from "next/link";
import SectionGrid, { SectionNumber } from "./SectionGrid";

const STAVKE = [
  { kod: "S1", naziv: "Šifriran prijenos", opis: "TLS 1.3 između preglednika i poslužitelja." },
  { kod: "S2", naziv: "Dvofaktorska prijava", opis: "Drugi faktor uz lozinku, po odluci ureda." },
  { kod: "S3", naziv: "Odvojeno po uredu", opis: "Razdvajanje na razini baze, ne samo u sučelju." },
  { kod: "S4", naziv: "Evidencija pristupa", opis: "Zapis tko je i kada otvorio predmet." },
];

export default function Security() {
  return (
    <SectionGrid left={<SectionNumber n="04" />}>
      <div className="grid grid-cols-1 min-[720px]:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] gap-16">
        <div>
          <h2 id="sigurnost" className="text-[54px] leading-[1] tracking-[-0.03em] m-0">
            Odvjetnička tajna nije postavka koju treba uključiti.
          </h2>
          <p className="mt-6 text-[17px] leading-[1.55] opacity-85">
            Podaci su odvojeni po odvjetničkom uredu na razini baze, a ne samo u sučelju — svaki
            upit provjerava kojem uredu zapis pripada. Prijenos je šifriran, a pristup se
            dodatno štiti dvofaktorskom prijavom. Obrada je sukladna Općoj uredbi o zaštiti
            podataka i obvezi čuvanja odvjetničke tajne.
          </p>
          <div className="flex flex-wrap gap-6 mt-7 text-[14px]">
            <Link href="/privatnost">Politika privatnosti</Link>
            <Link href="/uvjeti">Uvjeti korištenja</Link>
            <Link href="/dpa">Ugovor o obradi podataka</Link>
          </div>
        </div>
        <div className="border-t-2 border-[var(--ptk-divider)]">
          {STAVKE.map((s) => (
            <div key={s.kod} className="flex gap-5 py-[18px] border-b border-[var(--ptk-divider)]">
              <span
                className="font-[family-name:var(--font-archivo)] font-extrabold text-[12px] min-w-8"
                style={{ color: "var(--ptk-accent)" }}
              >
                {s.kod}
              </span>
              <div>
                <h4 className="text-[16px] m-0 mb-1">{s.naziv}</h4>
                <p className="text-[14px] opacity-75 m-0">{s.opis}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </SectionGrid>
  );
}
