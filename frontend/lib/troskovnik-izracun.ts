// Živi izračun troškovnika — čista funkcija, izlučena iz `app/troskovnik/page.tsx`
// (`useMemo liveCalc`). Autoritativni izračun i dalje radi backend
// (`POST /api/troskovnik/izracunaj`, `TroskovnikService.Izracunaj`); ovo je živi
// pregled u sučelju i mora dati isti rezultat.
//
// Izlučeno da bi se agregacija (petlja po recima → Tbr. 40 → Tbr. 41 → PDV →
// pristojbe → sveukupno) mogla testirati bez Reacta. Sve teške tarifne odluke
// već su čiste funkcije u `./tarifa`; ovdje je samo njihovo spajanje.

import type { NaknadaStavkaZaIzracun, OsnovicaRezultat, TarifnaStavka, Tbr41Stavka, UlogaZastupanja } from "./tarifa";
import {
  PDV_RATE,
  ULOGA_OSTECENIK_STOPA,
  ZASTUPNIK_DJETETA_STOPA,
  napomenaUmanjenjaUloge,
  opisZaUlogu,
  pravilnikFaktor,
  pravilnikPrimjenjiv,
  KATEGORIJA_KAZNENI,
  umanjujeUloga,
  izracunajBodove,
  izracunajKazneneBodove,
  izracunajNaknaduStavku,
  podijeliPoOsobama,
  povisenjeViseOsobaPostotak,
  razrijesiTarifu,
  razrijesiTbr41,
  tbr41Izvor,
  tbr41Udio,
  tbr71Bodovi,
  ukupnoBodovaZaStavku,
  zaokruziNovac,
  zbrojiRucneTroskove,
} from "./tarifa";

/** Jedan redak troškovnika, poredan kronološki, s već isparsiranom količinom. */
export interface RedakIzracunaUlaz {
  uid: string;
  itemId: string;
  kolicina: number;
  datum: string | null;
  tbr41: Tbr41Stavka | null;
}

/** Ručno unesen trošak (dodatna pristojba / predujam) — iznos VEĆ isparsiran u broj. */
export interface TrosakIzracunaUlaz {
  opis: string;
  iznos: number;
}

/** Sve što petlja i zbrojevi trebaju — parsiranje stringova ostaje u komponenti. */
export interface TroskovnikIzracunUlaz {
  /** Kronološki poredani retci (page.tsx: `redciPoredani`). */
  redciPoredani: RedakIzracunaUlaz[];
  katalog: TarifnaStavka[];

  /** Rezultat `izracunajOsnovicu(...)` — Tbr. 7. toč. 1/4/7/8 ili Tbr. 45. */
  osnovica: OsnovicaRezultat | null;

  /** `true` kad je odvjetnik u sustavu PDV-a. */
  isPdvUkljucen: boolean;

  // Kazneni parametri
  kazneniTierBodovi: number;
  isZastupnikDjeteta: boolean;
  isPoSluzbenojDuznosti: boolean;
  isNaTeretProracuna: boolean;
  /** Kvalifikator na isNaTeretProracuna: izabrani (ne postavljeni) branitelj,
   *  trošak na proračun zbog oslobađajuće presude → puna Tarifa, bez umanjenja. */
  isIzabraniOslobodjajuca: boolean;
  /** Koga odvjetnik zastupa; null/izostavljeno = puna nagrada (stari pozivi). */
  ulogaZastupanja?: UlogaZastupanja | null;
  zaprijecenaKazna: string | null;

  // Prekršaj s oduzimanjem predmeta (Tbr. 7.1 po vrijednosti oduzetog)
  isOduzimanjePrekrsaj: boolean;
  parsedVrijednostOduzimanja: number;

  /**
   * Prekršajni postupak: `kazneniTierBodovi` je već razriješen (100 / Tbr. 7 t. 1 / kazna
   * za odgovornu osobu — vidi `kazneniRazredBodovi`), a dugotrajnog zatvora nema.
   */
  isPrekrsajniPostupak?: boolean;

