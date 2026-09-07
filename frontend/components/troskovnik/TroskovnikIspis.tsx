"use client";

/**
 * Troškovnik onako kako se prilaže sudu.
 *
 * Zaseban blok, a ne prestiliziran zaslon: dokument ima drukčiju strukturu od
 * sučelja — pet stupaca umjesto osam, stranke po procesnoj ulozi, radnje
 * grupirane po vrsti, i potpis kojeg u zaslonskom prikazu uopće nema. Skrivanje
 * stupaca preko `td:nth-child()` ne bi moglo ni pregrupirati retke ni dodati
 * potpis.
 *
 * Nije novi prozor s ručno složenim HTML-om — komponenta živi u istom React
 * stablu i vidljiva je samo pod `@media print`, pa `window.print()` ostaje
 * kakav jest, a dijakritika i fontovi dolaze iz same stranice.
 *
 * Na dokumentu nema imena aplikacije, ograda ni pravnih izvora. Ovo je papir
 * koji ide sudu.
 *
 * ── Zašto CSS živi OVDJE, a ne u stranici koja komponentu poziva ──
 *
 * Prije je ovaj `<style jsx global>` stajao u `/troskovnik/page.tsx`, uz poziv
 * komponente. Kad je `/troskovnik/[id]/page.tsx` počeo pozivati istu
 * komponentu, taj stil se nije ponio s njom — rezultat je bio da se ova
 * komponenta na TOJ stranici prikazivala i na zaslonu, kao sirov,
 * nestilizirani blok teksta uz lijevi rub ekrana, ispod stvarnog sadržaja.
 * Doc-komentar iznad je tvrdio da je komponenta "vidljiva samo pod
 * @media print" — to je bilo točno samo dok se sjetiš kopirati stil na svaku
 * novu stranicu koja je poziva. Sad je stil DIO komponente, pa ta tvrdnja
 * vrijedi sama po sebi, na svakoj stranici koja ikad pozove `<TroskovnikIspis>`.
 */
import { formatDatumIspis, formatEur } from "@/lib/format";
import type { TroskovnikIzvozModel, IzvozTrosak } from "@/lib/troskovnik-izvoz-model";

/** Iznos je uvijek desno, dvije decimale, zarez kao decimalni razdjelnik. */
function Iznos({ value, bold }: { value: number; bold?: boolean }) {
  return <td className={`iznos${bold ? " bold" : ""}`}>{formatEur(value)}</td>;
}

/**
 * Redak zbroja: oznaka desno preko stupaca Tar. br. i Datum, iznos uz nju.
 *
 * Oznaka ne stane u sam stupac Datum — "Osnovica (bez PDV)" bi se prelomila u
 * dva reda. Spajanje dvaju stupaca joj daje dovoljno mjesta, a iznos ostaje u
 * svom stupcu.
 */
function RedakZbroja({ oznaka, value, bold }: { oznaka: string; value: number; bold?: boolean }) {
  return (
    <tr>
      <td />
      <td colSpan={2} className={`oznaka${bold ? " bold" : ""}`}>
        {oznaka}
      </td>
      <Iznos value={value} bold={bold} />
      <td />
    </tr>
  );
}

/** Prolazna stavka (pristojba ili predujam) kao redak glavne tablice. */
function RedakTroska({ t }: { t: IzvozTrosak }) {
  return (
    <tr>
      <td>{t.opis}</td>
      <td />
      <td className="datum">{t.datum ? formatDatumIspis(t.datum) : ""}</td>
      <Iznos value={t.iznos} />
      <td />
    </tr>
  );
}

