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
 * Na dokumentu nema imena aplikacije ni ograda. Ovo je papir koji ide sudu.
 * Jedina iznimka je kratka neutralna bilješka na dnu — po kojim je propisima
 * obračun napravljen i kojeg dana (`neutralnaBiljeska`, plan pravne zaštite,
 * dio A). Ona sudu govori pravnu osnovu, a ne tko je računao.
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
import { neutralnaBiljeska } from "@/lib/pravna-osnova";
import type { TroskovnikIzvozModel, IzvozTrosak } from "@/lib/troskovnik-izvoz-model";

/** Iznos je uvijek desno, dvije decimale, zarez kao decimalni razdjelnik. */
function Iznos({ value, bold }: { value: number; bold?: boolean }) {
  return <td className={`iznos${bold ? " bold" : ""}`}>{formatEur(value)}</td>;
}

/**
 * Jedan redak obračuna — oznaka lijevo, iznos desno, izvan tablice. `veliko`
 * je rezerviran za SVEUKUPNO: jedino ono smije biti podebljano i vidljivo
 * veće, da obračun ima jedan jasan konačan broj umjesto pet podebljanih.
 */
function RedakObracuna({
  oznaka,
  value,
  veliko,
}: {
  oznaka: string;
  value: number;
  veliko?: boolean;
}) {
  return (
    <div className={`obracun-redak${veliko ? " veliko" : ""}`}>
      <span>{oznaka}</span>
      <span>{formatEur(value)}</span>
    </div>
  );
}

/** Prolazna stavka (pristojba ili predujam) kao redak glavne tablice. */
function RedakTroska({ t, imaDatum }: { t: IzvozTrosak; imaDatum: boolean }) {
  return (
    <tr>
      <td>{t.opis}</td>
      <td />
      {imaDatum && <td className="datum">{t.datum ? formatDatumIspis(t.datum) : ""}</td>}
      <Iznos value={t.iznos} />
    </tr>
  );
}

