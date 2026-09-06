/**
 * Podaci za primjer — jedino mjesto na kojem se mijenja sadržaj demo prikaza.
 *
 * ── Zašto ovo nikad ne ide u bazu ──
 *
 * Odvjetnik ne smije dobiti izmišljenu stranku u svom stvarnom popisu klijenata.
 * Ako mu se „Ivan Horvat" pojavi na troškovniku koji šalje sudu, izgubljen je
 * zauvijek. Zato nema seedanja u Supabase, nema `is_demo` stupca i nema
 * naknadnog čišćenja: postoji samo način rada „prikaz primjera"
 * (`contexts/DemoContext.tsx`), koji ekranima kaže da podatke čitaju odavde
 * umjesto s backenda. Isti ekrani, iste komponente, drugi izvor.
 *
 * ── Zašto su OIB-ovi namjerno neispravni ──
 *
 * Svaki OIB s ISPRAVNOM kontrolnom znamenkom (ISO 7064 MOD 11,10) vrlo vjerojatno
 * pripada nekoj stvarnoj osobi ili tvrtki, a takav broj ne smije stajati u
 * javnom repozitoriju. Brojevi ispod imaju ispravan OBLIK — jedanaest znamenki,
 * pa izgledaju uvjerljivo na ekranu — ali svi padaju na kontrolnoj znamenki.
 * Test `__tests__/demo-podaci.test.ts` to i drži: upiše li netko slučajno valjan
 * OIB, test pukne.
 *
 * Provjeru OIB-a nigdje ne treba isključivati: `validateOib` (`lib/utils.ts`)
 * zovu samo registracija i fiskalizacija, a ni obrazac stranke ni troškovnik
 * ne prolaze kroz nju.
 *
 * ── Zašto ovdje nema nijednog izračunatog iznosa ──
 *
 * Dolje stoje samo ULAZI: vrijednost predmeta, popis radnji s datumima, oznake
 * pristojbi. Bodove, iznose po stavci, PDV i ukupno računa postojeći motor za
 * Tarifu (`lib/tarifa.ts`), isti onaj koji radi za stvarne troškovnike. Demo
 * troškovnik je time i provjera ispravnosti: pokvari li se izračun, vidjet će
 * se na vodiču. Hardkodirani iznosi bi to sakrili.
 */

import type { Case, Client } from "@/lib/stranke-tipovi";

/** Nepomičan datum, da se primjer ne mijenja iz dana u dan. */
const T = (dan: string) => `2026-${dan}T09:00:00Z`;

/**
 * Deset stranaka — mješavina kakva stvarno postoji u praksi: fizičke osobe,
 * dva društva, obrt i udruga. Prve tri imaju predmet, a prva i spremljen
 * troškovnik, jer popis od deset imena bez tog lanca ne pokazuje ništa.
 */
