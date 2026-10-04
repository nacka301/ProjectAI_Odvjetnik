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
import { datumBezVremena, uredbeIzStavaka, type PravnaOsnova } from "@/lib/pravna-osnova";
import { PDV_RATE, TARIFA_TRENUTNA, ULOGE_ZASTUPANJA, VRSTA_POSTUPKA_PRAVILA, type TarifaVerzija, type UlogaZastupanja, type VrstaPostupka } from "@/lib/tarifa";

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
  /** Npr. „umanjeno 50% (Tbr. 4 t. 8 Tarife)" — prazno kad nagrada nije umanjena ulogom. */
  napomena?: string;
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
  telefon?: string;
  email?: string;
  /** Osobno ime prijavljenog odvjetnika (`user_profiles.full_name`) — za potpis, uz naziv ureda. */
  osobnoIme?: string;
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
  /** Vrijednost predmeta spora, u EUR. Prazno kad nije poznata. */
  vrijednostSpora?: number;
  /** Čitljiv naziv VPS kategorije (čl. 14-25 Zakona o sudskim pristojbama), npr. "Opće pravilo". */
  vrstaPostupkaNaziv?: string;
  /** „Zastupanje: punomoćnik oštećenika [ime]" — samo kazneni/prekršajni troškovnik. */
  zastupanje?: string;
  datumIzrade: Date;

  radnje: IzvozRadnja[];
  zbrojNagrade: { bodovi: number; iznos: number };
  /** Tbr. 48.-51. — ulazi u osnovicu. `0` kad je nema. */
  naknadaTroskova: number;
  /** `null` kad odvjetnik nije u sustavu PDV-a — tada odjeljak otpada u cijelosti. */
  pdv: { osnovica: number; iznos: number; ukupno: number; stopa: number } | null;

  pristojbe: IzvozTrosak[];
  ukupnoPristojbe: number;
  predujmovi: IzvozTrosak[];
  ukupnoPredujmovi: number;

  sveukupno: number;
  /** Prazno kad odvjetnik nije ništa upisao — tada se odjeljak ne ispisuje. */
  napomena?: string;

  /** Po čemu je izračunat — za bilješku na ispisu, u Excelu i u aplikaciji. */
  pravnaOsnova: PravnaOsnova;
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
  umanjenje_uloge?: string;
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
  uloga_zastupanja?: string | null;
  klijent: { ime: string } | null;
  odvjetnik: {
    ime: string;
    oib?: string;
    adresa?: string;
    grad?: string;
    iban?: string;
    telefon?: string;
    email?: string;
  } | null;
  protustranka?: { ime: string } | null;
  protustranka_polozaj?: string | null;
  pristojbene_stavke?: PristojbenaStavkaLike[];
  dodatne_pristojbe?: TrosakLike[];
  ukupno_pristojbe?: number;
  predujmovi?: TrosakLike[];
  ukupno_predujmovi?: number;
  sveukupni_trosak?: number;
  napomena?: string | null;
  // Vrijednost predmeta spora i kategorija po kojoj je razriješena (čl. 14-25
  // Zakona o sudskim pristojbama) — za podatke o predmetu na ispisu.
  vrijednost_spora?: number | null;
  vps_vrsta_postupka?: string | null;
  // Po čemu je izračunat — backend ih šalje od koraka 3 plana pravne zaštite.
  tarifa_naziv?: string;
  tarifa_vrijedi_od?: string;
  vrijednost_boda?: number;
  uredbe_pristojbe?: string[];
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
    odvjetnikTelefon: string;
    odvjetnikEmail: string;
    odvjetnikOsobnoIme: string;
    klijentIme: string;
    zastupanaPolozaj: string;
    /** Koga odvjetnik zastupa u kaznenom/prekršajnom postupku; izostavljeno u ostalim. */
    ulogaZastupanja?: string | null;
    protustrankaIme: string;
    protustrankaPolozaj: string;
    predmetSud: string;
    predmetPoslovniBroj: string;
  };
  /** Fallback kad `result` nema VPS podatke (živi pregled prije spremanja). */
  vrijednostSpora?: number;
  vrstaPostupka?: string;
  /**
   * Verzija Tarife razriješena na obrascu (Tbr. 52, po dospijeću). Koristi se samo
   * kad izračun s backenda ne kaže sam po kojoj je računao. Zadano: važeća.
   */
  tarifa?: TarifaVerzija;
  /**
   * Gotova pravna osnova koja ima prednost pred svime — spremljeni zapis je čita
   * iz vlastitih stupaca i ne smije je izvoditi iz današnjih konstanti.
   */
  pravnaOsnova?: PravnaOsnova;
}

