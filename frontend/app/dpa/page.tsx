import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Ugovor o obradi podataka (DPA) — PravoTek",
  description: "Data Processing Agreement — Ugovor o obradi osobnih podataka prema čl. 28. GDPR-a",
};

export default function DpaPage() {
  return (
    <div className="min-h-screen bg-white">
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-12 sm:py-16">
        {/* Header */}
        <div className="mb-10">
          <a href="/" className="text-xs text-slate-400 hover:text-slate-600 transition">
            ← Natrag na PravoTek
          </a>
          <h1 className="font-[family-name:var(--font-playfair)] text-3xl sm:text-4xl font-bold text-slate-900 mt-4">
            Ugovor o obradi osobnih podataka
          </h1>
          <p className="text-sm text-slate-500 mt-2">
            Data Processing Agreement — čl. 28. GDPR-a
          </p>
          <p className="text-sm text-slate-400 mt-1">Posljednje ažuriranje: 16. rujna 2026.</p>
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
          <h2>1. Ugovorne strane</h2>
          <p>
            <strong>Voditelj obrade:</strong> Korisnik aplikacije PravoTek (Odvjetnički ured)
            — u daljnjem tekstu: „Voditelj"
          </p>
          <p>
            <strong>Izvršitelj obrade:</strong> David Vacka, fizička osoba, kontakt:{" "}
            <a href="mailto:vackadavid@gmail.com">vackadavid@gmail.com</a> — u daljnjem tekstu:
            „Izvršitelj" ili „PravoTek"
          </p>

          {/* §2 */}
          <h2>2. Predmet i trajanje obrade</h2>
          <p>
            2.1. Ovaj Ugovor uređuje obradu osobnih podataka koju Izvršitelj obavlja u ime Voditelja
            prilikom korištenja aplikacije PravoTek.
          </p>
          <p>
            2.2. Obrada traje za vrijeme trajanja ugovornog odnosa između
            Voditelja i Izvršitelja.
          </p>

          {/* §3 */}
          <h2>3. Priroda i svrha obrade</h2>
          <p>
            3.1. <strong>Svrha:</strong> Tehnička pohrana, organizacija, pretraživanje i prikaz
            podataka koje Voditelj unosi u aplikaciju, te — kada Voditelj koristi module koji se
            oslanjaju na umjetnu inteligenciju — automatska analiza dokumenata putem API usluga u
            svrhu pružanja funkcionalnosti aplikacije.
          </p>
          <p>
            3.2. <strong>Vrste obrade:</strong> Pohrana, strukturiranje, pretraživanje, korištenje
            (analiza putem API-ja), brisanje.
          </p>

          {/* §4 */}
          <h2>4. Kategorije ispitanika i osobnih podataka</h2>
          <p>
            4.1. <strong>Kategorije ispitanika:</strong> Klijenti (stranke) Voditelja, protivne strane
            u postupcima, svjedoci, suci i druge osobe čije podatke Voditelj unosi u aplikaciju.
          </p>
          <p>4.2. <strong>Kategorije osobnih podataka:</strong></p>
          <ul>
            <li>Identifikacijski podaci: ime, prezime, OIB, adresa</li>
            <li>Kontaktni podaci: telefon, e-mail</li>
            <li>Pravni podaci: detalji predmeta, podnesci, ugovori, ZK izvadci</li>
            <li>Financijski podaci: iznosi iz troškovnika i računa</li>
          </ul>
          <p>
            4.3. <strong>Posebne kategorije podataka:</strong> Izvršitelj ne obrađuje posebne kategorije
            osobnih podataka u smislu čl. 9. GDPR-a osim ako ih Voditelj samostalno unese u slobodna
            tekstualna polja. U tom slučaju, odgovornost za zakonitost obrade tih podataka snosi
            isključivo Voditelj.
          </p>

          {/* §5 */}
          <h2>5. Obveze Izvršitelja obrade</h2>
          <p>Izvršitelj se obvezuje da će:</p>
          <p>
            5.1. Obrađivati osobne podatke <strong>isključivo prema dokumentiranim uputama</strong>{" "}
            Voditelja, uključujući upute sadržane u ovom Ugovoru i Uvjetima korištenja.
          </p>
          <p>
            5.2. Osigurati da su sve osobe ovlaštene za obradu podataka{" "}
            <strong>podvrgnute obvezi čuvanja povjerljivosti</strong>.
          </p>
          <p>
            5.3. Poduzeti sve <strong>tehničke i organizacijske mjere</strong> u skladu s čl. 32.
            GDPR-a, uključujući ali ne ograničavajući se na:
          </p>
          <ul>
            <li>Enkripciju podataka u prijenosu i mirovanju (TLS 1.2/1.3, AES-256)</li>
            <li>Izolaciju podataka korisnika putem Row Level Security (RLS)</li>
            <li>Kontrolu pristupa putem JWT tokena i opcijskog 2FA</li>
            <li>Nepromjenjivi revizijski trag (audit log)</li>
            <li>Redovite sigurnosne provjere infrastrukture</li>
          </ul>
          <p>
            5.4. Ne angažirati <strong>podizvršitelja obrade</strong> bez prethodnog općeg ili posebnog
            pisanog odobrenja Voditelja. Popis trenutnih podizvršitelja naveden je u članku 6. ovog
            Ugovora. Izvršitelj će obavijestiti Voditelja o svakoj namjeravanoj promjeni podizvršitelja
            najmanje <strong>15 (petnaest) dana</strong> unaprijed.
          </p>
          <p>
            5.5. U mjeri u kojoj je to moguće, <strong>pomagati Voditelju</strong> u ispunjavanju
            obveza prema ispitanicima (prava pristupa, ispravka, brisanja i dr.) tehničkim sredstvima
            ugrađenim u aplikaciju.
          </p>
          <p>
            5.6. Po prestanku pružanja usluge, <strong>izbrisati ili vratiti</strong> sve osobne podatke
            prema izboru Voditelja, te uništiti sve kopije, osim ako pravo Unije ili države članice
            zahtijeva pohranu.
          </p>
          <p>
            5.7. Staviti na raspolaganje Voditelju <strong>sve informacije potrebne za dokazivanje
            ispunjenja</strong> obveza iz ovog Ugovora te dopustiti i pridonijeti revizijama.
          </p>

          {/* §6 */}
          <h2>6. Podizvršitelji obrade</h2>
          <p>
            6.1. Voditelj daje Izvršitelju <strong>opće pisano odobrenje</strong> za korištenje
            sljedećih podizvršitelja obrade:
          </p>
          <div className="overflow-x-auto">
            <table>
              <thead>
                <tr>
                  <th>Podizvršitelj</th>
                  <th>Svrha</th>
                  <th>Jamstvo</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>Supabase Inc.</td>
                  <td>Pohrana podataka (PostgreSQL)</td>
                  <td>DPA na supabase.com/privacy</td>
                </tr>
                <tr>
                  <td>Render Services Inc.</td>
                  <td>Backend hosting</td>
                  <td>DPA na render.com/privacy</td>
                </tr>
                <tr>
                  <td>Vercel Inc.</td>
                  <td>Frontend hosting / CDN</td>
                  <td>DPA na vercel.com/legal/dpa</td>
                </tr>
                <tr>
                  <td>OpenRouter</td>
                  <td>Analiza dokumenata (API gateway)</td>
                  <td>B2B API uvjeti; No-Log politika</td>
                </tr>
                <tr>
                  <td>Brevo (Sendinblue)</td>
                  <td>Transakcijski e-mail (SMTP)</td>
                  <td>DPA na brevo.com/legal</td>
                </tr>
                <tr>
                  <td>Resend (Plus Five Five, Inc.)</td>
                  <td>E-mail obavijest o prijavi greške u izračunu (bez podataka stranaka)</td>
                  <td>DPA na resend.com/legal/dpa</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p>
            6.2. U odnosu na obradu putem API-ja za analizu dokumenata, kada se ta obrada odvija,
            Izvršitelj jamči da:
          </p>
          <ul>
            <li>se podaci <strong>ne koriste za treniranje</strong> javnih modela,</li>
            <li>se podaci <strong>ne pohranjuju trajno</strong> na poslužiteljima podizvršitelja,</li>
            <li>se primjenjuju <strong>tehničke mjere</strong> za sprečavanje zapisivanja podataka.</li>
          </ul>

          {/* §7 */}
          <h2>7. Prijava povrede osobnih podataka</h2>
          <p>
            7.1. U slučaju saznanja o povredi osobnih podataka (data breach), Izvršitelj će obavijestiti
            Voditelja <strong>bez nepotrebnog odgađanja, a najkasnije u roku od 48 (četrdeset osam)
            sati</strong> od saznanja o povredi.
          </p>
          <p>7.2. Obavijest će sadržavati:</p>
          <ul>
            <li>opis prirode povrede,</li>
            <li>kategorije i okvirni broj ispitanika i evidencija osobnih podataka na koje povreda utječe,</li>
            <li>vjerojatne posljedice povrede,</li>
            <li>mjere poduzete ili predložene za ublažavanje štetnih učinaka.</li>
          </ul>

          {/* §8 */}
          <h2>8. Primjenjivo pravo</h2>
          <p>
            8.1. Na ovaj Ugovor primjenjuje se pravo <strong>Republike Hrvatske</strong> i{" "}
            <strong>Opća uredba o zaštiti podataka (GDPR — Uredba (EU) 2016/679)</strong>.
          </p>
        </article>
      </div>
    </div>
  );
}