  // Tbr. 40 / Tbr. 41 (prečac „primijeni na sve")
  brojZastupanihOsoba: number;
  tbr41Povisenje: number;
  tbr41Snizenje: number;

  /** Sudske pristojbe — već izračunato (`obracunajPristojbe`). */
  pristojbe: { ukupno: number; vpsUdio: number; naTeretProtustranke: number };

  /** Ručno dodane pristojbe / predujmovi (iznosi isparsirani). */
  dodatnePristojbe: TrosakIzracunaUlaz[];
  predujmovi: TrosakIzracunaUlaz[];

  /**
   * Naknada troškova (Tbr. 48.-51.) — iznosi/količine isparsirani; ULAZI u PDV osnovicu.
   * Vidi `NaknadaStavkaZaIzracun` — "rucno" koristi `iznos`, "izracun" izvodi iznos iz
   * `kolicina × jedinicniIznos` (+ `cestarina` za Prijevoz).
   */
  naknadaTroskovaStavke: NaknadaStavkaZaIzracun[];

  /** Tbr. 52 — mjerodavna verzija Tarife po dospijeću potraživanja. */
  datumDospijeca: string | null;
}

export interface TroskovnikIzracunRedak {
  uid: string;
  opis: string;
  tarifni_broj: string;
  kolicina: number;
  datum: string | null;
  bodova_po_jedinici: number;
  ukupno_bodova: number;
  iznos_eur: number;
  jedinica: string;
  is_jednokratna: boolean;
  pravni_izvor: string;
  item_id: string;
  tbr41_povisenje: number;
  tbr41_snizenje: number;
  tbr41_bodovi: number;
  tbr41_pravni_izvor: string;
  /** „umanjeno 50% (Tbr. 4 t. 8 Tarife)" kad je nagrada umanjena ulogom; inače prazno. */
  umanjenje_uloge: string;
}

/**
 * Napomena uz redak za ispis: umanjenje po ulozi (Tarifa) i/ili po Pravilniku NN 22/22.
 * Pravilnik se bilježi samo na retcima na koje je stvarno primijenjen. Zrcali
 * `TroskovnikService.NapomenaUmanjenja`.
 */
function napomenaUmanjenja(
  def: TarifnaStavka,
  u: TroskovnikIzracunUlaz,
  umanjenoUlogom: boolean,
  imaKaznene: boolean,
): string {
  const dijelovi: string[] = [];
  if (umanjenoUlogom) dijelovi.push(napomenaUmanjenjaUloge(def.tarifni_broj));
  if (pravilnikPrimjenjiv(def, imaKaznene)) {
    const faktor = pravilnikFaktor(u);
    if (faktor === ZASTUPNIK_DJETETA_STOPA) dijelovi.push("70% nagrade (Pravilnik NN 22/22, čl. 2)");
    else if (faktor !== 1) dijelovi.push("umanjeno 50% (Pravilnik NN 22/22, čl. 1)");
  }
  return dijelovi.join("; ");
}

export type TroskovnikIzracunRezultat = ReturnType<typeof izracunajTroskovnik>;

/**
 * Doslovan port tijela `liveCalc` — isti redoslijed, iste formule, isti oblik
 * izlaza. Bodovi ostaju decimalni kroz cijeli lanac; zaokružuje se samo novac,
 * jednom (`zaokruziNovac`). Čl. 13 (na teret protustranke) NE ulazi u zbroj.
 */
