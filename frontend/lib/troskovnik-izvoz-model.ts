/**
 * Jedan normalizirani oblik troškovnika koji troše obje izlazne putanje — izvoz
 * u `.xlsx` i ispis za sud.
 *
 * Postoji da se te dvije putanje ne raziđu. Prije je izvoz sam čitao `result`
 * pa `liveCalc`, sa svojim pravilima o tome koji je odjeljak prisutan; ispis bi
 * dobio vlastita. Čim bi se dodao odjeljak, jedna bi ga strana dobila a druga
 * ne, a razlika bi se vidjela tek na dokumentu koji je već predan.
 *
 * Prazan odjeljak ovdje ne postoji: `pdv` je `null` kad odvjetnik nije u sustavu
 * PDV-a, popisi su prazni kad nema stavaka. Potrošači tako ne ponavljaju uvjete
 * — samo izostave ono čega nema.
 */

/** Jedna odvjetnička radnja iz odjeljka I. */
export interface IzvozRadnja {
  opis: string;
  tarifniBroj: string;
  /** `null` kad datum nije unesen — polje je neobavezno. */
  datum: Date | null;
  kolicina: number;
  bodovaPoJedinici: number;
  ukupnoBodova: number;
  iznos: number;
}

/** Prolazna stavka — sudska pristojba ili predujam. Ne ulazi u osnovicu za PDV. */
export interface IzvozTrosak {
  datum: Date | null;
  opis: string;
  iznos: number;
}

export interface IzvozOdvjetnik {
  ime: string;
  oib?: string;
  adresa?: string;
  grad?: string;
  iban?: string;
}

/** Stranka onako kako stoji u zaglavlju dokumenta — po procesnoj ulozi, bez adrese. */
export interface IzvozStranka {
  /** "Tužitelj", "Tuženik", … Prazno kad uloga nije odabrana. */
  polozaj?: string;
  ime: string;
}

export interface TroskovnikIzvozModel {
  odvjetnik: IzvozOdvjetnik | null;
  sud?: string;
  brojPredmeta?: string;
  zastupana: IzvozStranka | null;
  protustranka: IzvozStranka | null;
  datumIzrade: Date;

  radnje: IzvozRadnja[];
  zbrojNagrade: { bodovi: number; iznos: number };
  /** Tbr. 48.-51. — ulazi u osnovicu. `0` kad je nema. */
  naknadaTroskova: number;
  /** `null` kad odvjetnik nije u sustavu PDV-a — tada odjeljak otpada u cijelosti. */
  pdv: { osnovica: number; iznos: number; ukupno: number } | null;

  pristojbe: IzvozTrosak[];
  ukupnoPristojbe: number;
  predujmovi: IzvozTrosak[];
  ukupnoPredujmovi: number;

  sveukupno: number;
  /** Prazno kad odvjetnik nije ništa upisao — tada se odjeljak ne ispisuje. */
  napomena?: string;
}

interface LinijaLike {
  opis: string;
  tarifni_broj: string;
  datum?: string | null;
  kolicina: number;
  bodova_po_jedinici: number;
  ukupno_bodova: number;
  iznos_eur: number;
  tbr41_bodovi?: number;
}

interface TrosakLike {
  datum?: string | null;
  opis: string;
  iznos: number;
}

interface PristojbenaStavkaLike {
  tarifni_broj: string;
  kolicina: number;
  iznos: number;
  uredba?: string;
}

interface LiveCalcLike {
  lines: LinijaLike[];
  ukupnoBodova: number;
  naknadaTroskovaUkupno: number;
  osnovicaSaNaknadom: number;
  pdv: number;
  ukupnoSPdv: number;
  ukupnoPristojbe: number;
  ukupnoPredujmovi: number;
  sveukupniTrosak: number;
}

interface ResultLike {
  stavke: LinijaLike[];
  ukupno_bodova: number;
  ukupno_eur_bez_pdv: number;
  pdv: number;
  ukupno_eur_s_pdv: number;
  naknada_troskova_ukupno?: number;
  is_pdv_ukljucen?: boolean;
  has_vat?: boolean;
  datum_izrade: string;
  sud?: string | null;
  broj_predmeta?: string | null;
  klijent: { ime: string } | null;
  odvjetnik: { ime: string; oib?: string; adresa?: string; grad?: string; iban?: string } | null;
  protustranka?: { ime: string } | null;
  protustranka_polozaj?: string | null;
  pristojbene_stavke?: PristojbenaStavkaLike[];
  dodatne_pristojbe?: TrosakLike[];
  ukupno_pristojbe?: number;
  predujmovi?: TrosakLike[];
  ukupno_predujmovi?: number;
  sveukupni_trosak?: number;
  napomena?: string | null;
}

/** Ulaz iz `page.tsx`. Namjerno labav: opisuje samo ono što se ovdje čita. */
export interface IzvozUlaz {
  result: ResultLike | null;
  liveCalc: LiveCalcLike;
  pristojbeneStavke: PristojbenaStavkaLike[];
  dodatnePristojbe: TrosakLike[];
  predujmovi: TrosakLike[];
  isPdvUkljucen: boolean;
  napomena: string;
  polja: {
    odvjetnikIme: string;
    odvjetnikOib: string;
    odvjetnikAdresa: string;
    odvjetnikGrad: string;
    odvjetnikIban: string;
    klijentIme: string;
    zastupanaPolozaj: string;
    protustrankaIme: string;
    protustrankaPolozaj: string;
    predmetSud: string;
    predmetPoslovniBroj: string;
  };
}

/** ISO datum iz zahtjeva/odgovora u `Date`; `null` kad ga nema ili je neispravan. */
function uDatum(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d;
}