/** ISO datum iz zahtjeva/odgovora u `Date`; `null` kad ga nema ili je neispravan. */
function uDatum(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d;
}

/**
 * Sortni ključ za kronološki poredak radnji. Redak bez datuma ide iza svih
 * datiranih; stabilan sort tada zadržava redoslijed unosa (i time učinak
 * strelica „Pomakni gore/dolje", koje su jedini način da se poredaju radnje
 * istog datuma ili radnje bez datuma).
 */
export function datumSortKey(d: string | Date | null | undefined): number {
  if (!d) return Number.POSITIVE_INFINITY;
  const t = d instanceof Date ? d.getTime() : new Date(d).getTime();
  return Number.isNaN(t) ? Number.POSITIVE_INFINITY : t;
}

/** Usporedba za `Array.prototype.sort` — čuva `0` za jednake da sort ostane stabilan. */
export function usporediPoDatumu(
  a: string | Date | null | undefined,
  b: string | Date | null | undefined,
): number {
  const ka = datumSortKey(a);
  const kb = datumSortKey(b);
  return ka === kb ? 0 : ka < kb ? -1 : 1;
}

function neprazno(s: string | null | undefined): string | undefined {
  const t = (s ?? "").trim();
  return t === "" ? undefined : t;
}

/**
 * Naziv ureda zna biti korisnikov mail — Google prijava ga tako postavlja kao
 * privremenu zamjenu dok korisnik ne upiše pravi naziv na `/dobrodosli`
 * (vidi `frontend/lib/onboarding.ts`). Dokument koji ide sudu ne smije
 * ispisati mail umjesto imena odvjetnika, pa se takva vrijednost ovdje
 * tretira kao da ime uopće nije upisano.
 */
function izgledaKaoMail(s: string): boolean {
  return s.includes("@");
}

/**
 * Tarifni broj je zakucan u opis iz kataloga (npr. "Ostali podnesci (Tbr. 8.4)
 * – 25% Tbr. 7.1, max 25"), jer isti opis služi i za prikaz u katalogu i za
 * izvoz. Stupac "Tar. br." ga već prikazuje zasebno, pa bi u nazivu radnje bio
 * dupliciran. Skida se samo prva zagrada koja počinje s "Tbr." — ostali tekst
 * (npr. formula izračuna) ostaje netaknut.
 */
