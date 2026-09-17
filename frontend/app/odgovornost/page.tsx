import type { Metadata } from "next";
import Link from "next/link";
import {
  PRAVNI_TEKSTOVI_VERZIJA_TEKST,
  PRUZATELJ_EMAIL,
  PRUZATELJ_IME,
} from "@/lib/pravno";

export const metadata: Metadata = {
  title: "Izjava o odgovornosti — PravoTek",
  description: "Što PravoTek jest, što nije, i tko odgovara za izračun troškovnika.",
};

/**
 * Izjava o odgovornosti — jedini izvor teksta o odgovornosti za izračun.
 *
 * Uvjeti korištenja (§5) upućuju ovamo kao na svoj sastavni dio, pa se tekst ne
 * ponavlja na dva mjesta i ne može se razići. Plan: docs/plan-pravna-zastita.md,
 * dio B. Pisano u prvom licu jer uslugu pruža jedna fizička osoba; je li to
 * pravno bolje od trećeg lica, pitanje je za odvjetnički pregled.
 *
 * Tekst smije tvrditi samo ono što aplikacija danas stvarno radi: potvrda pri
 * ulasku (dio C) i prijava greške uz iznos (dio D) postoje.
 */
export default function OdgovornostPage() {
  const email = <a href={`mailto:${PRUZATELJ_EMAIL}`}>{PRUZATELJ_EMAIL}</a>;

  return (
    <div className="min-h-screen bg-white">
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-12 sm:py-16">
        {/* Header */}
        <div className="mb-10">
          <Link href="/" className="text-xs text-slate-400 hover:text-slate-600 transition print:hidden">
            ← Natrag na PravoTek
          </Link>
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-4xl font-bold text-slate-900 mt-4">
            Izjava o odgovornosti
          </h1>
          <p className="text-sm text-slate-400 mt-2">Verzija od {PRAVNI_TEKSTOVI_VERZIJA_TEKST}</p>
        </div>

        {/* Content */}
        <article className="prose prose-slate prose-sm sm:prose-base max-w-none
          prose-headings:font-[family-name:var(--font-playfair)]
          prose-h2:text-xl prose-h2:mt-10 prose-h2:mb-4
          prose-p:leading-relaxed prose-p:text-slate-600
          prose-strong:text-slate-800
        ">
          <p>
            Ova izjava sastavni je dio <Link href="/uvjeti">Uvjeta korištenja</Link>. Kratka je
            namjerno: odvjetnik treba znati na čemu je prije nego iznos iz alata stavi u podnesak.
          </p>

          <h2>Što je PravoTek</h2>
          <p>
            PravoTek je pomoćni alat za izračun odvjetničkog troškovnika po Tarifi o nagradama i
            naknadi troškova za rad odvjetnika i Uredbi o Tarifi sudskih pristojbi. Namijenjen je
            odvjetnicima i odvjetničkim uredima za obavljanje njihove djelatnosti.
          </p>

          <h2>Što PravoTek nije</h2>
          <p>
            PravoTek nije pravni savjet. Ne zamjenjuje prosudbu odvjetnika o tome koje radnje i
            troškove zatražiti i kako ih opravdati.
          </p>

          <h2>Vaša provjera</h2>
          <p>
            Prije nego troškovnik upotrijebite u postupku, provjerite ga.{" "}
            <strong>Za sadržaj podneska odgovara odvjetnik koji ga podnosi.</strong> Rezultat ovisi
            o podacima koje unesete: vrijednosti predmeta, radnjama, datumima i oslobođenjima.
          </p>

          <h2>Propisi i njihove izmjene</h2>
          <p>
            Propise nastojim unijeti čim stupe na snagu, ali ne mogu jamčiti da alat u svakom
            trenutku prati posljednju izmjenu. Uz svaki novi spremljeni troškovnik zapisano je po
            kojoj je verziji Tarife i Uredbe izračunat i kojeg dana.
          </p>

          <h2>U kojoj je fazi alat</h2>
          <p>
            PravoTek je u ranoj fazi razvoja. Razvija ga jedna osoba. Izračuni su provjereni na
            primjerima iz prakse, ali nisu neovisno stručno revidirani. Tijekom probnog razdoblja
            alat se koristi bez naknade.
          </p>

          <h2>Odgovornost</h2>
          <p>
            Ne odgovaram za štetu nastalu zbog pogreške u izračunu ako je pogreška posljedica obične
            nepažnje. To ne vrijedi za štetu prouzročenu namjerno ili krajnjom nepažnjom: za nju
            odgovaram prema zakonu (članak 345. Zakona o obveznim odnosima). Ne odgovaram za
            pogrešku koja proizlazi iz netočno unesenih podataka.
          </p>

          <h2>Kad nađete grešku</h2>
          <p>
            Uz iznos svakog troškovnika stoji poveznica „Iznos mi ne izgleda točno“. Prijava stiže
            izravno meni, zajedno s unesenim stavkama i izračunom — bez imena i podataka stranaka.
            Možete pisati i na {email}. Svaku prijavu pregledam, a potvrđenu grešku ispravim i
            javim vam se.
          </p>

          <h2>Tko pruža uslugu</h2>
          <p>
            {PRUZATELJ_IME}, fizička osoba. E-pošta: {email}.
          </p>

          <h2>Izmjene</h2>
          <p>
            Kad se ova izjava ili Uvjeti korištenja promijene, na vrhu stranice mijenja se datum
            verzije, a pri sljedećem ulasku u aplikaciju tražit ću da ih ponovno potvrdite. O
            izmjenama koje za vas nešto mijenjaju obavještavam i e-poštom, prije nego stupe na snagu.
          </p>
        </article>
      </div>
    </div>
  );
}