export const DEMO_STRANKE: Client[] = [
  {
    id: "demo-stranka-01", created_at: T("01-15"), updated_at: T("03-04"),
    user_id: "demo", ime_naziv: "Ivan Horvat", tip: "fizicka",
    oib: "12345678902", adresa: "Vukovarska 14", grad: "Osijek", postanski_broj: "31000",
    email: "ivan.horvat@primjer.hr", telefon: "091 234 5678",
    napomena: "Parnica radi isplate — u tijeku.",
  },
  {
    id: "demo-stranka-02", created_at: T("01-22"), updated_at: T("02-11"),
    user_id: "demo", ime_naziv: "Marija Kovačević", tip: "fizicka",
    oib: "23456789017", adresa: "Trg Ante Starčevića 5", grad: "Osijek", postanski_broj: "31000",
    email: "marija.kovacevic@primjer.hr", telefon: "098 111 2233",
    napomena: "Ovrha na temelju vjerodostojne isprave.",
  },
  {
    id: "demo-stranka-03", created_at: T("02-03"), updated_at: T("02-28"),
    user_id: "demo", ime_naziv: "Slavonija gradnja d.o.o.", tip: "pravna",
    oib: "34567890124", adresa: "Industrijska 8", grad: "Đakovo", postanski_broj: "31400",
    email: "uprava@slavonija-gradnja.primjer.hr", telefon: "031 812 400",
    napomena: "Ugovorni spor s podizvođačem.",
  },
  {
    id: "demo-stranka-04", created_at: T("02-14"), updated_at: T("02-14"),
    user_id: "demo", ime_naziv: "Obrt Marković, vl. Petar Marković", tip: "obrt",
    oib: "45678901235", adresa: "Kralja Petra Svačića 22", grad: "Vinkovci", postanski_broj: "32100",
    email: "obrt.markovic@primjer.hr", telefon: "032 331 570",
    napomena: null,
  },
  {
    id: "demo-stranka-05", created_at: T("03-02"), updated_at: T("03-02"),
    user_id: "demo", ime_naziv: "Ana Jurić", tip: "fizicka",
    oib: "56789012346", adresa: "Šetalište kardinala Šepera 3", grad: "Osijek", postanski_broj: "31000",
    email: "ana.juric@primjer.hr", telefon: "095 444 8899",
    napomena: null,
  },
  {
    id: "demo-stranka-06", created_at: T("03-19"), updated_at: T("03-19"),
    user_id: "demo", ime_naziv: "Panon logistika d.o.o.", tip: "pravna",
    oib: "67890123457", adresa: "Svilajska 61", grad: "Osijek", postanski_broj: "31000",
    email: "racunovodstvo@panon-logistika.primjer.hr", telefon: "031 200 155",
    napomena: null,
  },
  {
    id: "demo-stranka-07", created_at: T("04-08"), updated_at: T("04-08"),
    user_id: "demo", ime_naziv: "Tomislav Babić", tip: "fizicka",
    oib: "78901234568", adresa: "Josipa Jurja Strossmayera 40", grad: "Našice", postanski_broj: "31500",
    email: "tomislav.babic@primjer.hr", telefon: "099 777 1212",
    napomena: null,
  },
  {
    id: "demo-stranka-08", created_at: T("04-25"), updated_at: T("04-25"),
    user_id: "demo", ime_naziv: "Katarina Novak", tip: "fizicka",
    oib: "89012345679", adresa: "Ulica hrvatskih branitelja 2", grad: "Belišće", postanski_broj: "31551",
    email: "katarina.novak@primjer.hr", telefon: "091 303 6070",
    napomena: null,
  },
  {
    id: "demo-stranka-09", created_at: T("05-06"), updated_at: T("05-06"),
    user_id: "demo", ime_naziv: "Udruga „Zeleni Osijek”", tip: "udruga",
    oib: "90123456780", adresa: "Ribarska 11", grad: "Osijek", postanski_broj: "31000",
    email: "ured@zeleni-osijek.primjer.hr", telefon: "031 565 002",
    napomena: null,
  },
  {
    id: "demo-stranka-10", created_at: T("05-21"), updated_at: T("05-21"),
    user_id: "demo", ime_naziv: "Josip Barišić", tip: "fizicka",
    oib: "01234567891", adresa: "Bana Josipa Jelačića 17", grad: "Valpovo", postanski_broj: "31550",
    email: "josip.barisic@primjer.hr", telefon: "098 660 4141",
    napomena: null,
  },
];

/** Tri predmeta, na prve tri stranke. Prvi je onaj kojem visi troškovnik. */
export const DEMO_PREDMETI: Case[] = [
  {
    id: "demo-predmet-01", created_at: T("03-04"), client_id: "demo-stranka-01",
    broj_predmeta: "P-412/2026", naziv: "Horvat protiv Panon logistika d.o.o.",
    opis: "Isplata neisplaćene naknade po ugovoru o djelu.",
    vrsta: "parnica", status: "aktivan", vrijednost_spora: 8000,
    sud: "Općinski sud u Osijeku", sudac: "N. Petrović",
    datum_pokretanja: "2026-03-04", datum_zatvaranja: null,
  },
  {
    id: "demo-predmet-02", created_at: T("02-11"), client_id: "demo-stranka-02",
    broj_predmeta: "Ovr-88/2026", naziv: "Kovačević — ovrha radi naplate",
    opis: "Ovrha na temelju vjerodostojne isprave.",
    vrsta: "ovrha", status: "aktivan", vrijednost_spora: 2400,
    sud: "Općinski sud u Osijeku", sudac: null,
    datum_pokretanja: "2026-02-11", datum_zatvaranja: null,
  },
  {
    id: "demo-predmet-03", created_at: T("02-28"), client_id: "demo-stranka-03",
    broj_predmeta: "P-155/2026", naziv: "Slavonija gradnja d.o.o. protiv podizvođača",
    opis: "Naknada štete zbog zakašnjelog izvođenja radova.",
    vrsta: "parnica", status: "mirovanje", vrijednost_spora: 31500,
    sud: "Trgovački sud u Osijeku", sudac: "M. Lukić",
    datum_pokretanja: "2026-02-28", datum_zatvaranja: null,
  },
];