function neprazno(s: string | null | undefined): string | undefined {
  const t = (s ?? "").trim();
  return t === "" ? undefined : t;
}

/**
 * Opis pristojbe onakav kakav stoji na dokumentu. Uredba naplaćuje po radnji, pa
 * svaki tarifni broj ide u svoj redak — zbroj bez razrade se ne bi dao provjeriti,
 * a troškovnik ide sudu.
 */
function opisPristojbe(s: PristojbenaStavkaLike): string {
  const kolicina = s.kolicina > 1 ? ` × ${s.kolicina}` : "";
  const stara = s.uredba === "NN 37/2023" ? " (NN 37/2023, čl. 2)" : "";
  return `Sudska pristojba — ${s.tarifni_broj}${kolicina}${stara}`;
}

export function buildIzvozModel(ulaz: IzvozUlaz): TroskovnikIzvozModel {
  const { result, liveCalc, polja } = ulaz;

  // Spremljeni rezultat je izvor istine kad postoji; bez njega se izvozi ono što
  // je na ekranu — isto ponašanje kao i dosad.
  const izvorLinija: LinijaLike[] = result?.stavke ?? liveCalc.lines;

  const radnje: IzvozRadnja[] = izvorLinija.map((l) => ({
    opis: l.opis,
    tarifniBroj: l.tarifni_broj,
    datum: uDatum(l.datum),
    kolicina: l.kolicina,
    bodovaPoJedinici: l.bodova_po_jedinici,
    // Tbr. 41 se na ekranu pribraja retku, pa mora i ovdje — inače zbroj
    // pojedinačnih bodova ne bi odgovarao ukupnom.
    ukupnoBodova: l.ukupno_bodova + (l.tbr41_bodovi ?? 0),
    iznos: l.iznos_eur,
  }));

  const jePdv = result
    ? (result.is_pdv_ukljucen ?? result.has_vat ?? ulaz.isPdvUkljucen)
    : ulaz.isPdvUkljucen;

  const osnovica = result ? result.ukupno_eur_bez_pdv : liveCalc.osnovicaSaNaknadom;
  const iznosPdv = result ? result.pdv : liveCalc.pdv;
  const sPdv = result ? result.ukupno_eur_s_pdv : liveCalc.ukupnoSPdv;

  const pristojbeneStavke = result?.pristojbene_stavke ?? ulaz.pristojbeneStavke;
  const dodatnePristojbe = result?.dodatne_pristojbe ?? ulaz.dodatnePristojbe;
  const predujmovi = result?.predujmovi ?? ulaz.predujmovi;

  const pristojbe: IzvozTrosak[] = [
    ...pristojbeneStavke.map((s) => ({
      datum: null,
      opis: opisPristojbe(s),
      iznos: s.iznos,
    })),
    ...dodatnePristojbe.map((t) => ({
      datum: uDatum(t.datum),
      opis: t.opis,
      iznos: t.iznos,
    })),
  ];

  const odvjetnikIme = neprazno(result?.odvjetnik?.ime ?? polja.odvjetnikIme);
  const zastupanaIme = neprazno(result?.klijent?.ime ?? polja.klijentIme);
  const protustrankaIme = neprazno(result?.protustranka?.ime ?? polja.protustrankaIme);

  return {
    odvjetnik: odvjetnikIme
      ? {
          ime: odvjetnikIme,
          oib: neprazno(result?.odvjetnik?.oib ?? polja.odvjetnikOib),
          adresa: neprazno(result?.odvjetnik?.adresa ?? polja.odvjetnikAdresa),
          grad: neprazno(result?.odvjetnik?.grad ?? polja.odvjetnikGrad),
          iban: neprazno(result?.odvjetnik?.iban ?? polja.odvjetnikIban),
        }
      : null,
    sud: neprazno(result?.sud ?? polja.predmetSud),
    brojPredmeta: neprazno(result?.broj_predmeta ?? polja.predmetPoslovniBroj),
    // Uloga se čita iz lokalnog stanja jer je ondje dostupna i prije spremanja.
    zastupana: zastupanaIme
      ? { polozaj: neprazno(polja.zastupanaPolozaj), ime: zastupanaIme }
      : null,
    protustranka: protustrankaIme
      ? {
          polozaj: neprazno(polja.protustrankaPolozaj) ?? neprazno(result?.protustranka_polozaj),
          ime: protustrankaIme,
        }
      : null,
    datumIzrade: uDatum(result?.datum_izrade) ?? new Date(),

    radnje,
    zbrojNagrade: {
      bodovi: result ? result.ukupno_bodova : liveCalc.ukupnoBodova,
      iznos: result ? result.ukupno_eur_bez_pdv : liveCalc.osnovicaSaNaknadom,
    },
    naknadaTroskova: result?.naknada_troskova_ukupno ?? liveCalc.naknadaTroskovaUkupno,
    pdv: jePdv ? { osnovica, iznos: iznosPdv, ukupno: sPdv } : null,

    pristojbe,
    ukupnoPristojbe: result?.ukupno_pristojbe ?? liveCalc.ukupnoPristojbe,
    predujmovi: predujmovi.map((t) => ({
      datum: uDatum(t.datum),
      opis: t.opis,
      iznos: t.iznos,
    })),
    ukupnoPredujmovi: result?.ukupno_predujmovi ?? liveCalc.ukupnoPredujmovi,

    sveukupno: result?.sveukupni_trosak ?? liveCalc.sveukupniTrosak,
    napomena: neprazno(result?.napomena ?? ulaz.napomena),
  };
}
