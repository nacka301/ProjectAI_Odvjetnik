import type { Metadata } from "next";

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
          <a href="/" className="text-xs text-slate-400 hover:text-slate-600 transition">
            ← Natrag na PravoTek
          </a>
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-4xl font-bold text-slate-900 mt-4">
            Uvjeti korištenja
          </h1>
          <p className="text-sm text-slate-400 mt-2">Posljednje ažuriranje: 3. ožujka 2026.</p>
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
            <a href="mailto:podrska@pravotek.hr">podrska@pravotek.hr</a>, i pravne ili fizičke
            osobe koja koristi aplikaciju PravoTek (u daljnjem tekstu: „Korisnik").
          </p>
          <p>
            1.2. PravoTek je softverska aplikacija u oblaku (SaaS — Software as a Service) namijenjena
            odvjetničkim uredima u Republici Hrvatskoj, koja pruža alate za automatski izračun odvjetničke
            nagrade prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (NN 142/2012 i naknadne
            izmjene), procjenu tržišne vrijednosti nekretnina, analizu pravnih dokumenata, upravljanje
            strankama i predmetima te izdavanje e-računa.
          </p>
          <p>
            1.3. Pristupanjem ili korištenjem aplikacije, Korisnik potvrđuje da je pročitao, razumio i
            da prihvaća ove Uvjete u cijelosti.
          </p>

          {/* §2 */}
          <h2>2. Usluga i opseg</h2>
          <p>2.1. PravoTek Korisniku omogućuje korištenje sljedećih modula:</p>
          <ul>
            <li><strong>Odvjetnički troškovnik</strong> — automatski izračun nagrade i troškova prema službenoj Tarifi</li>
            <li><strong>Tržišna procjena nekretnina</strong> — izračun raspona tržišne vrijednosti na temelju usporedive ponude</li>
            <li><strong>Analiza ugovora</strong> — automatska detekcija pravnih rizika i nedostajućih klauzula</li>
            <li><strong>ZK due diligence</strong> — analiza zemljišnoknjižnog stanja nekretnine</li>
            <li><strong>Upravljanje strankama i predmetima</strong> — CRM i case management za odvjetničke urede</li>
            <li><strong>e-Račun (UBL)</strong> — generiranje elektroničkih računa</li>
            <li><strong>Kronologija spisa</strong> — kronološki pregled događaja u predmetu</li>
            <li><strong>Pravna strategija</strong> — analiza pravne pozicije klijenta</li>
            <li><strong>Sudski tumač</strong> — pravni prijevod i transliteracija</li>
          </ul>
          <p>
            2.2. Pružatelj usluge zadržava pravo dodavanja, izmjene ili ukidanja pojedinih modula
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
          <h2>4. Pretplatni modeli i plaćanje</h2>
          <p>4.1. PravoTek nudi sljedeće pretplatne razine:</p>
          <div className="overflow-x-auto">
            <table>
              <thead>
                <tr>
                  <th></th>
                  <th>Free</th>
                  <th>Starter</th>
                  <th>Pro</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="font-medium">Cijena</td>
                  <td>0,00 €/mj</td>
                  <td>19,00 €/mj</td>
                  <td>39,00 €/mj</td>
                </tr>
                <tr>
                  <td className="font-medium">Predmeti</td>
                  <td>do 5</td>
                  <td>Neograničeno</td>
                  <td>Neograničeno</td>
                </tr>
                <tr>
                  <td className="font-medium">Računi</td>
                  <td>do 3</td>
                  <td>Neograničeno</td>
                  <td>Neograničeno</td>
                </tr>
                <tr>
                  <td className="font-medium">AI krediti</td>
                  <td>5/mj</td>
                  <td>0</td>
                  <td>100/mj</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p>
            4.2. Pružatelj usluge zadržava pravo izmjene cijena uz prethodnu pisanu obavijest
            Korisniku najmanje <strong>30 (trideset) dana</strong> prije primjene nove cjenovne politike.
            Za Korisnike s aktivnom pretplatom, nova cijena primjenjuje se od početka sljedećeg obračunskog razdoblja.
          </p>
          <p>
            4.3. Pretplata se automatski obnavlja na početku svakog obračunskog razdoblja (mjesečno),
            osim ako je Korisnik prethodno otkazao pretplatu.
          </p>
          <p>
            4.4. AI krediti se mjesečno resetiraju na početku svakog obračunskog razdoblja.
            Neiskorišteni krediti se <strong>ne prenose</strong> u sljedeće razdoblje.
          </p>

          {/* §5 */}
          <h2>5. Ograničenje odgovornosti</h2>
          <p>
            <strong>5.1. PravoTek je softverski alat, a ne pružatelj pravnih usluga.</strong>{" "}
            Pružatelj usluge ne pruža pravne savjete, pravno zastupanje niti bilo koji oblik pravne
            pomoći u smislu Zakona o odvjetništvu (NN 9/94 i naknadne izmjene).
          </p>
          <p>
            <strong>5.2. Isključiva odgovornost za pravnu točnost.</strong> Sve izračune, analize,
            procjene, prijedloge klauzula i druge rezultate generirane korištenjem aplikacije
            Korisnik je dužan <strong>samostalno provjeriti</strong> prije njihove uporabe u pravnom
            prometu. Konačna odgovornost za točnost svih podataka, izračuna i pravnih dokumenata
            u cijelosti leži na Korisniku (Odvjetničkom uredu).
          </p>
          <p><strong>5.3. Isključenje odgovornosti za štetu.</strong> Pružatelj usluge ne odgovara za:</p>
          <ul>
            <li>(a) štetu nastalu korištenjem rezultata aplikacije bez prethodne stručne provjere od strane Korisnika,</li>
            <li>(b) štetu nastalu uslijed prekida dostupnosti usluge, tehničkih pogrešaka ili kibernetičkih napada,</li>
            <li>(c) neizravnu, posljedičnu ili kaznenu štetu, uključujući ali ne ograničavajući se na izgubljenu dobit, gubitak podataka ili gubitak poslovnog ugleda,</li>
            <li>(d) štetu nastalu uslijed promjene zakonodavnog okvira ili sudske prakse nakon generiranja rezultata analize.</li>
          </ul>
          <p>
            <strong>5.4. Gornja granica odgovornosti.</strong> U svakom slučaju, ukupna kumulativna
            odgovornost Pružatelja usluge prema Korisniku ograničena je na iznos koji je Korisnik
            platio za korištenje usluge u razdoblju od <strong>12 (dvanaest) mjeseci</strong> neposredno
            prije nastanka štetnog događaja.
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
            ne čine konačni pravni stav. Korisnik ih je dužan samostalno provjeriti (vidjeti članak 5.2.).
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
            za vrijeme trajanja pretplate, isključivo za vlastite poslovne potrebe odvjetničkog ureda.
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
            <a href="mailto:podrska@pravotek.hr">podrska@pravotek.hr</a>.
          </p>
          <p>
            8.3. Pružatelj usluge može, uz prethodnu obavijest od <strong>15 (petnaest) dana</strong>,
            raskinuti ugovorni odnos u slučaju:
          </p>
          <ul>
            <li>kršenja ovih Uvjeta od strane Korisnika,</li>
            <li>korištenja aplikacije u nezakonite svrhe,</li>
            <li>neplaćanja pretplate duže od 30 (trideset) dana.</li>
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
            10.2. Nastavak korištenja aplikacije nakon stupanja na snagu izmijenjenih Uvjeta smatra
            se prihvaćanjem istih.
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
