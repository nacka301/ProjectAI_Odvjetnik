/**
 * Tekst svih vodiča — jedino mjesto na kojem se mijenjaju riječi koraka.
 *
 * Koraci su PODACI, ne komponente: dodavanje koraka je dodavanje jednog objekta
 * u polje. Motor koji ih prikazuje je `components/Vodic.tsx` i ne zna ništa o
 * sadržaju.
 *
 * ── Zašto tri vodiča, a ne jedan ──
 *
 * Prije je postojao jedan lanac od sedam koraka koji je išao kroz troškovnik pa
 * kroz stranke, na PRAZNIM formama. Objašnjavao je gdje se što nalazi, što je
 * odgovor na pitanje koje nitko nije postavio: odvjetnik koji prvi put otvori
 * alat ne pita „gdje je polje", nego „zašto bih ovo uopće koristio".
 *
 *   A) DOBRODOSLICA_KORACI — prvi dolazak, na PODACIMA ZA PRIMJER (demo način).
 *      Odgovara na „zašto bih ovo koristio". Vrhunac je korak 7: troškovnik se
 *      sprema i može mu se vratiti. To odvjetnici u praksi ne shvate sami, a to
 *      je jedina stvar zbog koje se alat drugi put otvara.
 *
 *   B) TROSKOVNIK_KORACI — na zahtjev, na stvarnim podacima. Detalji forme.
 *   C) STRANKE_KORACI    — na zahtjev, na stvarnim podacima.
 *
 * B i C su nekad bili ulančani i brojali 1/7…7/7. Kao dva neovisna vodiča na
 * zahtjev brojevi su vraćeni na 1/4 odnosno 1/3 — komentar koji je to predviđao
 * stajao je uz staru numeraciju od početka.
 */

export interface VodicKorak {
  /**
   * Element koji korak ističe. Traži se po `data-vodic="…"`, a kao rezerva po
   * DOM `id`-u iste vrijednosti — atribut, ne CSS klasa i ne položaj u DOM-u,
   * jer se klase mijenjaju pri svakom redizajnu i vodič tiho pukne.
   *
   * Izostavljen na koracima koji ništa ne ističu (uvodna i završna kartica);
   * kartica tada stoji na sredini ekrana.
   */
  target?: string;
  title: string;
  text: string;
  /**
   * Ruta na kojoj korak živi. Motor na nju navigira prije nego potraži cilj.
   * Izostavljena znači „ostani gdje jesi".
   */
  ruta?: string;
}

// ══════════════════════════════════════════════════════════════════
//  A) Vodič dobrodošlice — na podacima za primjer
// ══════════════════════════════════════════════════════════════════

/**
 * Osam koraka i završna kartica. Brojevi u naslovima idu 1/8…8/8; završna
 * kartica nije korak nego rastanak, pa broja nema.
 *
 * Koraci 1–7 stoje na troškovniku, korak 8 na popisu stranaka. Prijelaz radi
 * motor preko `ruta`, pa je to običan korak, a ne posebna „predaja štafete".
 */
export const DOBRODOSLICA_KORACI: VodicKorak[] = [
  {
    ruta: "/troskovnik",
    title: "1/8 · Ovo je primjer",
    text:
      "Ovo je gotov troškovnik za parnicu vrijednosti 8.000,00 €. " +
      "Podaci su primjer i ništa od ovoga nije spremljeno u vaš račun. " +
      "Za dvije minute pokazat ću vam kako je složen i što alat radi umjesto vas.",
  },
  {
    target: "vodic-vps",
    title: "2/8 · Vrijednost predmeta",
    text:
      "Vrijednost predmeta određuje tarifni broj, a tarifni broj određuje koliko " +
      "bodova nosi svaka radnja. Promijenite ovaj iznos i cijeli se troškovnik " +
      "preračuna sam.",
  },
  {
    target: "vodic-popis-radnji",
    title: "3/8 · Popis radnji",
    text:
      "Svaka obavljena radnja je jedan redak — upisujete ih redom kojim ste ih " +
      "obavljali. Uz svaku ostaje datum, pa se poslije vidi kad je što učinjeno.",
  },
  {
    target: "vodic-bodovi",
    title: "4/8 · Bodovi i iznos",
    text:
      "Bod je obračunska jedinica Tarife i iznosi 2,00 €. Ovdje vidite koliko je " +
      "bodova Tarifa predviđa za tu radnju i koliko to iznosi u eurima.",
  },
  {
    target: "vodic-pristojbe",
    title: "5/8 · Sudske pristojbe",
    text:
      "Pristojba nije vaša nagrada — to je trošak koji plaćate sudu i prenosite " +
      "stranci. Zato stoji odvojeno i na nju se ne obračunava PDV (čl. 33 Zakona o " +
      "PDV-u). Za podneske predane elektronički pristojba je upola manja (čl. 7 " +
      "Zakona o sudskim pristojbama) i alat to primjenjuje sam. Iznosi su po Uredbi " +
      "koja vrijedi od 1. lipnja 2026.",
  },
  {
    target: "vodic-ukupno",
    title: "6/8 · Ukupno",
    text:
      "Nagrada, pristojbe i predujmovi zbrajaju se odvojeno, pa je na kraju jasno što " +
      "je vaše, a što ste platili umjesto stranke.",
  },
  {
    target: "vodic-spremi",
    title: "7/8 · Spremanje",
    text:
      "Troškovnik se sprema u vaš račun. Ne morate ga dovršiti odjednom — vratite mu " +
      "se kad obavite iduću radnju, dodajte je i ponovno spremite. Iz popisa " +
      "spremljenih otvarate ga kad god zatreba, ispisujete ga ili izvozite u Excel.",
  },
  {
    target: "vodic-dosje",
    ruta: "/klijenti",
    title: "8/8 · Stranke",
    text:
      "Stranku upišete jednom. Kod idućeg troškovnika je samo odaberete i podaci se " +
      "povuku sami. Ovdje ih je deset kao primjer — kod vas će popis biti prazan dok " +
      "ne upišete prvu.",
  },
  {
    title: "Gotovi ste",
    text:
      "To je sve. Zatvaram primjere i otvaram vam prazan troškovnik — podaci vašeg " +
      "ureda su već upisani. " +
      "Vodič možete pokrenuti ponovno bilo kad iz izbornika.",
  },
];

