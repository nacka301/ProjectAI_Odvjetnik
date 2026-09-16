# Plan — pravna zaštita: odgovornost za točnost izračuna

> Nakon odobrenja ovaj se dokument sprema kao `docs/plan-pravna-zastita.md` na granu `razvoj`
> (commit + push samo te datoteke) i **tu se staje**. Kod se piše tek na Davidovu potvrdu,
> korak po korak, jedan korak = jedan zadatak.

## Kontekst

Troškovnik iz PravoTeka ide sudu. Ako je iznos kriv, odvjetnik gubi novac. Alat gradi jedna osoba,
izračun nije neovisno revidiran. Cilj: (1) reći istinu o tome što alat jest, (2) provjeru učiniti
dijelom posla, (3) ograničiti odgovornost koliko ZOO dopušta — bez zatrpavanja sučelja.

**Odluke donesene u ovom razgovoru (16.9.2026.):**
- Na **ispisu za sud** ide **neutralna bilješka** — pravna osnova i datum, bez imena alata i bez
  ograde. Puna bilješka (PravoTek + „za izračun odgovara odvjetnik") stoji u aplikaciji uz spremljeni
  troškovnik i u Excelu. Time ostaje na snazi pravilo iz prompta 3.
- **Cijene** nestaju iz Uvjeta, s landinga i iz aplikacije. Piše: besplatno tijekom probnog razdoblja,
  naplata najavljena najmanje 30 dana unaprijed uz novu potvrdu uvjeta.
- **Ograničenje odgovornosti bez iznosa** — isključenje za običnu nepažnju, izričito ne za namjeru i
  krajnju nepažnju. Odvjetnik potvrđuje.

---

## Dio 1 — Nalazi istraživanja

### 1.1 ZOO — isključenje i ograničenje odgovornosti (provjereno u tekstu propisa)

Izvor: pročišćeni tekst na zakon.hr (NN 35/05, 41/08, 125/11, 78/15, 29/18, 126/21, 114/22, 156/22,
155/23, 69/26; baza ažurirana 16.9.2026.). Čl. 345. nema oznaku izmjene — tekst je izvorni iz NN 35/05.
Isti tekst potvrđen i u RRiF-ovu izvatku.

**Čl. 345. — Ograničenje i isključenje odgovornosti**
> (1) Odgovornost dužnika za namjeru ili krajnju nepažnju ne može se unaprijed ugovorom isključiti ni ograničiti.
> (2) Ali sud može na zahtjev zainteresirane ugovorne strane poništiti i ugovornu odredbu o isključenju i ograničenju odgovornosti za običnu nepažnju, ako je takav sporazum proizašao iz monopolskog položaja dužnika ili uopće iz neravnopravnog odnosa ugovornih strana.
> (3) Pravovaljana je odredba ugovora kojom se određuje najviši iznos naknade, ako tako određeni iznos nije u očitom nerazmjeru sa štetom i ako za određeni slučaj nije što drugo zakonom određeno.
> (4) U slučaju ograničenja visine naknade vjerovnik ima pravo na potpunu naknadu ako je nemogućnost ispunjenja obveze prouzročena namjerno ili krajnjom nepažnjom dužnika.

Što iz toga slijedi:
- **Ne može se isključiti:** namjera i krajnja nepažnja (st. 1). Svaka odredba koja to pokušava je
  neučinkovita, a u općim uvjetima i sumnjiva po čl. 296.
- **Može se isključiti:** obična nepažnja — ali sud je može poništiti ako postoji monopolski položaj
  ili neravnopravan odnos (st. 2). PravoTek nije monopolist; odnos odvjetnik–alat teško je
  „neravnopravan" u korist alata, ali to je **procjena, ne sigurnost**.
- **Najviši iznos naknade može se ugovoriti** (st. 3), ali ne smije biti u očitom nerazmjeru sa štetom.
  Sadašnji §5.4 Uvjeta („iznos koji je Korisnik platio u 12 mjeseci") dok je usluga besplatna iznosi
  0 € — gotovo sigurno očiti nerazmjer. Zato ga mičemo.

**Čl. 295.–296. — Opći uvjeti ugovora** (Uvjeti korištenja su opći uvjeti):
- čl. 295. st. 4–5: opći uvjeti moraju se objaviti na uobičajeni način i obvezuju ako su korisniku bili
  poznati ili morali biti poznati u vrijeme sklapanja. → Potvrda pri prvom ulasku (dio C) to dokazuje.
- čl. 296. st. 1: ništetne su odredbe općih uvjeta koje „suprotno načelu savjesnosti i poštenja,
  prouzroče očiglednu neravnopravnost u pravima i obvezama strana". → Preširoko isključenje
  (npr. sadašnji §5.3 b „pogreške" bez razlike na stupanj krivnje) riskira ništetnost.

**Čl. 346. st. 4 i čl. 347.** — korisni i bez ikakve klauzule: oštećeni mora poduzeti razumne mjere da
smanji štetu, a naknada se razmjerno smanjuje ako je oštećeni pridonio šteti. Odvjetnik koji nije
provjerio izračun prije podnošenja vjerojatno je pridonio šteti. To je razlog zašto je *poticanje
provjere* (dio C, D) pravno vrijednije od sitnog teksta.

**Nisam siguran, treba provjeriti s odvjetnikom:**
- Je li odnos s korisnikom tijekom besplatnog razdoblja uopće ugovorni (pa vrijedi čl. 345.), ili bi se
  tužba vodila po izvanugovornoj odgovornosti (čl. 1045. i dalje) — i obuhvaća li ugovorno isključenje i
  takav zahtjev.
- Drži li isključenje za običnu nepažnju po čl. 296. u općim uvjetima prema profesionalnom korisniku.

### 1.2 Odvjetnik: potrošač ili poslovni subjekt

**Zakon o zaštiti potrošača** (zakon.hr, izmjene do NN 59/26), čl. 4. t. 28:
> potrošač je svaka fizička osoba koja sklapa pravni posao ili djeluje na tržištu izvan svoje trgovačke, poslovne, obrtničke ili profesionalne djelatnosti

Odvjetnik koji alat koristi za rad ureda djeluje **u okviru profesionalne djelatnosti → nije potrošač.**
Posljedice:
- **Ne primjenjuju se:** nepoštene odredbe u potrošačkim ugovorima (ZZP čl. 53.–54.), obavijesti prije
  sklapanja ugovora na daljinu (čl. 60.), pravo na jednostrani raskid u 14 dana (čl. 79. i dalje),
  obveza pisanog prigovora s odgovorom u 15 dana (čl. 10.).
- **Primjenjuje se:** opći ZOO — čl. 345. i čl. 296. (gore). To je širi prostor za ograničenje nego
  prema potrošaču, ali ne neograničen.
- **Što napraviti:** u Uvjetima izričito navesti da je usluga namijenjena odvjetnicima i odvjetničkim
  uredima za obavljanje profesionalne djelatnosti. Registracija i sad traži naziv ureda.

**Nisam siguran:** odvjetnički vježbenik ili student prava koji se registrira „da proba" — je li potrošač.
Pitanje za odvjetnika; praktično rješenje je rečenica u Uvjetima o namjeni.

**Zakon o trgovačkim ugovorima:** ZOO čl. 14. st. 2 — trgovački ugovori su ugovori „što ih sklapaju
trgovci među sobom". David nije trgovac (nema obrt ni društvo), pa posebna pravila za trgovačke
ugovore ne vrijede; primjenjuju se opća pravila ZOO-a.

### 1.3 Obvezni sadržaj — Zakon o elektroničkoj trgovini i ZZP

**Zakon o elektroničkoj trgovini** (zakon.hr: NN 173/03, 67/08, 36/09, 130/11, 30/14, 32/19, 67/25).

- **čl. 2. t. 2:** usluga informacijskog društva je usluga „koja se uz naknadu pruža elektroničkim putem".
  Dok je PravoTek besplatan, **nisam siguran** da je uopće u opsegu Zakona — ali Direktiva 2000/31
  tumači „uz naknadu" kao gospodarsku djelatnost, pa se obveze ionako preporučuje ispuniti.
- **čl. 6. st. 1 — opće informacije**, neposredno i stalno dostupne:
  - ime i prezime ili tvrtka davatelja → **David Vacka** ✔
  - sjedište obrta ili pravne osobe → **nema** (fizička osoba bez obrta); ne izmišljati
  - podaci za brz kontakt, „uključujući elektroničku adresu" → **vackadavid@gmail.com** ✔
  - broj registra → nema; porezni broj samo za obveznike PDV-a → nije
  - **Nisam siguran:** traži li se za fizičku osobu bez obrta ipak poštanska adresa (Direktiva 2000/31
    čl. 5. traži „geografsku adresu"; hrvatski tekst navodi samo sjedište obrta/pravne osobe).
    Pitanje za odvjetnika.
- **čl. 6. st. 2:** ako se prikazuju cijene, moraju biti jasne. → Razlog više da cijene maknemo dok nisu na snazi.
- **čl. 12.:** prije sklapanja ugovora obavijestiti o koracima, ugovornim odredbama, općim uvjetima,
  jeziku. St. 3: strane koje nisu potrošači mogu ugovoriti odstupanje.
- **čl. 13.:** opći uvjeti moraju biti dostupni tako da ih se može „spremiti, ponovno koristiti i
  reproducirati". → Stranica mora biti ispisiva; stare verzije treba moći dohvatiti (čuvamo ih u gitu,
  datum verzije piše na stranici).

**Nisam siguran, treba provjeriti s odvjetnikom (i računovođom):** smije li fizička osoba bez registrirane
djelatnosti javno nuditi uslugu koja je besplatna, ali se najavljuje kao buduća plaćena. Ovo nije pitanje
teksta Uvjeta nego statusa — i odgovor određuje kad mora nastati obrt ili društvo.

### 1.4 Kako to rade slični alati

1. **Tarifo** (tarifo.net, hrvatski kalkulator tarife i pristojbi). Uz izračun navodi propis s brojem NN
   i datum ažuriranja. Iznos zove „referentna vrijednost", ne pravni savjet.
   - *Dobro:* verzija propisa i datum ažuriranja stoje uz rezultat.
   - *Pouka:* na dan provjere (16.9.2026.) navodi Uredbu NN 37/2023, iako je od 1.6.2026. na snazi
     NN 50/2026. Upravo zato vrijedi zapisati verziju uz svaki izračun: zastarjelost se tada vidi.
2. **DAV-Prozesskostenrechner** (Deutscher Anwaltverein). Kod izmjene RVG-a (1.6.2025.) nudi
   izračun i po staroj i po novoj verziji.
   - *Dobro:* verzija propisa je vidljiv izbor korisnika, a ne skrivena pretpostavka.
   - Isto načelo već postoji u PravoTeku: odabir Tarife po dospijeću (Tbr. 52) i Uredbe po datumu podneska.
3. **Njemački RVG kalkulatori** (npr. deutschland-rechner.de, prema opisu u rezultatima pretrage):
   iznos je „unverbindliche Schätzung", ne zamjenjuje savjet, „ohne Gewähr".
   - *Dobro:* kratko i na jednom mjestu.
   - *Loše za nas:* „orijentacijski / bez jamstva" na dokumentu koji ide sudu obezvrjeđuje dokument.
     Zato to ne ide na ispis.
4. **TurboTax „100% Accurate Calculations Guarantee"** (Intuit) — suprotan pristup. Plaćaju kamate i
   kazne nastale zbog pogreške *u izračunu*, ali ne zbog pogrešnog unosa ili razvrstavanja korisnika.
   - *Dobro:* jasno razdvaja pogrešku alata od pogreške unosa. Tu razliku preuzimamo u Izjavi.
   - Samo jamstvo ne preuzimamo: nemamo osiguranje ni pravnu osobu.

### 1.5 Što već postoji u kodu (repo `C:\Projects\odvjetnik_ai`, grana `razvoj`)

**Pravne stranice.** Postoje tri: `frontend/app/uvjeti/page.tsx`, `privatnost/page.tsx` i `dpa/page.tsx`.
- Server komponente bez zajedničkog layouta.
- Javne u `frontend/middleware.ts:50-54`.
- Pružatelj je svugdje „David Vacka, fizička osoba", kontakt vackadavid@gmail.com. ✔ Nema d.o.o.,
  „naš tim", „tvrtka", „sjedište", `[OIB]` — provjereno pretragom cijelog `frontend/`, `Backend/` i
  `supabase/emails`. „d.o.o./obrt/Tvrtka" postoje samo kao placeholderi za *korisnikove* podatke i u
  demo podacima, i to je ispravno.

**Poveznice.**
- Landing podnožje: `components/landing/Footer.tsx:13-21`
- `components/landing/Security.tsx:26-28`
- Podnožje u aplikaciji `SecurityFooter`: `components/TrustComponents.tsx:130-140`
- **U Postavkama nema poveznica.**

**Registracija.** `app/auth/register/page.tsx:588-602` ima kvačicu „Prihvaćam Uvjete…".
- **Ne sprema se nigdje**, provjerava se samo u pregledniku.
- **Google registracija je potpuno zaobilazi**: kvačica je na koraku 2, Google gumb na koraku 1.

**Neistine i zastarjelosti koje treba ukloniti:**

| Mjesto | Problem |
|---|---|
| `uvjeti/page.tsx:98-144` §4 | Cijene 19 €/190 €, plaćanje, pretplata — kao da vrijede |
| `uvjeti/page.tsx:169-172` §5.4 | Ograničenje na plaćeni iznos = 0 € |
| `uvjeti/page.tsx:161-167` §5.3 | Isključuje „pogreške" bez razlike namjera/krajnja nepažnja |
| `uvjeti/page.tsx:45-47` §1.2 | Tarifa „NN 142/2012" — kod računa po Tarifi HOK od 14.10.2023. |
| `uvjeti/page.tsx:59` §2.1 | Nabraja 9 modula; korisnicima rade 2 |
| `uvjeti` §10.2 | „nastavak korištenja = prihvat izmjena" → zamjenjuje se ponovnom potvrdom |
| sve tri stranice | „Posljednje ažuriranje: 3. ožujka 2026." — netočno (mijenjano u kolovozu) |
| `landing/Pricing.tsx:19-20,37,82,86` | 19 €/mj, 190 €/god, „Cijena zaključana za rane korisnike" |
| `landing/AnnouncementBar.tsx:12`, `landing/Faq.tsx:18` | Cijena |
| `landing/Faq.tsx:10` | „izmjena propisa ne traži novu verziju programa" — **netočno**, nova Uredba traži izmjenu koda |
| `lib/useSubscription.ts:63-66`, `settings/SubscriptionView.tsx:158-176`, `PretplataGate.tsx:84-100` | Cijene u aplikaciji |

Riječi „jamči točnost", „pravno provjeren", „revidiran" **ne postoje nigdje** ✔.
- „Korisnik jamči" (`uvjeti:79`) i „Izvršitelj jamči" (`dpa:182`, AI ne trenira na podacima) nisu
  tvrdnje o točnosti — ostaju, odvjetnik ih vidi u pregledu.

**Troškovnik — spremanje i verzije:**
- **Spremljeni troškovnik se nikad ne preračunava.** Backend na `POST /api/troskovnik/spremi` računa
  ponovno i cijeli odgovor sprema u `raw_json_output`:
  - `Backend/Controllers/TroskovnikController.cs:118-154`, `SastaviZapis` :234-269
  - `Backend/Services/ClientCaseService.cs:357`
- Stranica `app/troskovnik/[id]/page.tsx` samo čita spremljeno. Nitko ne radi UPDATE nad `troskovnici`.
  Dobra osnova — zapis je već „zamrznut".
- **Registar verzija već postoji:**
  - `TarifaVerzije` (`Backend/Models/TarifaKonstante.cs:102-154`) — odabir po dospijeću, Tbr. 52
  - `UredbaVerzija { Nn50_2026, Nn37_2023 }` (`Backend/Models/Pristojbe.cs:16-34`) — odabir **po retku**
    (`SudskePristojbeCalculator.cs:294-317`); per-line `uredba` je u `raw_json_output`
  - Zrcalo u `frontend/lib/tarifa.ts:293-340, 908-914`
- **Nema stupca** za verziju propisa ni datum izračuna. Ima samo `created_at` i
  `raw_json_output.datum_izrade`. Verzija Tarife po nazivu ne ulazi u JSON, samo `vrijednost_boda`.

**Ispis:** `components/troskovnik/TroskovnikIspis.tsx`.
- Kraj dokumenta: tablica → potpis (:211-216) → napomena (:219).
- `@page { margin: 0 }` je namjeran (inače preglednik crta URL) — bilješka mora biti običan sadržaj,
  ne margin-box.
- Test `__tests__/troskovnik-ispis.test.tsx:116-125` zabranjuje „PravoTek", „orijentacijski" i
  „Hrvatska odvjetnička komora" na ispisu.

**Excel:** `lib/troskovnik-xlsx.ts:225-233` završava „SVEUKUPNI TROŠAK" + „Napomena". Postoji samo na
obrascu, ne na spremljenom zapisu. Drugih izvoza nema.

**Gateovi:** `middleware.ts` (MFA, admin, zaključani moduli), `components/PretplataGate.tsx` (klijentski).
- Onboarding stanje iz baze: `lib/onboarding.ts`, `user_profiles.onboarded_at`.
- Vodič se automatski pali u `Dashboard.tsx:270-281`.

**Mail:** `Backend/Services/ResendEmailService.cs` je registriran, ali **nikad korišten**.
- **Tihi kvar:** `appsettings.json:23-27` ima placeholder `"YOUR_RESEND_API_KEY"` koji se čita prije env
  varijable `RESEND_API_KEY`, pa servis misli da je konfiguriran i pada na SMTP prijavi.
- Admin adresa je u `Middleware/AdminIdentity.cs` (nema javni getter).
- Nijedan tok danas ne šalje mail adminu.

**Rate limiting** (`Program.cs:102-130`) **nije po korisniku/IP-u** nego globalan — „auth" politika bi
nakon 5 prijava blokirala sve korisnike.

**Prijava greške / feedback:** ne postoji ništa. Samo `mailto:` poveznice.

---

## Dio 2 — Rješenje

### A. Verzioniranje propisa (najvažnije)

**Baza** — nova polja na `troskovnici` (migracija 025, dio A):

| Stupac | Tip | Sadržaj |
|---|---|---|
| `izracunato_at` | `timestamptz` | trenutak izračuna na serveru pri spremanju |
| `tarifa_naziv` | `text` | `TarifaVerzija.Naziv` odabrane verzije |
| `tarifa_vrijedi_od` | `date` | `TarifaVerzija.VrijediOd` |
| `vrijednost_boda` | `numeric(10,2)` | `TarifaVerzija.BodValue` |
| `uredbe_pristojbe` | `text[]` | različite Uredbe stvarno primijenjene na retke (npr. `{"Uredba o Tarifi sudskih pristojbi (NN 50/2026)"}`; može biti obje) |
| `verzija_izracuna` | `text` | git commit backenda (`RENDER_GIT_COMMIT`), da se pogreška može reproducirati |

- **Puni ih isključivo backend** u `SastaviZapis` (`TroskovnikController.cs`) iz istog rezultata koji
  ide u `raw_json_output` — nikad iz tijela zahtjeva. Dodati polja u `TroskovnikRecord`
  (`Backend/Models/ClientModels.cs:91-155`, zapis i čitanje). `TroskovnikService.cs:467` već zna
  odabranu `TarifaVerzija`; treba je izložiti u rezultatu (npr. `tarifa_verzija` objekt uz
  `vrijednost_boda` :785).
- **Stari troškovnici:** stupci ostaju `NULL`, bez naknadnog popunjavanja. Prikaz: „Propis nije zabilježen
  — troškovnik je spremljen prije uvođenja ove oznake." Ne izmišljamo podatak koji nismo zapisali.
- Kad se propis promijeni: dodaje se redak u `TarifaVerzije` / nova `UredbaVerzija`. Stari zapisi i dalje
  nose svoj naziv jer je spremljen kao tekst, a ne kao poveznica na konstantu.

**Prikaz — tri mjesta, tri teksta:**

1. **Spremljeni troškovnik u aplikaciji** (`app/troskovnik/[id]/page.tsx`, ispod kartice zbroja :218-239) — puna bilješka:
   > Izračunato u alatu PravoTek 16.9.2026. prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (HOK, na snazi od 14.10.2023., 1 bod = 2,00 €) i Uredbi o Tarifi sudskih pristojbi (NN 50/2026). Izračun provjerava i za njega odgovara odvjetnik.
2. **Excel** (`lib/troskovnik-xlsx.ts`) — ista puna bilješka kao zadnji redak ispod „Napomena".
   Na obrascu (nespremljeno) umjesto datuma spremanja stoji datum izvoza, a verzije iz rezultata izračuna.
3. **Ispis za sud** (`TroskovnikIspis.tsx`, iza potpisa i napomene, sitnim slovima) — **neutralno**:
   > Obračun prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (na snazi od 14.10.2023.) i Uredbi o Tarifi sudskih pristojbi (NN 50/2026). Stanje propisa na dan 16.9.2026.

   Bez „PravoTek", bez ograde. Nazivi dolaze iz novog polja `TroskovnikIzvozModel.pravnaOsnova`,
   koje pune `buildIzvozModel` i `izvozModelIzZapisa` (`lib/troskovnik-izvoz-model.ts`).
   - Na ispisu se izostavlja „Hrvatska odvjetnička komora" iz naziva: test to i sada zabranjuje, a
     naziv Tarife je dovoljan. Datum „na snazi od" ostaje.
   - Test se dopunjuje: bilješka postoji; „PravoTek" i „orijentacijski" i dalje zabranjeni.

### B. Izjava o odgovornosti i Uvjeti korištenja

**Struktura.**
- **Nova stranica `/odgovornost` — „Izjava o odgovornosti".** Kratka, ljudska, jedini izvor teksta o
  odgovornosti.
- **`/uvjeti` se prepravlja:** §5 postaje kratko upućivanje na Izjavu kao sastavni dio Uvjeta, pa se
  tekst ne duplicira i ne može se razići.
- **Jedna konstanta verzije** `PRAVNI_TEKSTOVI_VERZIJA = "2026-09-XX"` u novom `frontend/lib/pravno.ts`.
  Ispisuje se na obje stranice („Verzija od …") i koristi je potvrda (C).
- **Poveznice:**
  - `SecurityFooter` (`TrustComponents.tsx`)
  - landing `Footer.tsx`
  - Postavke: novi blok „Pravni dokumenti" u donjem dijelu `app/postavke/page.tsx` (~:701-716) —
    Uvjeti, Izjava, Privatnost, DPA
- `/odgovornost` dodati među javne rute u `middleware.ts:50-54`.

**Nacrt Izjave o odgovornosti** (za Davidov pregled, pa odvjetnikov):

> **Izjava o odgovornosti**
> Verzija od [datum]
>
> **Što je PravoTek**
> PravoTek je pomoćni alat za izračun odvjetničkog troškovnika po Tarifi o nagradama i naknadi troškova za rad odvjetnika i Uredbi o Tarifi sudskih pristojbi. Namijenjen je odvjetnicima i odvjetničkim uredima za obavljanje njihove djelatnosti.
>
> **Što PravoTek nije**
> PravoTek nije pravni savjet. Ne zamjenjuje prosudbu odvjetnika o tome koje radnje i troškove zatražiti i kako ih opravdati.
>
> **Vaša provjera**
> Prije nego troškovnik upotrijebite u postupku, provjerite ga. Za sadržaj podneska odgovara odvjetnik koji ga podnosi. Rezultat ovisi o podacima koje unesete: vrijednosti predmeta, radnjama, datumima i oslobođenjima.
>
> **Propisi i njihove izmjene**
> Propise nastojim unijeti čim stupe na snagu, ali ne mogu jamčiti da alat u svakom trenutku prati posljednju izmjenu. Uz svaki spremljeni troškovnik zapisano je po kojoj je verziji Tarife i Uredbe izračunat i kojeg dana.
>
> **U kojoj je fazi alat**
> PravoTek je u ranoj fazi razvoja. Razvija ga jedna osoba. Izračuni su provjereni na primjerima iz prakse, ali nisu neovisno stručno revidirani. Tijekom probnog razdoblja alat se koristi bez naknade.
>
> **Odgovornost**
> Ne odgovaram za štetu nastalu zbog pogreške u izračunu ako je pogreška posljedica obične nepažnje. To ne vrijedi za štetu prouzročenu namjerno ili krajnjom nepažnjom: za nju odgovaram prema zakonu (čl. 345. Zakona o obveznim odnosima). Ne odgovaram za pogrešku koja proizlazi iz netočno unesenih podataka.
>
> **Kad nađete grešku**
> Uz svaki troškovnik stoji gumb „Iznos mi ne izgleda točno". Prijava stiže izravno meni. Možete pisati i na vackadavid@gmail.com. Svaku prijavu pregledam, a potvrđenu grešku ispravim i javim vam.
>
> **Tko pruža uslugu**
> David Vacka, fizička osoba. E-pošta: vackadavid@gmail.com.
>
> **Izmjene**
> Kad se ova izjava ili Uvjeti promijene, pri sljedećoj prijavi tražit ću da ih ponovno potvrdite.

**Otvoreno za Davida:** prvo lice („ne odgovaram") ili treće („pružatelj usluge ne odgovara").
Prvo je iskrenije za jednu osobu, treće je uobičajenije u pravnom tekstu — odvjetnik neka presudi.
Nacrt je u prvom licu.

**Izmjene Uvjeta (`/uvjeti`):**
- §1: namjena za profesionalnu upotrebu; pružatelj David Vacka, fizička osoba, e-mail
- §1.2: ispraviti NN 142/2012 u stvarnu Tarifu (HOK, 14.10.2023.). **NN broj objave Tarife provjeriti prije
  upisa** — ne upisujem ga napamet.
- §2.1: korisnicima su dostupni Troškovnik i Stranke; ostali moduli su u razvoju. AI odredbe ostaju
  uvjetno formulirane, kako je dogovoreno u prompt-pravni-tekstovi.
- §4 „Pretplata i plaćanje" → **„Naknada"**: usluga se tijekom probnog razdoblja koristi bez naknade;
  prije uvođenja naplate korisnik se obavještava najmanje 30 dana unaprijed i ponovno potvrđuje uvjete.
  Bez iznosa.
- §5 → upućivanje na Izjavu o odgovornosti; brišu se 5.3 b/d i 5.4
- §10.2 → izmjene se primjenjuju tek nakon ponovne potvrde
- Datum „Posljednje ažuriranje" zamijeniti stvarnom verzijom, i na Privatnosti/DPA uskladiti datume
- Stranica mora biti ispisiva (ZET čl. 13.) — provjeriti da `SecurityFooter` i gumbi nisu na ispisu

**Landing i aplikacija:**
- Maknuti iznose iz `Pricing.tsx`, `AnnouncementBar.tsx`, `Faq.tsx:18`, `useSubscription.ts`,
  `SubscriptionView.tsx` i `PretplataGate.tsx`. Umjesto toga: „Besplatno tijekom probnog razdoblja."
- `Faq.tsx:10` prepisati: „Kad se propis promijeni, alat se ažurira. Troškovnici spremljeni ranije ostaju
  izračunati po propisu koji je vrijedio kad su napravljeni, i to na njima piše."
- **Ne dira se** logika `PretplataGate` od 7 dana — to je zasebna odluka (vidi Dio 3).

### C. Potvrda pri prvom ulasku

**Baza** (migracija 025, dio C) — tablica `potvrde_pravnih_tekstova`, samo dodavanje:

| Stupac | Opis |
|---|---|
| `id` | uuid pk |
| `user_id` | → `auth.users`, `ON DELETE CASCADE` |
| `verzija` | text, npr. `2026-09-17` |
| `potvrdeno_at` | timestamptz, postavlja baza |
| `user_agent` | text, neobavezno |

- Unique (`user_id`, `verzija`).
- RLS: `SELECT` samo vlastiti redak (`user_id = auth.uid()`); **nema** `INSERT`/`UPDATE`/`DELETE`
  policyja za klijenta.
- Upis isključivo kroz RPC `potvrdi_pravne_tekstove(p_verzija text)`:
  - `SECURITY DEFINER`, `search_path` zaključan
  - sam uzima `auth.uid()` i `now()` — klijent ne može podmetnuti tuđi ID ni datum
  - `REVOKE` od `anon`, `GRANT` samo `authenticated` — obrazac iz 019/023
  - `ON CONFLICT DO NOTHING`
- Povijest ostaje: stara verzija se ne briše, nova se dodaje. Pri brisanju korisnika brišu se kaskadno —
  provjeriti da admin brisanje (`2839ff0`) to ne ruši.

**Sučelje** — klijentski gate po obrascu `PretplataGate`:
- Nova komponenta `components/PravnaPotvrdaGate.tsx`, montirana u `app/layout.tsx` oko `<main>`.
- Preskače: odjavljene, `/auth/*`, `/uvjeti`, `/odgovornost`, `/privatnost`, `/dpa`.
- Čita postoji li redak za `PRAVNI_TEKSTOVI_VERZIJA`. Ako ne — jedan ekran umjesto sadržaja.
  Ako da — sadržaj. Dok čeka odgovor, spinner, kao `PretplataGate`, da sadržaj ne bljesne.
- `Dashboard.tsx:270-281`: vodič se ne pali dok potvrda nije dana.
- Ovo rješava i rupu s Google registracijom: potvrdu daje svatko, bez obzira kako se registrirao.
  Kvačica na registraciji ostaje kakva jest.
- Kod pogreške mreže: ekran s „Pokušaj ponovno", ne propušta dalje. Ovo nije sigurnosna granica nego
  evidencija pristanka, pa je zadržavanje korisnika manje zlo nego propuštanje bez zapisa.

**Nacrt ekrana** (jedan ekran, bez scrolla):

> **Prije nego počnete**
>
> PravoTek je pomoćni alat za izračun troškovnika — nije pravni savjet i ne zamjenjuje vašu prosudbu.
> Svaki izračun provjerite prije nego ga upotrijebite u postupku; za sadržaj podneska odgovara odvjetnik.
> Alat ne mora u svakom trenutku pratiti posljednju izmjenu propisa, a izračuni nisu neovisno stručno revidirani.
> Uz svaki spremljeni troškovnik piše po kojem je propisu izračunat.
>
> [Pročitajte cijelu Izjavu o odgovornosti] · [Uvjeti korištenja]
>
> **[ Razumijem i nastavljam ]**

Kad se tekst promijeni, naslov postaje „Izjava o odgovornosti je izmijenjena" i ispod stoji jedna
rečenica što se promijenilo.

### D. Prijava greške u izračunu

**Baza** (migracija 025, dio D) — tablica `prijave_gresaka`:

| Stupac | Opis |
|---|---|
| `id`, `created_at` | |
| `user_id` | `ON DELETE SET NULL`, po obrascu iz 020 |
| `law_firm_id` | `ON DELETE CASCADE` |
| `troskovnik_id` | nullable, `ON DELETE SET NULL` |
| `opis` | text, obavezno, 10–2000 znakova |
| `ocekivani_iznos` | text, neobavezno („po meni bi trebalo biti …") |
| `ulazni_podaci` | jsonb — stavke, VPS, datumi, zastavice iz obrasca, **bez** podataka stranaka i odvjetnika |
| `rezultat` | jsonb — prikazani izračun |
| `verzija_izracuna` | text |
| `status` | `nova` / `potvrdena` / `nije_greska` / `ispravljena` |
| `email_poslan` | bool |

- RLS uključen, samo `service_role` (obrazac `user_mfa` iz 017). Korisnik ne čita tuđe ni vlastite
  prijave iz baze.

**Zašto `ulazni_podaci` bez stranaka:** da se greška može reproducirati, a da u tablicu i mail ne ulaze
imena i OIB-i stranaka. Uklanjaju se ključevi `odvjetnik`, `stranke`, `klijent`, `protustranka*` i
`client_*`, na frontendu i ponovno na backendu.

**Backend.**
- Novi `Backend/Controllers/PrijavaGreskeController.cs` s `POST /api/prijave-gresaka`:
  - korisnik i ured iz tokena (`HttpContext.Items["AuthUserId"]`, `this.FirmId()`)
  - validacija; ako je poslan `troskovnik_id`, provjeri da pripada uredu
  - upis kroz `SupabaseService` s `asService: true` i komentarom zašto (pravilo iz `SupabaseRequestContext.cs:14-19`)
  - zatim mail adminu kroz `IResendEmailService`
- **Mail ne smije srušiti prijavu:** zapis u bazi je izvor istine, `email_poslan` bilježi ishod.
- Sadržaj maila: tko (e-mail korisnika, za odgovor), opis, očekivani iznos, poveznica na troškovnik,
  verzija izračuna i JSON ulaza u privitku ili tijelu.
- `AdminIdentity`: dodati javni getter adrese.
- **Popraviti Resend konfiguraciju:** placeholder u `appsettings.json` tretirati kao nepostavljen
  (ili ga ukloniti), da se čita `RESEND_API_KEY`.
- **Rate limit po korisniku:** nova particionirana politika `"prijave"` u `Program.cs`
  (`PartitionedRateLimiter` po `AuthUserId`, 5 prijava na sat). Postojeće globalne politike se ne
  diraju u ovom zadatku — zaseban popravak.
- Admin: `GET /api/admin/prijave-gresaka` i `PATCH …/{id}` (status) u `AdminController.cs` — zaštićeno
  postojećim `AdminGuardMiddleware`.

**Frontend.**
- Nova `components/troskovnik/PrijavaGreskeDialog.tsx` — modal po obrascu
  `components/admin/DeleteUserDialog.tsx`: jedno obavezno polje „Što ne izgleda točno?", jedno
  neobavezno „Koliko bi po vama trebalo biti?", gumb „Pošalji".
  - Nakon slanja: „Hvala. Prijava je stigla. Odgovor šaljem na [e-mail korisnika]."
- **Gumb „Iznos mi ne izgleda točno"** — mali tekstualni gumb, `print:hidden`, samo na dva mjesta:
  - obrazac, uz redak „SVEUKUPNI TROŠAK" (`app/troskovnik/page.tsx:3584-3591`)
  - spremljeni troškovnik, ispod kartice zbroja (`app/troskovnik/[id]/page.tsx:236-238`)
- Admin: nova sekcija „Prijave grešaka" u `app/admin/page.tsx` između „Korisnici i pretplate" i
  „Developer Tools" — popis, otvaranje JSON-a, promjena statusa.

**Privatnost/DPA:** u popis svrha obrade dodati „prijave grešaka u izračunu". Ako mail ide kroz Resend,
dodati Resend među podizvršitelje u DPA (§6). Samo te dvije rečenice — ostatak je izvan opsega.

### Dio 3 — Što se NE radi

- Nema upozorenja na svakom ekranu. Ograda postoji na tri mjesta:
  - jednokratni ekran potvrde
  - Izjava o odgovornosti
  - puna bilješka uz spremljeni troškovnik i u Excelu
- Nigdje „jamči točnost", „pravno provjeren", „revidiran" — ni u aplikaciji ni na landingu. Nacrt
  izričito kaže suprotno.
- Ne prepisuju se tuđi uvjeti. Primjeri iz 1.4 služe samo kao načela.
- Ne dira se `PretplataGate` logika od 7 dana, globalni rate limiti, kvačica na registraciji ni
  `TroskovnikDocumentService.cs` (mrtav kod).
- Na `main` se ne dira ništa. Migracija ide prvo na dev bazu.

---

## 1. Datoteke po koracima

Svaki korak je zaseban zadatak i commit na `razvoj`, provjera na Vercel Preview (dev baza).

**Korak 0 — plan**
- `docs/plan-pravna-zastita.md` (novo)

**Korak 1 — uklanjanje neistina (samo tekst, bez baze)**
- `frontend/app/uvjeti/page.tsx`: §1.2, §2.1, §4, §5.3–5.4, §10.2, datum
- `frontend/app/privatnost/page.tsx`, `frontend/app/dpa/page.tsx`: datum
- `frontend/components/landing/Pricing.tsx`, `AnnouncementBar.tsx`, `Faq.tsx`
- `frontend/lib/useSubscription.ts`, `frontend/components/settings/SubscriptionView.tsx`,
  `frontend/components/PretplataGate.tsx` (tekst, ne logika)

**Korak 2 — migracija**
- `supabase/migrations/025_pravna_zastita.sql` (novo)
- `supabase/dev_bootstrap.sql` (dopisati 025 na kraj)
- `supabase/migrations/README.md` (redak o 025)

**Korak 3 — A backend**
- `Backend/Models/ClientModels.cs` (`TroskovnikRecord` zapis i čitanje)
- `Backend/Controllers/TroskovnikController.cs` (`SastaviZapis`)
- `Backend/Services/TroskovnikService.cs`, `Backend/Models/TroskovnikOdgovor.cs`
  (izložiti odabranu `TarifaVerzija`)
- Testovi: `Backend.Tests/TroskovnikControllerKarakterizacijaTests.cs` (novi slučajevi: stupci se pune;
  mješavina NN 37/2023 + NN 50/2026 daje obje Uredbe; tijelo zahtjeva ne može ih podmetnuti)

**Korak 4 — A frontend**
- `frontend/lib/troskovnik-izvoz-model.ts` (`pravnaOsnova`)
- `frontend/components/troskovnik/TroskovnikIspis.tsx` (neutralna bilješka)
- `frontend/lib/troskovnik-xlsx.ts` (puna bilješka)
- `frontend/app/troskovnik/[id]/page.tsx` (puna bilješka; stari zapisi bez verzije)
- `frontend/lib/supabase.ts` (select novih stupaca, ako se čita izravno)
- Testovi: `frontend/__tests__/troskovnik-ispis.test.tsx`, `troskovnik-izvoz.test.ts`

**Korak 5 — B tekstovi** (nacrt najprije Davidu, pa odvjetniku; tek onda objava)
- `frontend/lib/pravno.ts` (novo: verzija, kontakt)
- `frontend/app/odgovornost/page.tsx` (novo)
- `frontend/app/uvjeti/page.tsx` (§5 → upućivanje)
- `frontend/middleware.ts` (javna ruta)
- `frontend/components/TrustComponents.tsx`, `frontend/components/landing/Footer.tsx`,
  `frontend/app/postavke/page.tsx` (poveznice)

**Korak 6 — C potvrda**
- `frontend/components/PravnaPotvrdaGate.tsx` (novo)
- `frontend/lib/pravna-potvrda.ts` (novo: čitanje + RPC poziv)
- `frontend/app/layout.tsx`
- `frontend/components/Dashboard.tsx` (vodič čeka potvrdu)
- Test: `frontend/__tests__/pravna-potvrda-gate.test.tsx` (novo)

**Korak 7 — D prijava greške**
- `Backend/Controllers/PrijavaGreskeController.cs` (novo)
- `Backend/Models/PrijavaGreskeModels.cs` (novo)
- `Backend/Services/SupabaseService.cs` (`SavePrijavaGreskeAsync`)
- `Backend/Middleware/AdminIdentity.cs` (getter)
- `Backend/Services/ResendEmailService.cs`, `Backend/appsettings.json` (placeholder)
- `Backend/Program.cs` (politika „prijave")
- `Backend/Controllers/AdminController.cs` (popis, status)
- `frontend/components/troskovnik/PrijavaGreskeDialog.tsx` (novo)
- `frontend/app/troskovnik/page.tsx`, `frontend/app/troskovnik/[id]/page.tsx` (gumb)
- `frontend/app/admin/page.tsx` (sekcija)
- `frontend/app/privatnost/page.tsx`, `frontend/app/dpa/page.tsx` (svrha + Resend)
- Testovi: xUnit za kontroler (validacija, čišćenje podataka stranaka, `troskovnik_id` tuđeg ureda →
  404, mail pao → prijava ipak spremljena)

**Korak 8 — produkcija** (samo na Davidovu riječ)
- Backup prod baze → 025 na prod → merge `razvoj` u `main` → poravnati `razvoj`.
- Na Renderu (prod i dev) postaviti `RESEND_API_KEY`.

---

## 2. Migracija `025_pravna_zastita.sql`

Jedna datoteka, idempotentna („sigurna za višekratno pokretanje"), s provjerom na kraju — obrazac
numeriranih migracija 015–024.

```sql
-- ── A. Verzija propisa uz troškovnik ─────────────────────────────
ALTER TABLE public.troskovnici
  ADD COLUMN IF NOT EXISTS izracunato_at     timestamptz,
  ADD COLUMN IF NOT EXISTS tarifa_naziv      text,
  ADD COLUMN IF NOT EXISTS tarifa_vrijedi_od date,
  ADD COLUMN IF NOT EXISTS vrijednost_boda   numeric(10,2),
  ADD COLUMN IF NOT EXISTS uredbe_pristojbe  text[],
  ADD COLUMN IF NOT EXISTS verzija_izracuna  text;
COMMENT ON COLUMN public.troskovnici.tarifa_naziv IS
  'Naziv verzije Tarife po kojoj je izračun napravljen. Puni backend pri spremanju. NULL = spremljeno prije 025.';
-- (bez backfilla: stari zapisi ostaju NULL i tako se i prikazuju)

-- ── C. Evidencija potvrde pravnih tekstova ───────────────────────
CREATE TABLE IF NOT EXISTS public.potvrde_pravnih_tekstova (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id      uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  verzija      text NOT NULL CHECK (length(verzija) BETWEEN 1 AND 40),
  potvrdeno_at timestamptz NOT NULL DEFAULT now(),
  user_agent   text,
  UNIQUE (user_id, verzija)
);
ALTER TABLE public.potvrde_pravnih_tekstova ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS potvrde_select_own ON public.potvrde_pravnih_tekstova;
CREATE POLICY potvrde_select_own ON public.potvrde_pravnih_tekstova
  FOR SELECT TO authenticated USING (user_id = auth.uid());
-- nema INSERT/UPDATE/DELETE policyja: upis samo kroz RPC

CREATE OR REPLACE FUNCTION public.potvrdi_pravne_tekstove(p_verzija text, p_user_agent text DEFAULT NULL)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Nije prijavljen'; END IF;
  INSERT INTO public.potvrde_pravnih_tekstova (user_id, verzija, user_agent)
  VALUES (auth.uid(), p_verzija, left(p_user_agent, 300))
  ON CONFLICT (user_id, verzija) DO NOTHING;
END $$;
REVOKE ALL ON FUNCTION public.potvrdi_pravne_tekstove(text, text) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.potvrdi_pravne_tekstove(text, text) TO authenticated;

-- ── D. Prijave grešaka u izračunu ────────────────────────────────
CREATE TABLE IF NOT EXISTS public.prijave_gresaka (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  created_at       timestamptz NOT NULL DEFAULT now(),
  user_id          uuid REFERENCES auth.users(id) ON DELETE SET NULL,
  law_firm_id      uuid NOT NULL REFERENCES public.law_firms(id) ON DELETE CASCADE,
  troskovnik_id    uuid REFERENCES public.troskovnici(id) ON DELETE SET NULL,
  opis             text NOT NULL CHECK (length(opis) BETWEEN 10 AND 2000),
  ocekivani_iznos  text CHECK (ocekivani_iznos IS NULL OR length(ocekivani_iznos) <= 200),
  ulazni_podaci    jsonb,
  rezultat         jsonb,
  verzija_izracuna text,
  status           text NOT NULL DEFAULT 'nova'
                   CHECK (status IN ('nova','potvrdena','nije_greska','ispravljena')),
  email_poslan     boolean NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS prijave_gresaka_status_created_idx
  ON public.prijave_gresaka (status, created_at DESC);
ALTER TABLE public.prijave_gresaka ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS service_role_full_access ON public.prijave_gresaka;
CREATE POLICY service_role_full_access ON public.prijave_gresaka
  USING (auth.role() = 'service_role') WITH CHECK (auth.role() = 'service_role');

-- ── Provjera ─────────────────────────────────────────────────────
-- SELECT column_name FROM information_schema.columns WHERE table_name='troskovnici' AND column_name IN (...);
-- SELECT relrowsecurity FROM pg_class WHERE relname IN ('potvrde_pravnih_tekstova','prijave_gresaka');
-- SELECT has_function_privilege('anon','public.potvrdi_pravne_tekstove(text,text)','EXECUTE'); -- mora biti false
```

Redoslijed: **dev baza** (`zxerpqxheysobuqnouyv`) → provjera + `get_advisors` → backup prod → prod.
Provjeriti da admin brisanje korisnika (`AdminController` DELETE) prolazi s novim FK-ovima.

---

## 3. Prije prvih korisnika vs. kasnije

**Prije nego pustiš prve korisnike (obavezno):**
1. **Korak 1 — uklanjanje neistina.** Najjeftinije, a najveći rizik: objavljene cijene bez pravnog oblika,
   „izmjena propisa ne traži novu verziju", ograničenje na 0 €, NN 142/2012.
2. **Korak 2–4 — verzija propisa uz troškovnik i bilješke.** Jedino što korisniku stvarno koristi. Svaki
   troškovnik spremljen prije toga zauvijek ostaje bez oznake — zato što ranije.
3. **Korak 5 — Izjava o odgovornosti i prepravljeni Uvjeti**, barem u verziji koju je pročitao David.
4. **Korak 6 — potvrda.** Bez nje nema dokaza da je korisnik vidio uvjete (čl. 295. st. 5), a Google
   registracija ih danas potpuno zaobilazi.
5. **Korak 7 — prijava greške**, barem spremanje u bazu i popis u adminu. Mail je poželjan, ali ako
   Resend (ključ + verificirana domena pošiljatelja) nije spreman, prijave se ne gube.

**Može kasnije:**
- Pregled odvjetnika (vidi 4). Idealno prije šireg otvaranja. Za prvih nekoliko poznatih testera
  dovoljan je Davidov pregled + iskren tekst, a nakon pregleda nova verzija i ponovna potvrda —
  mehanizam iz C to i omogućuje.
- Popravak globalnih rate limitera (`Program.cs`) za ostale endpointe.
- Naknadno popunjavanje verzije za stare troškovnike iz `raw_json_output` (samo gdje je jednoznačno).
- Spajanje kvačice na registraciji i potvrde u jedan tok.
- Obavijest korisniku kad je njegova prijava riješena (danas ručno, mailom).
- Status, obrt/društvo i porez — **mora biti riješeno prije uključivanja naplate**, ne prije prvih
  besplatnih korisnika. Pitanje otvaranja obrta/društva je za računovođu.

**Davidove radnje (ja ih ne mogu):**
- `RESEND_API_KEY` na Renderu (dev i prod) i verificirana domena pošiljatelja u Resendu.
- Paste migracije u prod SQL Editor nakon backupa.
- Odluka o `PretplataGate` od 7 dana: s „besplatno do daljnjega" u tekstu, zaključavanje nakon 7 dana
  treba ili ostati kao „proba koju ručno produljujem" (onda to piše u Uvjetima), ili se produljiti.

---

## 4. Što dati odvjetniku na pregled i koliko košta

**Paket za pregled:**
1. Izjava o odgovornosti (nacrt iz B) — **glavni dokument**
2. Uvjeti korištenja (prepravljeni)
3. Tekst ekrana potvrde i tri bilješke (aplikacija, Excel, ispis)
4. Politika privatnosti i DPA — samo izmijenjeni dijelovi, i dosad nepregledane tvrdnje (AES-256,
   „nepromjenjivi dnevnik", 11 godina čuvanja računa, podizvršitelji)

**Konkretna pitanja** (da se ne plaća opći pregled):
1. Drži li isključenje odgovornosti za običnu nepažnju u općim uvjetima prema odvjetniku (ZOO čl. 345.
   st. 2 i čl. 296.)? Treba li ipak i najviši iznos naknade (čl. 345. st. 3) i koliki?
2. Je li odnos tijekom besplatnog razdoblja ugovorni? Pokriva li isključenje i izvanugovorni zahtjev
   (čl. 1045.)?
3. Je li odvjetnik koji koristi alat za ured sigurno izvan ZZP-a? Što s vježbenikom ili studentom koji
   se registrira?
4. ZET čl. 6.: je li za fizičku osobu bez obrta dovoljna e-adresa ili treba i poštanska adresa? Je li
   besplatna usluga uopće „usluga informacijskog društva" (čl. 2. t. 2)?
5. Smije li fizička osoba bez registrirane djelatnosti javno nuditi besplatnu uslugu koja se najavljuje
   kao buduća plaćena? Kad najkasnije mora nastati obrt ili društvo?
6. Neutralna bilješka na ispisu za sud — ima li prigovora struke ili suda na navođenje pravne osnove i
   datuma?
7. Izjava u prvom ili trećem licu?
8. Je li potvrda (klik + zapis verzije i vremena u bazi) dovoljan dokaz prihvata općih uvjeta?

**Cijena — gruba procjena, nije iz propisa:**
- Pregled 3–4 kratka dokumenta s gornjim pitanjima: **otprilike 3–6 sati rada**.
- Na hrvatskom tržištu to je **otprilike 300–1.000 € + PDV**.
- **Traži fiksnu cijenu unaprijed**, uz pitanja iz popisa.
- Moguće jeftinije: odvjetnica koja je već pregledala izračun (Zvjezdana) — pitati je li voljna ili
  preporučuje kolegu za ugovorno i potrošačko pravo.
- Ne preporučujem „sastavljanje od nule" — skuplje je, a nacrt već postoji.

---

## Provjera (za svaki korak)

- **Build/testovi:**
  - `dotnet test` (Backend.Tests)
  - `npm run test` (Vitest), `npm run lint`, `npx tsc --noEmit`, `npm run build`
  - Build u worktreeu treba `.env.local` i TLS flag — vidi memoriju „Build frontenda u worktreeu".
- **Pretraga** korisniku vidljivog teksta u `frontend/` (bez `node_modules`, `.next`, zip mape):
  - `19 €`, `190 €`, `pretplat`, `jamči točn`, `pravno provjeren`, `d.o.o.` (osim placeholdera),
    `NN 142/2012` — nula pogodaka.
- **A (Preview, dev baza):**
  - Spremi troškovnik → SQL na dev bazi: stupci popunjeni. Troškovnik s odgovorom na tužbu prije
    1.6.2026. ima obje Uredbe u `uredbe_pristojbe`.
  - Stari zapis prikazuje „Propis nije zabilježen".
  - Ispis (Ctrl+P): neutralna bilješka postoji, nema „PravoTek" ni URL-a u margini.
  - Excel: puna bilješka.
- **C:**
  - Bacački račun (e-mail i Google) → ekran potvrde prije dashboarda i prije vodiča → klik →
    redak u `potvrde_pravnih_tekstova`.
  - Povećati `PRAVNI_TEKSTOVI_VERZIJA` → ekran se vraća, a stari redak ostaje.
  - `/uvjeti` i `/odgovornost` čitljivi bez potvrde.
  - RPC s anon ključem → odbijen.
- **D:**
  - Prijava s obrasca i sa spremljenog zapisa → redak u `prijave_gresaka` bez imena i OIB-a stranaka;
    mail stigne.
  - 6. prijava u sat vremena → 429 samo tom korisniku.
  - Pogrešan `RESEND_API_KEY` → prijava spremljena, `email_poslan = false`.
  - Admin vidi popis i mijenja status.
- **Snimke:** desktop + mobilni (375 px) za ekran potvrde, dijalog prijave i bilješku uz spremljeni
  troškovnik.

## Izvori

- Zakon o obveznim odnosima, pročišćeni tekst: https://www.zakon.hr/z/75/zakon-o-obveznim-odnosima — čl. 14., 295., 296., 345., 346., 347., 1045.
- ZOO, urednički pročišćeni izvadak (RRiF): https://www.rrif.hr/dok/propisi/trgovina/3.PDF
- Zakon o zaštiti potrošača: https://www.zakon.hr/z/193/zakon-o-zaštiti-potrošača — čl. 4. t. 28 i 47, čl. 10., 53., 60., 79.
- Zakon o elektroničkoj trgovini: https://www.zakon.hr/z/199/zakon-o-elektronickoj-trgovini — čl. 2., 6., 12., 13. (**provjeriti je li NN 67/25 mijenjao čl. 6.** — tekst koji sam čitao nema oznake izmjena po članku)
- Tarifo, kalkulator: https://tarifo.net/hr/calculator/hr/
- DAV-Prozesskostenrechner: https://anwaltsblatt.anwaltverein.de/de/apps/prozesskostenrechner
- Deutschland-Rechner RVG (prema rezultatima pretrage, stranica nije izravno otvorena): https://www.deutschland-rechner.de/rvg-rechner
- TurboTax jamstva: https://turbotax.intuit.com/corp/guarantees/