function skiniTarifniBroj(opis: string): string {
  return opis.replace(/\s*\(Tbr\.[^()]*\)/, "").trim();
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

/** Kako se uloga piše u zaglavlju troškovnika: „Zastupanje: punomoćnik oštećenika [ime]". */
const NAZIV_ULOGE_ISPIS: Record<UlogaZastupanja, string> = {
  BRANITELJ: "branitelj",
  PRIVATNI_TUZITELJ: "punomoćnik privatnog tužitelja",
  OSTECENIK_KAO_TUZITELJ: "punomoćnik oštećenika kao tužitelja",
  PUNOMOCNIK_OSTECENIKA: "punomoćnik oštećenika",
};

export function buildIzvozModel(ulaz: IzvozUlaz): TroskovnikIzvozModel {
  const { result, liveCalc, polja } = ulaz;

  // Spremljeni rezultat je izvor istine kad postoji; bez njega se izvozi ono što
  // je na ekranu — isto ponašanje kao i dosad.
  const izvorLinija: LinijaLike[] = result?.stavke ?? liveCalc.lines;

  // Redoslijed dolazi već ispravan — onakav kakav je odvjetnik složio na
  // obrascu. Backend više ne presortira kronološki (vidi `IzracunajRetke`),
  // pa ni ovdje nema razloga da se radnje presortiraju.
  const radnje: IzvozRadnja[] = izvorLinija.map((l) => ({
    opis: skiniTarifniBroj(l.opis),
    tarifniBroj: l.tarifni_broj,
    datum: uDatum(l.datum),
    kolicina: l.kolicina,
    bodovaPoJedinici: l.bodova_po_jedinici,
    // Tbr. 41 se na ekranu pribraja retku, pa mora i ovdje — inače zbroj
    // pojedinačnih bodova ne bi odgovarao ukupnom.
    ukupnoBodova: l.ukupno_bodova + (l.tbr41_bodovi ?? 0),
    iznos: l.iznos_eur,
    napomena: neprazno(l.umanjenje_uloge),
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

  const datumIzrade = uDatum(result?.datum_izrade) ?? new Date();

  // Izračun s backenda kaže sam po čemu je računao. Bez njega (živi pregled) ili uz
  // stariji backend, vrijedi ono po čemu je računao obrazac — danas, po dospijeću.
  const tarifa = ulaz.tarifa ?? TARIFA_TRENUTNA;
  const pravnaOsnova: PravnaOsnova = ulaz.pravnaOsnova ?? {
    zabiljezena: true,
    tarifaNaziv: result?.tarifa_naziv ?? tarifa.naziv,
    tarifaVrijediOd: datumBezVremena(result?.tarifa_vrijedi_od ?? tarifa.vrijediOd),
    vrijednostBoda: result?.vrijednost_boda ?? tarifa.bodValue,
    uredbe: result?.uredbe_pristojbe ?? uredbeIzStavaka(pristojbeneStavke),
    datum: datumIzrade,
  };

  const odvjetnikImeSirovo = neprazno(result?.odvjetnik?.ime ?? polja.odvjetnikIme);
  const odvjetnikIme = odvjetnikImeSirovo && !izgledaKaoMail(odvjetnikImeSirovo) ? odvjetnikImeSirovo : undefined;
  const zastupanaIme = neprazno(result?.klijent?.ime ?? polja.klijentIme);
  const uloga = ULOGE_ZASTUPANJA.find((u) => u.value === (result?.uloga_zastupanja ?? polja.ulogaZastupanja));
  const zastupanje = uloga
    ? `${NAZIV_ULOGE_ISPIS[uloga.value]}${zastupanaIme ? ` ${zastupanaIme}` : ""}`
    : undefined;
  const protustrankaIme = neprazno(result?.protustranka?.ime ?? polja.protustrankaIme);

  const vrijednostSpora = result?.vrijednost_spora ?? ulaz.vrijednostSpora;
  const vrstaPostupkaKljuc = neprazno(result?.vps_vrsta_postupka ?? ulaz.vrstaPostupka);
  const vrstaPostupkaNaziv = vrstaPostupkaKljuc
    ? VRSTA_POSTUPKA_PRAVILA[vrstaPostupkaKljuc as VrstaPostupka]?.naziv
    : undefined;

  return {
    odvjetnik: odvjetnikIme
      ? {
          ime: odvjetnikIme,
          oib: neprazno(result?.odvjetnik?.oib ?? polja.odvjetnikOib),
          adresa: neprazno(result?.odvjetnik?.adresa ?? polja.odvjetnikAdresa),
          grad: neprazno(result?.odvjetnik?.grad ?? polja.odvjetnikGrad),
          iban: neprazno(result?.odvjetnik?.iban ?? polja.odvjetnikIban),
          telefon: neprazno(result?.odvjetnik?.telefon ?? polja.odvjetnikTelefon),
          email: neprazno(result?.odvjetnik?.email ?? polja.odvjetnikEmail),
          osobnoIme: neprazno(polja.odvjetnikOsobnoIme),
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
    vrijednostSpora: vrijednostSpora && vrijednostSpora > 0 ? vrijednostSpora : undefined,
    vrstaPostupkaNaziv,
    zastupanje,
    datumIzrade,

    radnje,
    zbrojNagrade: {
      bodovi: result ? result.ukupno_bodova : liveCalc.ukupnoBodova,
      iznos: result ? result.ukupno_eur_bez_pdv : liveCalc.osnovicaSaNaknadom,
    },
    naknadaTroskova: result?.naknada_troskova_ukupno ?? liveCalc.naknadaTroskovaUkupno,
    pdv: jePdv ? { osnovica, iznos: iznosPdv, ukupno: sPdv, stopa: Math.round(PDV_RATE * 100) } : null,

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
    pravnaOsnova,
  };
}