export default function TroskovnikIspis({ model }: { model: TroskovnikIzvozModel }) {
  const { odvjetnik } = model;
  const adresa = [odvjetnik?.adresa, odvjetnik?.grad].filter(Boolean).join(", ");

  const biljeska = neutralnaBiljeska(model.pravnaOsnova);
  const imaPristojbe = model.pristojbe.length > 0 || model.ukupnoPristojbe > 0;
  const imaPredujmove = model.predujmovi.length > 0 || model.ukupnoPredujmovi > 0;
  const imaProlazne = imaPristojbe || imaPredujmove;

  // Čisti zbroj samih radnji, bez naknade troškova — `zbrojNagrade.iznos` je
  // zapravo osnovica (nagrada + naknada, kad je ima), pa bi ovdje udvostručio
  // naknadu koja ispod stoji kao zaseban redak.
  const nagradaRadnji = model.radnje.reduce((zbroj, r) => zbroj + r.iznos, 0);

  // Stupac se prikazuje samo ako barem jedan redak u njemu ima vrijednost —
  // inače ostaju tri stupca dovoljno široka za vlastiti sadržaj.
  const imaDatum =
    model.radnje.some((r) => r.datum) ||
    model.pristojbe.some((t) => t.datum) ||
    model.predujmovi.some((t) => t.datum);

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
  if (model.vrijednostSpora) {
    predmet.push({ oznaka: "VPS:", vrijednost: formatEur(model.vrijednostSpora) });
  }
  if (model.vrstaPostupkaNaziv) {
    predmet.push({ oznaka: "Vrsta postupka:", vrijednost: model.vrstaPostupkaNaziv });
  }

  return (
    <div id="troskovnik-ispis" aria-hidden="true">
      {/* ── Zaglavlje: odvjetnik, gore lijevo, bez okvira ── */}
      {odvjetnik && (
        <div className="zaglavlje">
          <div className="ime">{odvjetnik.ime}</div>
          {adresa && <div>{adresa}</div>}
          {odvjetnik.oib && <div>OIB: {odvjetnik.oib}</div>}
          {odvjetnik.iban && <div>IBAN: {odvjetnik.iban}</div>}
          {odvjetnik.telefon && <div>Tel: {odvjetnik.telefon}</div>}
          {odvjetnik.email && <div>{odvjetnik.email}</div>}
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
      <table className={imaDatum ? undefined : "bez-datuma"}>
        <thead>
          <tr>
            <th className="st-radnja">Radnja</th>
            <th className="st-tarifa">Tar. br.</th>
            {imaDatum && <th className="st-datum">Datum</th>}
            <th className="st-iznos">Iznos</th>
          </tr>
        </thead>
        <tbody>
          {model.radnje.map((r, i) => {
            // Naziv radnje se ponavlja samo kad se promijeni — kao u predlošku,
            // gdje „sastav podneska" stoji jednom, a datumi se nižu ispod.
            const isti = i > 0 && model.radnje[i - 1].opis === r.opis;
            return (
              <tr key={`${r.opis}-${i}`} className={i % 2 === 1 ? "alt" : undefined}>
                <td>{isti ? "" : r.opis}</td>
                <td className="tarifa">{isti ? "" : r.tarifniBroj}</td>
                {imaDatum && <td className="datum">{r.datum ? formatDatumIspis(r.datum) : ""}</td>}
                <Iznos value={r.iznos} />
              </tr>
            );
          })}

          {/* Prolazni troškovi — bez PDV-a, kao obični retci, kao u predlošku.
              Prazan razdjelni redak se izostavlja kad prolaznih troškova nema. */}
          {imaProlazne && (
            <tr className="razmak">
              <td />
              <td />
              {imaDatum && <td />}
              <td />
            </tr>
          )}

          {/* Pojedinačne stavke ostaju u tablici, iako ih obračun ispod zbraja
              u jedan redak: kod oslobođenja od pristojbi zbroj nije jednak
              zbroju navedenih iznosa (Zakon o sudskim pristojbama, čl. 11-13),
              pa bi bez razrade troškovnik koji ide sudu bio računski
              neprovjerljiv. */}
          {imaPristojbe && model.pristojbe.map((p, i) => (
            <RedakTroska key={`pristojba-${i}`} t={p} imaDatum={imaDatum} />
          ))}
          {imaPredujmove && model.predujmovi.map((p, i) => (
            <RedakTroska key={`predujam-${i}`} t={p} imaDatum={imaDatum} />
          ))}
        </tbody>
      </table>

      {/* ── Obračun — izvan tablice, poravnat desno. Jedan redak po stavci,
          nula-iznosi izostavljaju se u cijelosti; jedino SVEUKUPNO je
          podebljano i vidljivo veće. */}
      <div className="obracun">
        <div className="obracun-grupa">
          <RedakObracuna oznaka="Nagrada (zbroj radnji)" value={nagradaRadnji} />
          {model.naknadaTroskova > 0 && (
            <RedakObracuna oznaka="Naknada troškova (Tbr. 48.-51.)" value={model.naknadaTroskova} />
          )}
          {model.pdv && (
            <>
              <RedakObracuna oznaka={`PDV (${model.pdv.stopa} %)`} value={model.pdv.iznos} />
              <RedakObracuna oznaka="Nagrada s PDV-om" value={model.pdv.ukupno} />
            </>
          )}
        </div>

        {(model.ukupnoPristojbe > 0 || model.ukupnoPredujmovi > 0) && (
          <div className="obracun-grupa">
            {model.ukupnoPristojbe > 0 && (
              <RedakObracuna oznaka="Sudske pristojbe" value={model.ukupnoPristojbe} />
            )}
            {model.ukupnoPredujmovi > 0 && (
              <RedakObracuna oznaka="Izdaci i predujmovi" value={model.ukupnoPredujmovi} />
            )}
          </div>
        )}

        <div className="obracun-grupa">
          <RedakObracuna oznaka="SVEUKUPNO" value={model.sveukupno} veliko />
        </div>
      </div>

      {/* ── Potpis ── mjesto i datum lijevo, crta za potpis desno.
          Mjesto se namjerno ne deklinira ("Mjesto: Zagreb", ne "U Zagrebu") —
          deklinacija bi za velik dio gradova bila gramatički pogrešna.
          Bez „Odvjetnik"/„Odvjetnica": aplikacija nema podatak o rodu, pa bi
          predložak nametnuo krivi oblik dijelu korisnika. */}
      {odvjetnik && (
        <div className="potpis">
          <div className="potpis-mjesto-datum">
            {odvjetnik.grad && <div>Mjesto: {odvjetnik.grad}</div>}
            <div>Datum: {formatDatumIspis(model.datumIzrade)}</div>
          </div>
          <div className="potpis-crta">
            <div className="crta" />
            <div className="ime">{odvjetnik.ime}</div>
            {odvjetnik.osobnoIme && <div className="osobno-ime">{odvjetnik.osobnoIme}</div>}
          </div>
        </div>
      )}

      {/* ── Napomena — slobodan tekst; bez nje nema odjeljka ── */}
      {model.napomena && <div className="napomena">{model.napomena}</div>}

      {/* ── Pravna osnova — sitno, na dnu; kad propis nije zabilježen, izostaje ── */}
      {biljeska && <div className="pravna-osnova">{biljeska}</div>}

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
            font-size: 18pt;
            font-weight: 700;
            letter-spacing: 0.5pt;
            text-align: center;
            margin: 0 0 8mm;
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

          #troskovnik-ispis .st-radnja { width: 52%; }
          #troskovnik-ispis .st-tarifa { width: 16%; }
          #troskovnik-ispis .st-datum { width: 16%; }
          #troskovnik-ispis .st-iznos { width: 16%; }
          /* Bez stupca Datum (nijedan redak nema datum) preostala tri stupca
             preuzimaju njegovih 16%. */
          #troskovnik-ispis table.bez-datuma .st-radnja { width: 60%; }
          #troskovnik-ispis table.bez-datuma .st-tarifa { width: 20%; }
          #troskovnik-ispis table.bez-datuma .st-iznos { width: 20%; }

          #troskovnik-ispis .datum { text-align: center; }
          #troskovnik-ispis .tarifa { text-align: center; }
          #troskovnik-ispis .iznos { text-align: right; white-space: nowrap; }
          #troskovnik-ispis .bold { font-weight: bold; }
          #troskovnik-ispis .razmak td { height: 5mm; }

          /* Naizmjenično blago sivilo redaka radnji, radi čitkosti — samo na
             stvarnim retcima radnji, ne na zbrojnim/oznaka retcima. */
          #troskovnik-ispis tbody tr.alt { background: #f5f5f5; }

          /* Obračun — izvan tablice, poravnat desno. Prazan razmak između
             grupa dolazi od margine, ne od praznog retka. */
          #troskovnik-ispis .obracun {
            margin-top: 8mm;
            margin-left: auto;
            width: 75mm;
            break-inside: avoid;
            page-break-inside: avoid;
          }
          #troskovnik-ispis .obracun-grupa { margin-bottom: 4mm; }
          #troskovnik-ispis .obracun-grupa:last-child { margin-bottom: 0; }
          #troskovnik-ispis .obracun-redak {
            display: flex;
            justify-content: space-between;
            gap: 6mm;
            padding: 0.5mm 0;
          }
          /* Jedino SVEUKUPNO je podebljano i vidljivo veće — sve ostalo je
             obični tekst, da se konačan broj ne izgubi među pet podebljanih. */
          #troskovnik-ispis .obracun-redak.veliko {
            font-weight: bold;
            font-size: 13pt;
            margin-top: 2mm;
            padding-top: 2mm;
            border-top: 1px solid #000;
          }

          #troskovnik-ispis .potpis {
            margin-top: 15mm;
            display: flex;
            justify-content: space-between;
            align-items: flex-end;
            gap: 10mm;
            break-inside: avoid;
            page-break-inside: avoid;
          }
          #troskovnik-ispis .potpis-mjesto-datum { text-align: left; }
          #troskovnik-ispis .potpis-crta { text-align: center; }
          #troskovnik-ispis .potpis-crta .crta {
            width: 55mm;
            border-top: 1px solid #000;
            margin-bottom: 2mm;
          }
          #troskovnik-ispis .potpis .ime { font-weight: bold; text-transform: uppercase; }
          #troskovnik-ispis .potpis .osobno-ime { margin-top: 1mm; }

          #troskovnik-ispis .napomena {
            margin-top: 12mm;
            white-space: pre-line;
            break-inside: avoid;
            page-break-inside: avoid;
          }

          #troskovnik-ispis .pravna-osnova {
            margin-top: 10mm;
            font-size: 9pt;
            color: #333;
            break-inside: avoid;
            page-break-inside: avoid;
          }
        }
      `}</style>
    </div>
  );
}
