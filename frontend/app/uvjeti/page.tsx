import type { Metadata } from "next";
import { PRAVNI_TEKSTOVI_VERZIJA_TEKST } from "@/lib/pravno";

export const metadata: Metadata = {
  title: "Uvjeti korištenja — PravoTek",
  description: "Uvjeti korištenja usluge PravoTek — pravna inteligencija za odvjetnike",
};

export default function UvjetiPage() {
  return (
    <div className="min-h-screen bg-white">
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-12 sm:py-16">
        {/* Header */}
        <div className="mb-10">
          <a href="/" className="text-xs text-slate-400 hover:text-slate-600 transition print:hidden">
            ← Natrag na PravoTek
          </a>
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-4xl font-bold text-slate-900 mt-4">
            Uvjeti korištenja
          </h1>
          <p className="text-sm text-slate-400 mt-2">Verzija od {PRAVNI_TEKSTOVI_VERZIJA_TEKST}</p>
        </div>

        {/* Content */}
        <article className="prose prose-slate prose-sm sm:prose-base max-w-none
          prose-headings:font-[family-name:var(--font-playfair)]
          prose-h2:text-xl prose-h2:mt-10 prose-h2:mb-4
          prose-h3:text-base prose-h3:mt-6 prose-h3:mb-2
          prose-p:leading-relaxed prose-p:text-slate-600
          prose-li:text-slate-600
          prose-strong:text-slate-800
          prose-table:text-sm
        ">

          {/* §1 */}
          <h2>1. Opće odredbe</h2>
          <p>
            1.1. Ovi Uvjeti korištenja (u daljnjem tekstu: „Uvjeti") uređuju prava i obveze
            između <strong>Davida Vacke</strong>, fizičke osobe koja pruža uslugu PravoTek
            (u daljnjem tekstu: „Pružatelj usluge" ili „PravoTek"), kontakt:{" "}
            <a href="mailto:vackadavid@gmail.com">vackadavid@gmail.com</a>, i pravne ili fizičke
            osobe koja koristi aplikaciju PravoTek (u daljnjem tekstu: „Korisnik").
          </p>
          <p>
            1.2. PravoTek je aplikacija koja se koristi putem interneta. Namijenjena je odvjetnicima i
            odvjetničkim uredima u Republici Hrvatskoj za obavljanje odvjetničke djelatnosti. Omogućuje
            izračun odvjetničke nagrade prema Tarifi o nagradama i naknadi troškova za rad odvjetnika
            (Hrvatska odvjetnička komora, 14. listopada 2023.), izračun sudskih pristojbi prema Uredbi o
            Tarifi sudskih pristojbi (NN 50/2026) te vođenje evidencije stranaka i predmeta.
          </p>
          <p>
            1.3. Korisnik prihvaća ove Uvjete i <a href="/odgovornost">Izjavu o odgovornosti</a>{" "}
            potvrdom pri prvom ulasku u aplikaciju. Potvrda se bilježi s datumom i verzijom teksta.
          </p>

          {/* §2 */}
          <h2>2. Usluga i opseg</h2>
          <p>2.1. Korisnicima su trenutno dostupna dva modula:</p>
          <ul>
            <li><strong>Odvjetnički troškovnik</strong> — izračun nagrade prema Tarifi i sudskih pristojbi prema Uredbi o Tarifi sudskih pristojbi</li>
            <li><strong>Stranke i predmeti</strong> — evidencija stranaka i predmeta s kronologijom događaja</li>
          </ul>
          <p>
            2.2. Ostali moduli koji se u aplikaciji i na naslovnoj stranici označavaju kao „u razvoju“
            Korisnicima nisu dostupni. Kad budu pušteni u rad, na njih se primjenjuju ovi Uvjeti.
          </p>
          <p>
            2.3. Pružatelj usluge zadržava pravo dodavanja, izmjene ili ukidanja pojedinih modula
            uz prethodnu obavijest Korisniku elektroničkim putem, s primjerenim prijelaznim rokom
            od najmanje <strong>30 (trideset) dana</strong>.
          </p>

          {/* §3 */}
          <h2>3. Registracija i korisnički račun</h2>
          <p>
            3.1. Za korištenje aplikacije potrebna je registracija putem valjane adrese elektroničke
            pošte i postavljanje zaporke. Korisnik jamči da su podaci navedeni pri registraciji
            točni i potpuni.
          </p>
          <p>
            3.2. Korisnik je odgovoran za čuvanje tajnosti svojih pristupnih podataka. Svaka radnja
            izvršena putem korisničkog računa smatra se radnjom Korisnika.
          </p>
          <p>
            3.3. Korisnik je dužan bez odgode obavijestiti Pružatelja usluge o svakom neovlaštenom
            korištenju svog računa.
          </p>
          <p>
            3.4. PravoTek omogućuje dvofaktorsku autentifikaciju (2FA) prema TOTP standardu (RFC 6238).
            Korisnicima se preporučuje aktiviranje 2FA radi dodatne zaštite korisničkog računa.
          </p>

          {/* §4 */}
          <h2>4. Naknada</h2>
          <p>
            4.1. Usluga se koristi <strong>bez naknade</strong>. Aplikacija ne traži i ne pohranjuje
            podatke o karticama ni druge podatke o plaćanju.
          </p>

          {/* §5 — tekst o odgovornosti živi samo u Izjavi, da se dva teksta ne raziđu. */}
          <h2>5. Odgovornost</h2>
          <p>
            5.1. Odgovornost Pružatelja usluge, obveza Korisnika da provjeri izračun te način
            prijave greške uređeni su <a href="/odgovornost">Izjavom o odgovornosti</a>, koja je
            sastavni dio ovih Uvjeta.
          </p>
          <p>
            5.2. Pružatelj usluge ne pruža pravne savjete, pravno zastupanje niti bilo koji oblik
            pravne pomoći u smislu Zakona o odvjetništvu. Aplikacija ne zamjenjuje prosudbu
            odvjetnika.
          </p>

          {/* §6 */}
          <h2>6. Korištenje tehnologije za analizu dokumenata</h2>
          <p>
            6.1. Određeni moduli aplikacije (analiza ugovora, ZK due diligence, procjena nekretnina,
            kronologija spisa, pravna strategija, sudski tumač) oslanjaju se na umjetnu inteligenciju.
            Kada Korisnik koristi te module, uneseni tekst šalje se prema uslugama obrade prirodnog
            jezika putem šifriranog API sučelja.
          </p>
          <p>6.2. Kada se ti moduli koriste, podaci koji se tako obrađuju:</p>
          <ul>
            <li><strong>ne koriste se za treniranje</strong> javnih modela,</li>
            <li><strong>ne pohranjuju se trajno</strong> na poslužiteljima pružatelja API usluge,</li>
            <li>prenose se isključivo putem šifriranog kanala (TLS 1.2/1.3),</li>
            <li>obrađuju se u skladu s poslovnim (B2B/Enterprise) uvjetima pružatelja API usluge koji eksplicitno zabranjuju korištenje korisničkih podataka u svrhe treniranja.</li>
          </ul>
          <p>
            6.3. Pružatelj usluge primjenjuje tehničke mjere zaštite privatnosti pri komunikaciji
            s API-jem, uključujući zaglavlja koja zabranjuju zapisivanje sadržaja upita i odgovora.
          </p>
          <p>
            6.4. Rezultati generirani korištenjem ovih tehnologija su <strong>pomoćni alati</strong> i
            ne čine konačni pravni stav. Korisnik ih je dužan samostalno provjeriti (vidjeti <a href="/odgovornost">Izjavu o odgovornosti</a>).
          </p>

          {/* §7 */}
          <h2>7. Intelektualno vlasništvo</h2>
          <p>
            7.1. Sva prava intelektualnog vlasništva na aplikaciji PravoTek, uključujući izvorni kod,
            dizajn korisničkog sučelja, bazu podataka, algoritme, dokumentaciju i žigove, isključivo
            su vlasništvo Pružatelja usluge.
          </p>
          <p>
            7.2. Korisnik stječe neisključivu, neprenosivu i opozivu licenciju za korištenje aplikacije
            dok koristi aplikaciju, isključivo za vlastite poslovne potrebe odvjetničkog ureda.
          </p>
          <p>
            7.3. Sadržaj (podaci o strankama, predmetima, dokumentima) koji Korisnik unese u aplikaciju
            ostaje u vlasništvu Korisnika. Pružatelj usluge nema pravo korištenja tog sadržaja izvan
            opsega tehničkog pružanja usluge.
          </p>

          {/* §8 */}
          <h2>8. Trajanje i prestanak</h2>
          <p>
            8.1. Ovi Uvjeti stupaju na snagu danom registracije i vrijede do prestanka korištenja usluge.
          </p>
          <p>
            8.2. Korisnik može u svakom trenutku zatražiti zatvaranje svog korisničkog računa i brisanje
            svih podataka pisanim zahtjevom na adresu elektroničke pošte{" "}
            <a href="mailto:vackadavid@gmail.com">vackadavid@gmail.com</a>.
          </p>
          <p>
            8.3. Pružatelj usluge može, uz prethodnu obavijest od <strong>15 (petnaest) dana</strong>,
            raskinuti ugovorni odnos u slučaju:
          </p>
          <ul>
            <li>kršenja ovih Uvjeta od strane Korisnika,</li>
            <li>korištenja aplikacije u nezakonite svrhe.</li>
          </ul>
          <p>
            8.4. Po prestanku ugovornog odnosa, Korisnik ima pravo na izvoz svojih podataka u roku od{" "}
            <strong>30 (trideset) dana</strong> od datuma prestanka, nakon čega Pružatelj usluge briše
            sve podatke Korisnika.
          </p>

          {/* §9 */}
          <h2>9. Primjenjivo pravo i rješavanje sporova</h2>
          <p>9.1. Na ove Uvjete primjenjuje se pravo <strong>Republike Hrvatske</strong>.</p>
          <p>
            9.2. U slučaju spora, ugovorne strane nastojat će spor riješiti mirnim putem. U slučaju
            neuspjeha mirnog rješavanja, nadležan je stvarno nadležni sud u Zagrebu, Republika Hrvatska.
          </p>

          {/* §10 */}
          <h2>10. Završne odredbe</h2>
          <p>
            10.1. Pružatelj usluge zadržava pravo izmjene ovih Uvjeta uz prethodnu obavijest Korisniku
            putem elektroničke pošte ili obavijesti unutar aplikacije, najmanje{" "}
            <strong>15 (petnaest) dana</strong> prije stupanja na snagu izmijenjenih Uvjeta.
          </p>
          <p>
            10.2. Izmijenjene Uvjete Korisnik potvrđuje pri sljedećem ulasku u aplikaciju. Izmjene
            kojima se Korisniku nameću nove obveze primjenjuju se na Korisnika tek kad ih prihvati. Korisnik koji izmjene ne prihvati može zatražiti
            zatvaranje računa prema članku 8.
          </p>
          <p>
            10.3. Ako bilo koja odredba ovih Uvjeta bude proglašena ništavom ili nevažećom, preostale
            odredbe ostaju na snazi.
          </p>
        </article>
      </div>
    </div>
  );
}
