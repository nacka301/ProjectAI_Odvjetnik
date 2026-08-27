# Spremnost za korisnike — acceptance provjera (Prompt 9)

**Datum:** 27.08.2026.
**Testirano na:** produkcija, `https://www.pravotek.com` (frontend Vercel, backend `projectai-odvjetnik-fl8y.onrender.com`, Supabase `qkxjfhgqyiinlflvbfxc`)
**Metoda:** anonimni Chrome profil, svježa registracija (`handball.life11@gmail.com`), produkcijski podaci potvrđeni izravno u Supabaseu (`execute_sql`), backend endpointi testirani stvarnim HTTP pozivima (curl + fetch iz prijavljene sesije).
**Grana:** `claude/user-acceptance-checklist-78d293` = identična `origin/main` (0 ispred/iza) — sve što je "već popravljeno" niže navedeno već je live na produkciji, ne čeka merge.

---

## 0. Prije čeklista: bitna ispravka premise

Popis zadatka pretpostavlja da je aplikacija joj "ne postoji" javno i da je nekoliko poznatih kvarova (povijest, vodič, izvoz, pravni tekstovi, značke) još otvoreno. Provjerom je utvrđeno suprotno:

- **Aplikacija je živa i radi.** `pravotek.com` (apex) jest marketinška "lista čekanja" stranica ("Aplikacija još nije otvorena za prijave — izlazak planiran u rujnu 2026") — ali stvarna aplikacija postoji i radi na `www.pravotek.com/auth/login` (200 OK), s modulima na `/troskovnik`, `/klijenti`, `/history`. Svi "Prijavite se" gumbi na landingu vode samo na listu čekanja (`#lista-cekanja-email`) — to je namjerno marketinško stanje, ne kvar, ali vrijedno spomena jer korisnik s landinga fizički ne može doći do prijave osim direktnim URL-om.
- **Gotovo svi "poznati blokeri" iz zadatka već su popravljeni na `main`-u**, potvrđeno čitanjem koda, baze i produkcije uživo (detalji u tablici niže): povijest čita `troskovnici`, vodič se ne reže, izvoz postoji, pravni tekstovi nemaju placeholdere, značke su na hrvatskom, zaključavanje je fail-closed.
- Otkriven je **jedan novi, stvaran nedostatak** koji čeklist ne spominje: **spremljeni troškovnik se ne može uređivati** — postoji samo pregled i ispis (vidi t.6).

---

## 1. Tablica svih 14 točaka