export function izracunajTroskovnik(u: TroskovnikIzracunUlaz) {
  const lines: TroskovnikIzracunRedak[] = [];
  const catalogMap = new Map(u.katalog.map((s) => [s.id, s]));

  // Tbr. 52 — vrijednost boda mjerodavne verzije Tarife (po dospijeću). Danas
  // postoji jedna verzija, pa je jednaka BOD_VALUE; backend razrješava istu logiku.
  const bodValue = razrijesiTarifu(u.datumDospijeca || null).bodValue;

  // Pravilnik NN 22/22 pokriva i „Razno" (Tbr. 36...) kad troškovnik ima kaznenu stavku.
  const imaKaznene = u.redciPoredani.some(
    (r) => catalogMap.get(r.itemId)?.kategorija === KATEGORIJA_KAZNENI,
  );

  for (const redak of u.redciPoredani) {
    const def = catalogMap.get(redak.itemId);
    if (!def || redak.kolicina <= 0) continue;

    let bodovi: number;
    if (def.prekrsaj_oduzimanje) {
      // Prekršaj s oduzimanjem — Tbr. 7.1 po vrijednosti oduzetog predmeta
      if (u.isOduzimanjePrekrsaj && u.parsedVrijednostOduzimanja > 0) {
        bodovi = tbr71Bodovi(u.parsedVrijednostOduzimanja);
      } else {
        bodovi = 100; // fallback: standard prekršaj 100 bodova
      }
      bodovi *= pravilnikFaktor(u);
    } else if (def.kazneni_base) {
      bodovi = izracunajKazneneBodove(
        def,
        u.kazneniTierBodovi,
        u.isZastupnikDjeteta,
        u.isPoSluzbenojDuznosti,
        u.isNaTeretProracuna,
        // U prekršajnom postupku dugotrajnog zatvora nema (kazna ne mijenja povišenja).
        u.isPrekrsajniPostupak ? undefined : (u.zaprijecenaKazna ?? undefined),
        u.isIzabraniOslobodjajuca,
      );
    } else if (u.osnovica) {
      bodovi = izracunajBodove(def, u.osnovica);
      // Pravilnik vrijedi na SVE radnje kaznenog postupka, i fiksne (dodatni sat,
      // pritvor, razmatranje spisa Tbr. 36...).
      if (pravilnikPrimjenjiv(def, imaKaznene)) bodovi *= pravilnikFaktor(u);
    } else {
      // Osnovica nije razriješena (Tbr. 45 bez razine suda ili načina).
      continue;
    }

    // Umanjenje po ulozi zastupanja (Tbr. 3 t. 3 / Tbr. 4 t. 8) — nakon Pravilnika, na nagradu retka.
    const umanjenoUlogom = umanjujeUloga(u.ulogaZastupanja, def);
    if (umanjenoUlogom) bodovi *= ULOGA_OSTECENIK_STOPA;

    // Redak s 0 bodova izgleda kao valjan iznos, a nije — ne prikazuje se.
    if (bodovi <= 0) continue;

    const ukupnoStavke = ukupnoBodovaZaStavku(def, bodovi, redak.kolicina);

    // Tbr. 41 po pojedinom retku; bez vlastitog postotka vrijedi prečac.
    const { povisenje, snizenje } = razrijesiTbr41(
      redak.tbr41 ?? {},
      u.tbr41Povisenje,
      u.tbr41Snizenje,
    );
    const tbr41BodoviStavke = ukupnoStavke * tbr41Udio(povisenje, snizenje);

    lines.push({
      uid: redak.uid,
      opis: opisZaUlogu(def.opis, u.ulogaZastupanja),
      tarifni_broj: def.tarifni_broj,
      kolicina: redak.kolicina,
      datum: redak.datum,
      bodova_po_jedinici: bodovi,
      ukupno_bodova: ukupnoStavke,
      iznos_eur: zaokruziNovac((ukupnoStavke + tbr41BodoviStavke) * bodValue),
      jedinica: def.jedinica,
      is_jednokratna: def.jednokratna_nagrada === true,
      pravni_izvor: def.pravni_izvor ?? def.tarifni_broj,
      item_id: redak.itemId,
      tbr41_povisenje: povisenje,
      tbr41_snizenje: snizenje,
      tbr41_bodovi: tbr41BodoviStavke,
      tbr41_pravni_izvor: tbr41Izvor(povisenje, snizenje),
      umanjenje_uloge: napomenaUmanjenja(def, u, umanjenoUlogom, imaKaznene),
    });
  }

  const osnovicaBodova = lines.reduce((s, l) => s + l.ukupno_bodova, 0);

  // Tbr. 40. toč. 1 — povišenje za drugu i svaku sljedeću zastupanu osobu.
  const povisenjePostotak = povisenjeViseOsobaPostotak(u.brojZastupanihOsoba);
  const povisenjeBodovi = osnovicaBodova * povisenjePostotak;

  // Tbr. 41 — zbroj prilagodbi po stavkama, aditivan na ukupan iznos.
  const tbr41Bodovi = lines.reduce((sum, l) => sum + l.tbr41_bodovi, 0);
  const tbr41Postotak = osnovicaBodova === 0 ? 0 : tbr41Bodovi / osnovicaBodova;

  const ukupnoBodova = osnovicaBodova + povisenjeBodovi + tbr41Bodovi;

  // Bodovi su do ovdje ostali decimalni; tek se gotov iznos zaokružuje, jednom.
  const ukupnoEur = zaokruziNovac(ukupnoBodova * bodValue);

  // Naknada troškova (Tbr. 48.-51.) — vlastiti trošak odvjetnika, ULAZI u osnovicu.
  // Svaka stavka je ili ručno upisana ili izračunata (Tbr. 50/51) — vidi izracunajNaknaduStavku.
  const naknadaTroskovaUkupno = zaokruziNovac(
    u.naknadaTroskovaStavke.reduce((s, r) => s + izracunajNaknaduStavku(r), 0),
  );
  const osnovicaSaNaknadom = ukupnoEur + naknadaTroskovaUkupno;

  const pdv = u.isPdvUkljucen ? zaokruziNovac(osnovicaSaNaknadom * PDV_RATE) : 0;
  const odvjetnickiTrosak = osnovicaSaNaknadom + pdv;

  // Sudske pristojbe se računaju izvan ove funkcije (`obracunajPristojbe`) jer
  // ovise o drugom propisu i ne ulaze u osnovicu za PDV.
  const sudskaPristojba = u.pristojbe.ukupno;
  const ukupnoPristojbe =
    sudskaPristojba +
    zbrojiRucneTroskove(
      u.dodatnePristojbe.map((t) => ({ opis: t.opis, iznos: t.iznos })),
    );

  // Predujmovi — vlastiti odjeljak, bez PDV-a, kao i pristojbe.
  const ukupnoPredujmovi = zbrojiRucneTroskove(
    u.predujmovi.map((t) => ({ opis: t.opis, iznos: t.iznos })),
  );

  // Čl. 13 ne ulazi u zbroj: tereti protivnu stranku, po pravomoćnosti.
  const sveukupniTrosak = odvjetnickiTrosak + ukupnoPristojbe + ukupnoPredujmovi;

  // Tbr. 40. toč. 1, drugi odlomak — dioba nagrade na jednake dijelove po osobi.
  const bodoviPoOsobi = podijeliPoOsobama(ukupnoBodova, u.brojZastupanihOsoba);
  const nagradaPoOsobi = zaokruziNovac(
    podijeliPoOsobama(ukupnoEur, u.brojZastupanihOsoba),
  );

  return {
    lines,
    osnovicaBodova,
    povisenjePostotak,
    povisenjeBodovi,
    tbr41Postotak,
    tbr41Bodovi,
    ukupnoBodova,
    bodoviPoOsobi,
    nagradaPoOsobi,
    ukupnoEur,
    // Osnovica za PDV = nagrada + naknada troškova (čl. 33. Zakona o PDV-u).
    naknadaTroskovaUkupno,
    osnovicaSaNaknadom,
    pdv,
    ukupnoSPdv: osnovicaSaNaknadom + pdv,
    sudskaPristojba,
    ukupnoPristojbe,
    ukupnoPredujmovi,
    sveukupniTrosak,
    pristojbaVpsUdio: u.pristojbe.vpsUdio,
    pristojbaNaTeretProtustranke: u.pristojbe.naTeretProtustranke,
  };
}