export default function TroskovnikIspis({ model }: { model: TroskovnikIzvozModel }) {
  const { odvjetnik } = model;
  const adresa = [odvjetnik?.adresa, odvjetnik?.grad].filter(Boolean).join(", ");

  const imaPristojbe = model.pristojbe.length > 0 || model.ukupnoPristojbe > 0;
  const imaPredujmove = model.predujmovi.length > 0 || model.ukupnoPredujmovi > 0;
  const imaProlazne = imaPristojbe || imaPredujmove;

  const predmet: { oznaka: string; vrijednost: string }[] = [];
  if (model.sud) predmet.push({ oznaka: "Sud:", vrijednost: model.sud });
  if (model.zastupana) {
    predmet.push({
      oznaka: `${model.zastupana.polozaj || "Zastupana stranka"}:`,
      vrijednost: model.zastupana.ime,
    });
  }
  if (model.protustranka) {
    predmet.push({
      oznaka: `${model.protustranka.polozaj || "Protustranka"}:`,
      vrijednost: model.protustranka.ime,
    });
  }
  if (model.brojPredmeta) predmet.push({ oznaka: "Posl. br.:", vrijednost: model.brojPredmeta });

  return (
    <div id="troskovnik-ispis" aria-hidden="true">
      {/* ── Zaglavlje: odvjetnik, gore lijevo, bez okvira ── */}
      {odvjetnik && (
        <div className="zaglavlje">
          <div className="ime">{odvjetnik.ime}</div>
          {adresa && <div>{adresa}</div>}
          {odvjetnik.oib && <div>OIB: {odvjetnik.oib}</div>}
          {odvjetnik.iban && <div>IBAN: {odvjetnik.iban}</div>}
        </div>
      )}

      <h1>TROŠKOVNIK</h1>

      {/* ── Predmet: oznake desno, vrijednosti lijevo, oko sredine stranice ── */}
      {predmet.length > 0 && (
        <div className="predmet">
          {predmet.map((p) => (
            <div className="predmet-redak" key={p.oznaka}>
              <span className="predmet-oznaka">{p.oznaka}</span>
              <span className="predmet-vrijednost">{p.vrijednost}</span>
            </div>
          ))}
        </div>
      )}

      {/* ── Jedna tablica: radnje, zbroj, prolazni troškovi, sveukupno ── */}
      <table>
        <thead>
          <tr>
            <th className="st-radnja">Radnja</th>
            <th className="st-tarifa">Tar. br.</th>
            <th className="st-datum">Datum</th>
            <th className="st-iznos">Iznos</th>
            <th className="st-napomena">Napomena</th>
          </tr>
        </thead>
        <tbody>
          {model.radnje.map((r, i) => {
            // Naziv radnje se ponavlja samo kad se promijeni — kao u predlošku,
            // gdje „sastav podneska" stoji jednom, a datumi se nižu ispod.
            const isti = i > 0 && model.radnje[i - 1].opis === r.opis;
            return (
              <tr key={`${r.opis}-${i}`}>
                <td>{isti ? "" : r.opis}</td>
                <td className="tarifa">{isti ? "" : r.tarifniBroj}</td>
                <td className="datum">{r.datum ? formatDatumIspis(r.datum) : ""}</td>
                <Iznos value={r.iznos} />
                <td />
              </tr>
            );
          })}

          {/* Zbroj odvjetničkih radnji */}
          <RedakZbroja oznaka="UKUPNO:" value={model.zbrojNagrade.iznos} bold />

          {/* Naknada troškova ULAZI u osnovicu, pa stoji prije PDV-a. */}
          {model.naknadaTroskova > 0 && (
            <tr>
              <td>Naknada troškova (Tbr. 48.-51.)</td>
              <td />
              <td />
              <Iznos value={model.naknadaTroskova} />
              <td />
            </tr>
          )}

          {model.pdv && (
            <>
              <RedakZbroja oznaka="Osnovica (bez PDV):" value={model.pdv.osnovica} />
              <RedakZbroja oznaka="PDV (25%):" value={model.pdv.iznos} />
              <RedakZbroja oznaka="Nagrada s PDV-om:" value={model.pdv.ukupno} bold />
            </>
          )}

          {/* Prolazni troškovi — bez PDV-a, kao obični retci, kao u predlošku.
              Prazan razdjelni redak se izostavlja kad prolaznih troškova nema. */}
          {imaProlazne && (
            <tr className="razmak">
              <td />
              <td />
              <td />
              <td />
              <td />
            </tr>
          )}

          {/* Zbroj po odjeljku stoji ovdje, iako ga predložak nema: kod
              oslobođenja od pristojbi zbroj nije jednak zbroju navedenih iznosa
              (Zakon o sudskim pristojbama, čl. 11-13), pa bi bez njega
              troškovnik koji ide sudu bio računski neprovjerljiv. */}
          {imaPristojbe && (
            <>
              {model.pristojbe.map((p, i) => (
                <RedakTroska key={`pristojba-${i}`} t={p} />
              ))}
              <RedakZbroja oznaka="Ukupno pristojbe:" value={model.ukupnoPristojbe} />
            </>
          )}
          {imaPredujmove && (
            <>
              {model.predujmovi.map((p, i) => (
                <RedakTroska key={`predujam-${i}`} t={p} />
              ))}
              <RedakZbroja oznaka="Ukupno predujmovi:" value={model.ukupnoPredujmovi} />
            </>
          )}

          <RedakZbroja oznaka="UKUPNO:" value={model.sveukupno} bold />
        </tbody>
      </table>

      {/* ── Potpis ──
          Bez „Odvjetnik"/„Odvjetnica": aplikacija nema podatak o rodu, pa bi
          predložak nametnuo krivi oblik dijelu korisnika. */}
      {odvjetnik && (
        <div className="potpis">
          <div className="ime">{odvjetnik.ime}</div>
          {adresa && <div>{adresa}</div>}
        </div>
      )}

      {/* ── Napomena — slobodan tekst; bez nje nema odjeljka ── */}
      {model.napomena && <div className="napomena">{model.napomena}</div>}

      {/* ── Skrivanje na zaslonu + prijelom za ispis ──
          Dio ove komponente, ne stranice koja je poziva — vidi doc-komentar
          na vrhu datoteke. Obični <style> element ne mijenja izgled ostatka
          diva (aria-hidden ga i onako izuzima iz stabla pristupačnosti), pa
          ne treba Fragment ni dodatna razina uvlake. */}
      <style jsx global>{`
        #troskovnik-ispis { display: none; }

        /* Margina je nula, a razmak do ruba papira daje padding dokumenta.
           Razlog nije estetika: u marginu stranice preglednik crta vlastito
           zaglavlje i podnožje — naslov kartice i adresu stranice — pa bi
           ime aplikacije završilo na dokumentu koji ide sudu. Bez margine
           nema gdje, i izostavlja ih.
           Cijena: padding se ne ponavlja po stranicama, pa kod troškovnika
           duljeg od jedne stranice druga i daljnje počinju uz sam gornji rub.
           Bočni razmak ostaje na svima. Ako to zasmeta, alternativa je vratiti
           margin na @page — ali onda se URL vraća s njom, i gasi se jedino
           rukom, kvačicom „Zaglavlja i podnožja" u dijalogu za ispis. */
        @page { size: A4 portrait; margin: 0; }

        @media print {
          body { background: #fff; }
          .print\\:hidden { display: none !important; }

          #troskovnik-ispis {
            display: block;
            padding: 20mm 18mm;
            font: 11pt/1.35 Arial, Helvetica, sans-serif;
            color: #000;
          }

          /* Zaglavlje odvjetnika — gore lijevo, bez okvira i logotipa. */
          #troskovnik-ispis .zaglavlje { margin-bottom: 10mm; }
          #troskovnik-ispis .zaglavlje .ime { text-transform: uppercase; }

          #troskovnik-ispis h1 {
            font-size: 16pt;
            font-weight: normal;
            letter-spacing: 0.5pt;
            margin: 0 0 8mm 22mm;
          }

          /* Predmet — oznake desno, vrijednosti lijevo, oko sredine stranice. */
          #troskovnik-ispis .predmet { margin: 0 0 6mm 0; }
          #troskovnik-ispis .predmet-redak { display: flex; gap: 6mm; margin-bottom: 1mm; }
          #troskovnik-ispis .predmet-oznaka { flex: 0 0 42%; text-align: right; }
          #troskovnik-ispis .predmet-vrijednost { flex: 1; }

          #troskovnik-ispis table {
            width: 100%;
            border-collapse: collapse;
            font-size: 10pt;
          }
          #troskovnik-ispis th,
          #troskovnik-ispis td {
            border: 1px solid #000;
            padding: 3pt 4pt;
            vertical-align: top;
          }
          #troskovnik-ispis th { font-weight: normal; text-align: center; }

          /* Zaglavlje se ponavlja na svakoj stranici, redak se ne prelama. */
          #troskovnik-ispis thead { display: table-header-group; }
          #troskovnik-ispis tr { break-inside: avoid; page-break-inside: avoid; }

          #troskovnik-ispis .st-radnja { width: 42%; }
          #troskovnik-ispis .st-tarifa { width: 13%; }
          #troskovnik-ispis .st-datum { width: 14%; }
          #troskovnik-ispis .st-iznos { width: 15%; }
          #troskovnik-ispis .st-napomena { width: 16%; }

          #troskovnik-ispis .datum { text-align: center; }
          #troskovnik-ispis .oznaka { text-align: right; }
          #troskovnik-ispis .tarifa { text-align: center; }
          #troskovnik-ispis .iznos { text-align: right; white-space: nowrap; }
          #troskovnik-ispis .bold { font-weight: bold; }
          #troskovnik-ispis .razmak td { height: 5mm; }

          #troskovnik-ispis .potpis {
            margin-top: 10mm;
            text-align: center;
            break-inside: avoid;
            page-break-inside: avoid;
          }
          #troskovnik-ispis .potpis .ime { font-weight: bold; text-transform: uppercase; }

          #troskovnik-ispis .napomena {
            margin-top: 12mm;
            white-space: pre-line;
            break-inside: avoid;
            page-break-inside: avoid;
          }
        }
      `}</style>
    </div>
  );
}
