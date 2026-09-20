/**
 * Troškovnik kao pravi `.xlsx`.
 *
 * Prije je ovo bio CSV složen spajanjem stringova. To se nije dalo dotjerati do
 * „otvori i gotovo": razdjelnik Excel bira po regionalnim postavkama čitatelja,
 * a ne po datoteci, pa isti CSV na jednom računalu ima stupce a na drugom ne.
 * Vodeće nule (OIB) u običnom CSV-u nema načina sačuvati.
 *
 * U xlsx-u oboje otpada. Iznos se pohranjuje kao broj `1234.56` uz format
 * `#,##0.00" €"` — zapis je jezično neutralan, pa ga Excel na hrvatskim
 * postavkama sam prikaže kao `1.234,56 €`, a na engleskima kao `1,234.56 €`.
 * Datum je serijski broj, ne tekst. OIB je tekstualna ćelija.
 *
 * Svi odjeljci dijele **istu mrežu stupaca**, pa iznos uvijek stoji u stupcu G.
 * U CSV-u su tri podtablice imale različit broj stupaca, pa su iznosi padali čas
 * u stupac I, čas u stupac C.
 */
import type { TroskovnikIzvozModel } from "./troskovnik-izvoz-model";
import { punaBiljeska } from "./pravna-osnova";

/** Ćelija onako kako je očekuje `write-excel-file`. */
type Cell = {
  value?: string | number | Date;
  type?: StringConstructor | NumberConstructor | DateConstructor;
  format?: string;
  fontWeight?: "bold";
  fontSize?: number;
  align?: "left" | "center" | "right";
  wrap?: boolean;
  borderStyle?: "thin";
  borderColor?: string;
} | null;

type Row = Cell[];

/** Jezično neutralan zapis — Excel ga prikaže po postavkama čitatelja. */
const NOVAC = '#,##0.00" €"';
const BODOVI = "#,##0.00";
const DATUM = "dd.mm.yyyy";
const OKVIR = { borderStyle: "thin", borderColor: "#000000" } as const;

/** Broj stupaca; svaki redak se dopunjuje do ove širine. */
const STUPACA = 7;

export const STUPCI = [
  { width: 6 }, // A — Rbr.
  { width: 12 }, // B — Datum
  { width: 52 }, // C — Opis
  { width: 16 }, // D — Tar. br.
  { width: 7 }, // E — Kol.
  { width: 11 }, // F — Bodova / oznaka zbroja
  { width: 16 }, // G — Iznos
];

/**
 * Datum sveden na podne po UTC-u.
 *
 * `convertDateToSerialNumber` u biblioteci dijeli `getTime()` s duljinom dana i
 * ne dira vremensku zonu. Datum parsiran kao lokalna ponoć time ispadne dan
 * ranije (u Hrvatskoj je lokalna ponoć 22:00 UTC prethodnog dana). Podne po
 * UTC-u je dovoljno daleko od obje granice da prikazani dan uvijek odgovara
 * onome što odvjetnik vidi u aplikaciji.
 */
function uSerijskiDatum(d: Date): Date {
  return new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate(), 12));
}

function iznos(n: number, podebljano = false): Cell {
  return {
    value: n,
    type: Number,
    format: NOVAC,
    align: "right",
    ...(podebljano ? { fontWeight: "bold" as const } : {}),
  };
}

/** Oznaka zbroja stoji desno u stupcu F, tik uz iznos u G. */
function oznakaZbroja(tekst: string, podebljano = false): Row {
  return [
    null,
    null,
    null,
    null,
    null,
    { value: tekst, type: String, align: "right", ...(podebljano ? { fontWeight: "bold" as const } : {}) },
  ];
}

function naslovOdjeljka(tekst: string): Row {
  return [{ value: tekst, type: String, fontWeight: "bold" }];
}

/** Par oznaka/vrijednost u zaglavlju dokumenta. */
function podatak(oznaka: string, vrijednost: string | Date, tip: StringConstructor | DateConstructor = String): Row {
  return [
    { value: oznaka, type: String, fontWeight: "bold" },
    null,
    tip === Date
      ? { value: uSerijskiDatum(vrijednost as Date), type: Date, format: DATUM }
      : { value: vrijednost as string, type: String },
  ];
}

/** Dopunjuje redak do pune širine da Excel ne dobije nazubljene retke. */
function poravnaj(row: Row): Row {
  return [...row, ...Array<Cell>(Math.max(0, STUPACA - row.length)).fill(null)];
}

/**
 * Slaže cijeli list. Odjeljci kojih nema — PDV, pristojbe, predujmovi, napomena
 * — ne ostavljaju prazno zaglavlje za sobom, nego se izostavljaju u cijelosti.
 */