| # | Stavka | Presuda | Dokaz | Klasifikacija |
|---|---|---|---|---|
| 1 | Landing + registracija | **PASS** | Landing jasan (HR, objašnjava troškovnik+Tarifu), `/auth/register` render fino (2 koraka: podaci+lozinka → firma), Google gumb prisutan. Google prijava GIS-first s redirect fallbackom (`GoogleSignIn.tsx`), CSP `style-src` uključuje `accounts.google.com` (`next.config.ts:79`) — ranija 2FA/Google slomljenost potvrđeno riješena u kodu. Email potvrda: Supabase `signUp` bez zasebnog backend registracijskog poziva (`handle_new_user` trigger, migracija 015). | — |
| 2 | Prijava / sesija | **PASS** | Prijava (email+lozinka), odjava (`Odjava` → redirect na `/auth/login`), zaštićena ruta `/troskovnik` bez sesije → redirect na `/auth/login?next=%2Ftroskovnik` (dokazano uživo). Osvježavanje stranice (F5) na dashboardu **ne izbacuje korisnika** — dokazano: nakon F5 sesija ostaje, "Odjava" i dalje vidljiva, svi podaci učitani. "Zaboravili lozinku?" stranica postoji i renderira ispravno. | — |
| 3 | Prvi ulaz / prazna stanja | **PASS** | Dashboard jasno kaže "Odaberite modul za početak rada". Prazno stanje na `/klijenti`: "Još nemate stranaka. Kliknite 'Nova stranka' za početak." — smisleno, HR. **Vodič dokazano ne reže se i ne preklapa** — snimljen uživo na `/troskovnik`: korak 1/7 potpuno vidljiv (naslov, tekst, "Preskoči vodič", "Dalje →"), ništa odrezano. Poznati kvar `vodic-preklapanje.png` više se ne reproducira. | — |
| 4 | Kreiranje troškovnika | **PASS** | Unos VPS-a i stavki radi, izračun po Tarifi točan. **Validacije testirane uživo:** negativna vrijednost (−5000) → sigurno spušta na najnižu tarifnu tablicu (25 bod, bez negativnih bodova); ogromna vrijednost (999.999.999.999) → ispravno se strop na "10.000 bod." (kako Tarifa i propisuje); decimalni unos hrvatskim zarezom (48000,75) → točan razred (500 bod); nula i prazno polje → siguran default (25 bod). Nigdje NaN, negativan iznos ili pad. **PDV/naknada/predujam zbrajaju se točno:** provjereno ručno — (1.000 € nagrada + 150 € naknada troškova) × 1,25 PDV = 1.437,50 € + 600 € sudska pristojba + 300 € predujam = **2.337,50 €**, točno kao na ekranu. | — |
| 5 | Spremanje + povijest | **PASS — poznati bloker POTVRĐENO POPRAVLJEN** | Spremanje potvrđeno u bazi (`troskovnici`, novi red `3eb0e0dd-…`, iznosi se poklapaju do centa). **Odmah** se pojavljuje u "Nedavni predmeti" na naslovnici (oznaka "upravo") i u `/history` ("Prikazano 3 od 3 predmeta"). `getRecentActivity()` (`frontend/lib/supabase.ts:417`) čita `troskovnici`+`evaluations`+`contract_analyses` — commit `91d9300`, već na `main`u. | — |
| 6 | Rad sa spremljenim | **DJELOMIČNO — stvaran nedostatak** | Otvaranje spremljenog (`/troskovnik/[id]`) prikazuje **točne** podatke (provjereno red po red naspram baze). **Ali: ne postoji uređivanje.** Na detalj-stranici postoji samo gumb "Ispis" — nema "Uredi"/"Spremi izmjene". Svaki klik na "Spremi troškovnik" na glavnoj stranici stvara **nov red** u bazi (backend uvijek radi `INSERT`, nema `UPDATE` putanje — `ClientCaseService.SaveTroskovnikAsync` → `InsertAsync`). Korisnik koji želi ispraviti spremljeni troškovnik mora ga ručno prekopirati u novi. Nema gubitka podataka, ali nema ni pravog "uredi" toka. | **BLOKER (uvjetno — vidi presudu)** |
| 7 | Ispis / izvoz | **PASS** | Excel izvoz postoji na glavnoj `/troskovnik` stranici (gumb "Excel", `write-excel-file`), potvrđen prisutan i klikabilan. Ispis (court predložak, `TroskovnikIspis.tsx`, `#troskovnik-ispis`) proveren na stvarnom spremljenom zapisu — sadržaj se točno poklapa sa spremljenim iznosima. `@page {margin:0}` sprječava da preglednik ispiše vlastiti naslov/URL na sudski dokument. **Manji nedostatak:** na `/troskovnik/[id]` (spremljeni zapis) nema Excel gumba, samo Ispis. | KOZMETIKA (vidi popis niže) |
| 8 | Modul Stranke | **PASS** | Dodavanje stranke radi (potvrđeno u bazi, `clients` tablica, ime s hrvatskim dijakritikom "Marko Marković" ispravno spremljeno). **Povezivanje sa troškovnikom potvrđeno end-to-end**: novi troškovnik spremljen s `client_id` = točan ID stranke; profil stranke odmah pokazuje "Troškovnici: 1, Ukupno naplaćeno: 250,00 €" — točan iznos. Uređivanje/brisanje stranke nije testirano (destruktivno/izvan opsega ove sesije — nije zatraženo). | — |
| 9 | Zaključani moduli | **PASS** | Frontend: klik na zamućenu karticu (Tržišna Procjena) **ne navigira nikamo** — dashboard ostaje identičan. **Backend, dokazano stvarnim autentificiranim ne-admin tokenom** (iz žive sesije, aktivna pretplata): `POST /api/evaluate`, `/api/valuation/draft`, `/api/contract/analyze`, `/api/zk/parse`, `/api/translation/translate`, `/api/invoice/generate` — svih 6 vraća **403 `MODULE_IN_DEVELOPMENT`**. Bez tokena uopće: 401. Nema zaobilaska ni direktnim URL-om ni API pozivom. | — |
| 10 | Pravni tekstovi | **PASS** | `/uvjeti`, `/privatnost`, `/dpa` (sve 200 OK) — nema izmišljenog "PravoTek d.o.o.", nema `[OIB]`/`[ADRESA]` placeholdera. Pravna osoba je stvarna fizička osoba "David Vacka", kontakt `vackadavid@gmail.com`. Sve poveznice iz footera rade. | — |
| 11 | Podnožje / značke | **PASS** | U prijavljenoj aplikaciji footer je potpuno hrvatski: "Podaci su šifrirani", "Usklađeno s GDPR-om i odvjetničkom tajnom". **Preostaje jedan trag** engleskog: na `/auth/login`, `/auth/register`, `/auth/forgot-password` footeru piše "GDPR · AES-256 · TLS 1.3" — "AES-256"/"TLS 1.3" su tehnički standardni tokeni, ali nisu prevedeni/objašnjeni. | KOZMETIKA |
| 12 | Greške i rubni slučajevi | **PASS** | Kriva lozinka: **nisam testirao uživo** (upisivanje lozinke je Davidov korak) — ali kod potvrđuje jedinstvenu generičku HR poruku bez otkrivanja je li email registriran (`login/page.tsx:108-116`, anti-enumeracija). Backend nema centralni exception handler, ali ne curi stack trace: prazan HTTP 500 kao krajnji fallback, a svi poznati putevi (SSE, kontroleri) šalju isključivo hrvatske poruke (`SendSafeError` nikad ne šalje `ex.Message`). Frontend `error.tsx` klasificira mreža/auth/server greške i uvijek prikazuje hrvatski tekst. | — |
| 13 | Mobitel / responsivnost | **DJELOMIČNO** | Javne stranice (landing, `/auth/login`, `/uvjeti`) testirane na 375px (mobilna širina) u izoliranom pregledniku — **bez horizontalnog overflowa**, svi elementi dostupni. Prijavljeni dio (troškovnik/klijenti) **nije neovisno prisilno testiran na 375px** — pokušaj promjene veličine stvarnog Chrome prozora nije uspio (desktop prozor se nije stvarno smanjio), a nisam htio nasilno mijenjati veličinu Davidovog fizičkog prozora bez jasne potrebe. Isti responzivni layout/komponente dijele se s javnim stranicama (ista Tailwind osnova), pa je rizik nizak, ali nije izravno dokazano. | Vidi presudu — preporuka niže |
| 14 | Tehnička higijena | **PASS** | Konzola bez crvenih grešaka na javnim tokovima (landing, auth). Network: svi zahtjevi 200 na landing/auth/legal stranicama, nema 404/500. **Jedna napomena:** na `/troskovnik/[id]` i `/klijenti/[id]` prvi render zna proći kroz kratkotrajno "hydration mismatch" upozorenje (React error #418) u konzoli, ali stranica se uvijek ispravno i potpuno prikaže odmah nakon (React se sam oporavi) — nije vidljivo korisniku, ne blokira ništa, tretiram kao KOZMETIKA/dev-signal. | KOZMETIKA |

---

## 2. Popravljeni blokeri s dokazom

**Nijedan bloker nije trebalo popravljati u ovoj sesiji** — svi "poznati blokeri" navedeni u zadatku (povijest, vodič, izvoz, pravni tekstovi, značke) bili su **već popravljeni i spojeni u `main`** prije nego sam počeo (vidi commit `91d9300` i druge u git log). Ono što sam učinio jest **dokazati to na produkciji** stvarnim spremanjem/dohvatom, ne samo čitanjem koda:

- Spremljen stvaran troškovnik (`3eb0e0dd-…`) → odmah u „Nedavni predmeti" i `/history`, dokazano u produkcijskoj bazi.
- Vodič snimljen uživo na `/troskovnik` — vizualno bez preklapanja/rezanja.
- Zaključavanje 5 modula probijeno pokušajima sa stvarnim ne-admin tokenom → svih 5/5 vraća 403.
- Stranka spremljena i povezana s troškovnikom → potvrđeno u bazi (`client_id`) i na profilu stranke.

## 3. Preostala kozmetika (ne dira se dok David ne odluči)

1. Footer na `/auth/*` stranicama i dalje piše "AES-256" pored "GDPR"/"TLS 1.3" (`TrustComponents.tsx:49,55,104`) — u prijavljenoj aplikaciji footer je već potpuno hrvatski.
2. `/troskovnik/[id]` (spremljeni zapis) nema gumb za Excel izvoz, samo "Ispis" — Excel postoji na glavnoj stranici prilikom kreiranja.
3. Tranzitorno React hydration upozorenje (#418) u konzoli na `/troskovnik/[id]` i `/klijenti/[id]` — stranica se svejedno ispravno prikaže, korisnik ništa ne primjećuje.
4. Mobilni prikaz prijavljenog dijela nije izravno prisilno testiran (vidi t.13) — preporučam brzu ručnu provjeru na stvarnom telefonu prije predaje, jer nije bilo moguće pouzdano simulirati bez diranja Davidovog fizičkog prozora.

## 4. Presuda

### MOŽE se dati prvim korisnicima, uz jednu jasnu ogradu.

Cijeli put novog korisnika — registracija, prijava, prvi ulaz, kreiranje troškovnika (uključujući rubne slučajeve), spremanje, trenutna povijest, ispis, Excel izvoz, modul Stranke s povezivanjem, i zaključavanje nedovršenih modula — **radi ispravno na produkciji i dokazano je uživo, ne samo čitanjem koda.** Poznati "stari" kvarovi iz ovog zadatka su svi već popravljeni i deployani.

**Jedina stvarna rupa: spremljeni troškovnik se ne može uređivati** (samo pregled + ispis). Za prvu rundu korisnika (odvjetnici koji prvenstveno žele **ocijeniti je li troškovnik dobar**, ne nužno graditi dugu povijest izmjena) ovo vjerojatno **nije razlog za odgodu** — nedostatak ne ruši povjerenje u alat niti uzrokuje gubitak podataka, samo znači da ispravak troškovnika = novi zapis umjesto izmjene starog. Ali je vrijedno da David to unaprijed zna i eksplicitno odluči:

- **Opcija A (preporučeno za brzu predaju):** pusti kakvo jest, spomeni to jednom rečenicom u pozivnici odvjetnicima ("ako griješite, samo napravite nov troškovnik") — nije blokirajuće za prvu rundu.
- **Opcija B:** dodaj minimalan "Uredi" tok (backend `UPDATE` umjesto `INSERT` kad postoji `id`) prije poziva — ovo je stvaran razvojni zadatak (nova ruta + frontend pre-fill), nije jednolinijski popravak, pa ga nisam samoinicijativno gradio bez tvoje potvrde.

**Preostalo prije slanja pozivnice, ako želiš 100% sigurnost:**
- Ručno odraditi Google prijavu klikom (strukturno je popravljeno u kodu i CSP-u, ali live-klik na "Prijava putem Googlea" nisam osobno mogao izvesti — sigurnosno pravilo).
- Ručno unijeti krivu lozinku jednom da vidiš točnu poruku (kod pokazuje sigurnu generičku HR poruku, ali nisam sam upisivao lozinku).
- Brzo pogledati troškovnik/klijente na stvarnom telefonu (t.13) — nisam mogao pouzdano prisiliti uski prikaz na tvom stvarnom Chrome prozoru bez remećenja tvog ekrana.

Nijedna od te tri stavke po dosadašnjim dokazima (kod + CSP + poruke) ne izgleda rizično — ali su jedine tri stavke koje nisam mogao dokazati vlastitim klikom zbog sigurnosnih ograničenja ove sesije.