// ══════════════════════════════════════════════════════════════════
//  B) Vodič za troškovnik — na zahtjev, na stvarnim podacima
// ══════════════════════════════════════════════════════════════════

export const TROSKOVNIK_KORACI: VodicKorak[] = [
  {
    target: "vodic-vps",
    title: "1/4 · Vrijednost predmeta spora",
    text:
      "Upišite vrijednost spora u eurima. Iz nje se izvodi osnovica u bodovima — " +
      "sve nagrade u nastavku računaju se iz tog broja. U kaznenom predmetu ovo " +
      "polje ne postoji; tamo se polazi od zaprijećene kazne.",
  },
  {
    target: "vodic-radnje",
    title: "2/4 · Odaberite radnje",
    text:
      "Pretražite tarifu i dodajte radnje koje ste obavili — tužba, ročište, žalba. " +
      "Ista se stavka smije dodati više puta, svaka sa svojim datumom. Bodovi se " +
      "zbrajaju sami, ne morate ih tražiti u Tarifi.",
  },
  {
    target: "vodic-stranke",
    title: "3/4 · Stranke i PDV",
    text:
      "Odjeljak je neobavezan i služi samo zaglavlju ispisa. Vaši podaci — naziv " +
      "ureda, OIB, adresa, IBAN — povlače se iz Postavki → Odvjetnički ured; ondje " +
      "ih upišete jednom, a ovdje ih mijenjate samo za ovaj troškovnik. Sud i " +
      "stranke dodajte ako troškovnik ide sudu. Prekidač „U sustavu PDV-a\" desno " +
      "određuje obračunava li se PDV na nagradu.",
  },
  {
    target: "vodic-rezultat",
    title: "4/4 · Rezultat",
    text:
      "Iznos se osvježava dok unosite. Kad ste gotovi, troškovnik se sprema uz " +
      "stranku i predmet; „Ispis\" daje dokument za sud, „Excel\" tablicu za daljnji " +
      "rad. Taj isti troškovnik poslije nađete u dosjeu stranke i u Povijesti.",
  },
];

// ══════════════════════════════════════════════════════════════════
//  C) Vodič za stranke — na zahtjev, na stvarnim podacima
// ══════════════════════════════════════════════════════════════════

export const STRANKE_KORACI: VodicKorak[] = [
  {
    target: "vodic-nova-stranka",
    title: "1/3 · Nova stranka",
    text:
      "Ovim gumbom otvarate obrazac za novu stranku. Obavezno je samo ime " +
      "odnosno naziv; OIB, adresa i kontakt idu u zaglavlje troškovnika i na " +
      "dopise, pa ih je vrijedno upisati odmah. Stranku ne morate otvarati " +
      "ručno — ako je upišete u troškovnik, otvorit će se sama pri spremanju.",
  },
  {
    target: "vodic-pretraga",
    // Nekad je ovdje pisalo „po imenu, OIB-u i adresi". Filtar gleda ime, OIB i
    // e-mail; adresu nikad nije gledao.
    title: "2/3 · Pretraga",
    text:
      "Pretražuje po imenu, OIB-u i e-mailu. Kad se popis stranaka nakupi, ovo " +
      "je brži put do predmeta nego listanje.",
  },
  {
    target: "vodic-dosje",
    title: "3/3 · Dosje stranke",
    text:
      "Klik na stranku otvara njezin dosje — predmeti i svi troškovnici " +
      "spremljeni uz nju, sa zbrojem. Ovime je vodič gotov; otvorite ga opet " +
      "stavkom „Vodič\" u zaglavlju.",
  },
];
