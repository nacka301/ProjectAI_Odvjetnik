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
    </div>
  );
}