export function buildTroskovnikSheet(m: TroskovnikIzvozModel): Row[] {
  const redci: Row[] = [];
  const prazno = () => redci.push([]);

  // ── Zaglavlje dokumenta ──
  redci.push([{ value: "TROŠKOVNIK", type: String, fontWeight: "bold", fontSize: 16 }]);
  prazno();

  if (m.odvjetnik) {
    redci.push(podatak("Odvjetnik", m.odvjetnik.ime));
    // OIB kao tekst — kao broj bi izgubio vodeću nulu i postao 10-znamenkast.
    if (m.odvjetnik.oib) redci.push(podatak("OIB", m.odvjetnik.oib));
    const adresa = [m.odvjetnik.adresa, m.odvjetnik.grad].filter(Boolean).join(", ");
    if (adresa) redci.push(podatak("Adresa", adresa));
    if (m.odvjetnik.iban) redci.push(podatak("IBAN", m.odvjetnik.iban));
  }
  if (m.sud) redci.push(podatak("Sud", m.sud));
  // Poslovni broj je tekst: "P-007/24" bi kao broj bio uništen.
  if (m.brojPredmeta) redci.push(podatak("Poslovni broj", m.brojPredmeta));
  if (m.zastupana) redci.push(podatak(m.zastupana.polozaj || "Zastupana stranka", m.zastupana.ime));
  if (m.protustranka) redci.push(podatak(m.protustranka.polozaj || "Protustranka", m.protustranka.ime));
  redci.push(podatak("Datum izrade", m.datumIzrade, Date));
  prazno();

  // ── I. Odvjetničke radnje ──
  redci.push(naslovOdjeljka("I. Odvjetničke radnje"));
  redci.push(
    ["Rbr.", "Datum", "Opis", "Tar. br.", "Kol.", "Bodova", "Iznos"].map((t, i) => ({
      value: t,
      type: String,
      fontWeight: "bold" as const,
      align: i >= 4 ? ("right" as const) : ("left" as const),
      ...OKVIR,
    })),
  );

  m.radnje.forEach((r, i) => {
    redci.push([
      { value: i + 1, type: Number, format: "0", align: "right", ...OKVIR },
      r.datum
        ? { value: uSerijskiDatum(r.datum), type: Date, format: DATUM, align: "center", ...OKVIR }
        : { value: "", type: String, ...OKVIR },
      { value: r.opis, type: String, wrap: true, ...OKVIR },
      { value: r.tarifniBroj, type: String, ...OKVIR },
      { value: r.kolicina, type: Number, format: BODOVI, align: "right", ...OKVIR },
      { value: r.ukupnoBodova, type: Number, format: BODOVI, align: "right", ...OKVIR },
      { value: r.iznos, type: Number, format: NOVAC, align: "right", ...OKVIR },
    ]);
  });

  redci.push([
    ...oznakaZbroja("Zbroj nagrade", true),
    { value: m.zbrojNagrade.iznos, type: Number, format: NOVAC, align: "right", fontWeight: "bold" },
  ]);
  redci.push([
    null,
    null,
    null,
    null,
    null,
    { value: "Ukupno bodova", type: String, align: "right" },
    { value: m.zbrojNagrade.bodovi, type: Number, format: BODOVI, align: "right" },
  ]);

  // Naknada troškova ULAZI u osnovicu, pa stoji prije PDV-a — za razliku od
  // pristojbi i predujmova, koji su prolazne stavke i imaju vlastite odjeljke.
  if (m.naknadaTroskova > 0) {
    redci.push([...oznakaZbroja("Naknada troškova (Tbr. 48.-51.)"), iznos(m.naknadaTroskova)]);
  }

  if (m.pdv) {
    redci.push([...oznakaZbroja("Osnovica (bez PDV)"), iznos(m.pdv.osnovica)]);
    redci.push([...oznakaZbroja(`PDV (${m.pdv.stopa}%)`), iznos(m.pdv.iznos)]);
    redci.push([...oznakaZbroja("Nagrada s PDV-om", true), iznos(m.pdv.ukupno, true)]);
  }

  // ── II. Sudske pristojbe ──
  if (m.pristojbe.length > 0 || m.ukupnoPristojbe > 0) {
    prazno();
    redci.push(naslovOdjeljka("II. Sudske pristojbe"));
    for (const p of m.pristojbe) {
      redci.push([
        null,
        p.datum ? { value: uSerijskiDatum(p.datum), type: Date, format: DATUM, align: "center" } : null,
        { value: p.opis, type: String, wrap: true },
        null,
        null,
        null,
        iznos(p.iznos),
      ]);
    }
    redci.push([...oznakaZbroja("Ukupno pristojbe", true), iznos(m.ukupnoPristojbe, true)]);
  }

  // ── III. Predujmovi ──
  if (m.predujmovi.length > 0 || m.ukupnoPredujmovi > 0) {
    prazno();
    redci.push(naslovOdjeljka("III. Predujmovi"));
    for (const p of m.predujmovi) {
      redci.push([
        null,
        p.datum ? { value: uSerijskiDatum(p.datum), type: Date, format: DATUM, align: "center" } : null,
        { value: p.opis, type: String, wrap: true },
        null,
        null,
        null,
        iznos(p.iznos),
      ]);
    }
    redci.push([...oznakaZbroja("Ukupno predujmovi", true), iznos(m.ukupnoPredujmovi, true)]);
  }

  // ── Sveukupno ──
  prazno();
  redci.push([...oznakaZbroja("SVEUKUPNI TROŠAK", true), iznos(m.sveukupno, true)]);

  if (m.napomena) {
    prazno();
    redci.push(naslovOdjeljka("Napomena"));
    redci.push([{ value: m.napomena, type: String, wrap: true }]);
  }

  // Excel nije papir koji ide sudu, nego radna datoteka ureda — zato puna bilješka,
  // s imenom alata i s tim tko za izračun odgovara (plan pravne zaštite, dio A).
  prazno();
  redci.push([{ value: punaBiljeska(m.pravnaOsnova), type: String, wrap: true }]);

  return redci.map(poravnaj);
}

/** Naziv datoteke — `troskovnik-2026-08-23.xlsx`. */
export function nazivDatoteke(danas: Date): string {
  const g = danas.getFullYear();
  const m = String(danas.getMonth() + 1).padStart(2, "0");
  const d = String(danas.getDate()).padStart(2, "0");
  return `troskovnik-${g}-${m}-${d}.xlsx`;
}