/** Identitet spremljenog demo troškovnika — isti u dosjeu i na troškovniku. */
export const DEMO_TROSKOVNIK_ID = "demo-troskovnik-01";
export const DEMO_TROSKOVNIK_CREATED_AT = T("07-08");

/** Podaci ureda u zaglavlju demo troškovnika — očito izmišljeni, nisu korisnikovi. */
export const DEMO_URED = {
  ime: "Odvjetnički ured — primjer",
  oib: "11223344559",
  adresa: "Županijska 3, 31000 Osijek",
  iban: "HR0000000000000000000",
} as const;

/**
 * Troškovnik za primjer — prosječno težak parnični predmet.
 *
 * SAMO ULAZI. Ni jedan broj bodova, ni jedan iznos, ni PDV. Sve to izvodi
 * `lib/demo/izracun.ts` iz motora za Tarifu.
 *
 * `itemId` vrijednosti su ID-evi iz kataloga Tarife (`Backend/Services/TarifaKatalog.cs`):
 *   par_tuzba                → Tbr. 7.1, sastav tužbe
 *   par_rociste_procesno     → Tbr. 9.2, pripremno ročište (procesna pitanja)
 *   par_rociste_glavno       → Tbr. 9.1, ročište za glavnu raspravu
 *   par_obrazlozeni_podnesak → Tbr. 8.3, obrazloženi podnesak
 */
export const DEMO_TROSKOVNIK = {
  vrijednostSpora: 8000,
  /** Kako se 8.000,00 upisuje u polje forme (hrvatski zapis). */
  vrijednostSporaUnos: "8.000,00",
  opisPredmeta: "Parnični postupak radi isplate",
  sud: "Općinski sud u Osijeku",
  poslovniBroj: "P-412/2026",
  klijentId: "demo-stranka-01",
  protustranka: "Panon logistika d.o.o.",

  radnje: [
    { itemId: "par_tuzba", datum: "2026-03-04" },
    { itemId: "par_rociste_procesno", datum: "2026-04-22" },
    { itemId: "par_rociste_glavno", datum: "2026-05-27" },
    // Dva ročišta za glavnu raspravu, različiti datumi — dva retka, ne količina 2,
    // jer svaki redak nosi svoj datum.
    { itemId: "par_rociste_glavno", datum: "2026-06-24" },
    { itemId: "par_obrazlozeni_podnesak", datum: "2026-07-08" },
  ],

  /**
   * Čl. 7 st. 1 Zakona o sudskim pristojbama — tužba predana elektronički, pa je
   * pristojba na nju (Tar. br. 1 st. 1) upola manja. Alat to primjenjuje sam.
   * Popis ključeva odredaba na koje se odnosi (kao `odlukeElektronicki`).
   */
  podnesciElektronicki: ["tbr1_st1"],

  /**
   * Pristojba na presudu (Tar. br. 2 st. 1 Uredbe). Dodaje se imenovanjem
   * ODREDBE, ne iznosom — iznos i dalje računa motor.
   */
  pristojbeDodane: [{ odredba_id: "tbr2_st1" }],

  predujmovi: [
    { opis: "Predujam za vještačenje", iznos: "300,00", datum: "2026-05-06" },
  ],
} as const;
