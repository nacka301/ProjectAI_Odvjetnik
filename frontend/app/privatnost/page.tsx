import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Politika privatnosti — PravoTek",
  description: "Politika privatnosti i obrade podataka — PravoTek pravna inteligencija za odvjetnike",
};

export default function PrivatnostPage() {
  return (
    <div className="min-h-screen bg-white">
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-12 sm:py-16">
        {/* Header */}
        <div className="mb-10">
          <a href="/" className="text-xs text-slate-400 hover:text-slate-600 transition">
            ← Natrag na PravoTek
          </a>
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-4xl font-bold text-slate-900 mt-4">
            Politika privatnosti i obrade podataka
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
          <h2>1. Voditelj obrade i kontakt</h2>
          <p>
            1.1. Voditelj obrade osobnih podataka prikupljenih pri registraciji i korištenju aplikacije
            PravoTek je <strong>PravoTek d.o.o.</strong>, OIB: [OIB], sa sjedištem u [ADRESA],
            Republika Hrvatska.
          </p>
          <p>
            1.2. Kontakt za pitanja o zaštiti osobnih podataka:{" "}
            <a href="mailto:podrska@pravotek.hr">podrska@pravotek.hr</a>
          </p>

          {/* §2 */}
          <h2>2. Uloge prema GDPR-u</h2>
          <p>
            2.1. U odnosu na <strong>podatke o Korisniku</strong> (ime, prezime, e-mail, OIB ureda,
            podaci o pretplati), PravoTek je <strong>Voditelj obrade</strong> (Data Controller)
            jer te podatke prikuplja i obrađuje za vlastite potrebe pružanja usluge.
          </p>
          <p>
            2.2. U odnosu na <strong>podatke o klijentima Korisnika</strong> (imena stranaka, detalji
            predmeta, ugovori, ZK izvadci i svi ostali podaci koje Korisnik samostalno unosi u aplikaciju),{" "}
            <strong>Korisnik (Odvjetnički ured) je Voditelj obrade</strong>, a{" "}
            <strong>PravoTek je Izvršitelj obrade</strong> (Data Processor). PravoTek obrađuje te podatke
            isključivo prema uputama Korisnika i u svrhu pružanja usluge.
          </p>

          {/* §3 */}
          <h2>3. Koje podatke prikupljamo</h2>

          <h3>A) Podaci o Korisniku (Odvjetničkom uredu)</h3>
          <ul>
            <li>Ime i prezime, naziv ureda, OIB, adresa sjedišta, IBAN</li>
            <li>Adresa elektroničke pošte, telefonski broj</li>
            <li>Podaci o pretplati i korištenju (plan, krediti, datum isteka)</li>
            <li>Tehnički podaci: IP adresa (haširana — pohranjena u nepovratnom formatu), pregledavač, operativni sustav</li>
          </ul>
          <p>
            <strong>Pravna osnova:</strong> izvršenje ugovora (čl. 6. st. 1. toč. b GDPR-a) i legitimni
            interes (čl. 6. st. 1. toč. f GDPR-a).
          </p>

          <h3>B) Podaci o klijentima (sadržaj aplikacije)</h3>
          <ul>
            <li>Imena i podaci stranaka, OIB stranaka, adrese</li>
            <li>Detalji predmeta, nacrti ugovora, ZK izvadci, kronologije</li>
            <li>Financijski podaci iz troškovnika i računa</li>
          </ul>
          <p>
            <strong>Pravna osnova:</strong> izvršenje ugovora o obradi (DPA) — PravoTek obrađuje ove
            podatke isključivo kao Izvršitelj obrade na temelju dokumentiranih uputa Korisnika.
          </p>

          {/* §4 */}
          <h2>4. Korištenje tehnologije za analizu dokumenata</h2>
          <p>
            4.1. Za funkcionalnosti analize dokumenata, PravoTek koristi sigurne API usluge
            specijaliziranih tehnoloških partnera za obradu prirodnog jezika.
          </p>
          <p>4.2. Podaci preneseni putem API-ja:</p>
          <ul>
            <li><strong>ne koriste se za treniranje</strong> javnih modela,</li>
            <li>obrađuju se u realnom vremenu i <strong>ne pohranjuju trajno</strong> na poslužiteljima pružatelja,</li>
            <li>prenose se isključivo putem šifriranog kanala (TLS 1.2/1.3),</li>
            <li>podliježu B2B/Enterprise uvjetima pružatelja koji zabranjuju korištenje korisničkih podataka u svrhe treniranja.</li>
          </ul>
          <p>
            4.3. Pružatelj usluge primjenjuje tehničke mjere koje zabranjuju bilježenje sadržaja upita
            i odgovora na strani posrednika.
          </p>

          {/* §5 */}
          <h2>5. Pohrana i sigurnost podataka</h2>
          <p><strong>5.1. Tehničke mjere zaštite:</strong></p>
          <ul>
            <li>Enkripcija u prijenosu: <strong>TLS 1.2/1.3</strong> na svim komunikacijskim kanalima</li>
            <li>Enkripcija u mirovanju: <strong>AES-256</strong> na razini baze podataka</li>
            <li>Izolacija podataka: <strong>Row Level Security (RLS)</strong> — tehnički onemogućava pristup podacima drugog korisnika na razini baze podataka</li>
            <li>Autentifikacija: JWT tokeni s ograničenim vijekom trajanja + opcijska dvofaktorska autentifikacija (2FA/TOTP)</li>
            <li>Revizijski trag: nepromjenjivi dnevnik svih operacija (audit log)</li>
            <li>Haširanje IP adresa: osobni podaci pohranjuju se u nepovratnom obliku gdje je to moguće</li>
          </ul>

          {/* §6 */}
          <h2>6. Dijeljenje podataka s trećim stranama</h2>
          <p>
            6.1. PravoTek <strong>ne prodaje, ne iznajmljuje i ne dijeli</strong> korisničke podatke
            s trećim stranama u marketinške ili oglašivačke svrhe.
          </p>
          <p>
            6.2. Podatke dijelimo isključivo s infrastrukturnim partnerima nužnima za rad aplikacije
            (hosting, baza podataka, slanje transakcijskih e-mailova), uz uvjet poštivanja GDPR standarda.
          </p>
          <p>
            6.3. Sa svakim primateljem zaključen je odgovarajući ugovor o obradi (DPA) ili su
            primijenjene standardne ugovorne klauzule (SCC) kada je prijenos podataka izvan EGP-a nužan.
          </p>

          {/* §7 */}
          <h2>7. Vaša prava (GDPR čl. 15.–22.)</h2>
          <p>Korisnik u svakom trenutku ima pravo na:</p>
          <ul>
            <li><strong>Pristup</strong> — uvid u sve podatke koje PravoTek obrađuje o vama</li>
            <li><strong>Ispravak</strong> — ispravak netočnih ili nepotpunih podataka</li>
            <li><strong>Brisanje</strong> („pravo na zaborav") — brisanje svih vaših podataka iz sustava</li>
            <li><strong>Ograničenje obrade</strong> — privremeno zaustavljanje obrade</li>
            <li><strong>Prenosivost</strong> — izvoz podataka u strojno čitljivom formatu</li>
            <li><strong>Prigovor</strong> — prigovor na obradu temeljenu na legitimnom interesu</li>
          </ul>
          <p>
            Zahtjeve možete uputiti na:{" "}
            <a href="mailto:podrska@pravotek.hr">podrska@pravotek.hr</a>.
            Odgovorit ćemo u roku od <strong>30 dana</strong>.
          </p>
          <p>
            Imate pravo podnijeti pritužbu{" "}
            <strong>Agenciji za zaštitu osobnih podataka (AZOP)</strong>,
            Selska cesta 136, 10000 Zagreb,{" "}
            <a href="https://azop.hr" target="_blank" rel="noopener noreferrer">azop.hr</a>.
          </p>

          {/* §8 */}
          <h2>8. Rokovi pohrane</h2>
          <ul>
            <li><strong>Podaci korisničkog računa:</strong> čuvaju se za vrijeme trajanja pretplate + 30 dana nakon zatvaranja računa</li>
            <li><strong>Podaci o predmetima i strankama:</strong> čuvaju se za vrijeme trajanja pretplate + 30 dana, nakon čega se nepovratno brišu</li>
            <li><strong>Revizijski trag (audit log):</strong> čuva se 24 mjeseca u skladu s regulatornim zahtjevima</li>
            <li><strong>Fakture i financijski podaci:</strong> čuvaju se 11 godina sukladno Općem poreznom zakonu (čl. 66.)</li>
          </ul>
        </article>
      </div>
    </div>
  );
}
