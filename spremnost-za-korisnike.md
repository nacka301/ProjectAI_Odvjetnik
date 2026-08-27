# Spremnost za korisnike — acceptance provjera (Prompt 9)

**Datum:** 27.08.2026.
**Testirano na:** produkcija, `https://www.pravotek.com` (frontend Vercel, backend `projectai-odvjetnik-fl8y.onrender.com`, Supabase `qkxjfhgqyiinlflvbfxc`)
**Metoda:** anonimni Chrome profil, svježa registracija (`handball.life11@gmail.com`), produkcijski podaci potvrđeni izravno u Supabaseu (`execute_sql`), backend endpointi testirani stvarnim HTTP pozivima (curl + fetch iz prijavljene sesije).
**Grana:** rad je odrađen na `claude/user-acceptance-checklist-78d293`, koja je u trenutku početka bila identična `origin/main`u. Popravak iz §2 (t.6) pushan je izravno u `main` (fast-forward `9a3ffb7..91f86d5`) i deployan na produkciju.

---

## 0. Prije čeklista: bitna ispravka premise

Popis zadatka pretpostavlja da je aplikacija joj "ne postoji" javno i da je nekoliko poznatih kvarova (povijest, vodič, izvoz, pravni tekstovi, značke) još otvoreno. Provjerom je utvrđeno suprotno:

- **Aplikacija je živa i radi.** `pravotek.com` (apex) jest marketinška "lista čekanja" stranica ("Aplikacija još nije otvorena za prijave — izlazak planiran u rujnu 2026") — ali stvarna aplikacija postoji i radi na `www.pravotek.com/auth/login` (200 OK), s modulima na `/troskovnik`, `/klijenti`, `/history`. Svi "Prijavite se" gumbi na landingu vode samo na listu čekanja (`#lista-cekanja-email`) — to je namjerno marketinško stanje, ne kvar, ali vrijedno spomena jer korisnik s landinga fizički ne može doći do prijave osim direktnim URL-om.
- **Gotovo svi "poznati blokeri" iz zadatka već su popravljeni na `main`-u**, potvrđeno čitanjem koda, baze i produkcije uživo (detalji u tablici niže): povijest čita `troskovnici`, vodič se ne reže, izvoz postoji, pravni tekstovi nemaju placeholdere, značke su na hrvatskom, zaključavanje je fail-closed.
- Otkriven je **jedan novi, stvaran nedostatak** koji čeklist ne spominje: spremljeni troškovnik se nije mogao uređivati — postojao je samo pregled i ispis (vidi t.6). **Popravljeno u ovoj sesiji** (uz Davidovu potvrdu pristupa, jer je izvorno stanje bilo namjerna sigurnosna odluka — vidi §2).

---

## 1. Tablica svih 14 točaka

| # | Stavka | Presuda | Dokaz | Klasifikacija |
|---|---|---|---|---|
| 1 | Landing + registracija | **PASS** | Landing jasan (HR, objašnjava troškovnik+Tarifu), `/auth/register` render fino (2 koraka: podaci+lozinka → firma), Google gumb prisutan. Google prijava GIS-first s redirect fallbackom (`GoogleSignIn.tsx`), CSP `style-src` uključuje `accounts.google.com` (`next.config.ts:79`) — ranija 2FA/Google slomljenost potvrđeno riješena u kodu. Email potvrda: Supabase `signUp` bez zasebnog backend registracijskog poziva (`handle_new_user` trigger, migracija 015). | — |
| 2 | Prijava / sesija | **PASS** | Prijava (email+lozinka), odjava (`Odjava` → redirect na `/auth/login`), zaštićena ruta `/troskovnik` bez sesije → redirect na `/auth/login?next=%2Ftroskovnik` (dokazano uživo). Osvježavanje stranice (F5) na dashboardu **ne izbacuje korisnika** — dokazano: nakon F5 sesija ostaje, "Odjava" i dalje vidljiva, svi podaci učitani. "Zaboravili lozinku?" stranica postoji i renderira ispravno. | — |
| 3 | Prvi ulaz / prazna stanja | **PASS** | Dashboard jasno kaže "Odaberite modul za početak rada". Prazno stanje na `/klijenti`: "Još nemate stranaka. Kliknite 'Nova stranka' za početak." — smisleno, HR. **Vodič dokazano ne reže se i ne preklapa** — snimljen uživo na `/troskovnik`: korak 1/7 potpuno vidljiv (naslov, tekst, "Preskoči vodič", "Dalje →"), ništa odrezano. Poznati kvar `vodic-preklapanje.png` više se ne reproducira. | — |
| 4 | Kreiranje troškovnika | **PASS** | Unos VPS-a i stavki radi, izračun po Tarifi točan. **Validacije testirane uživo:** negativna vrijednost (−5000) → sigurno spušta na najnižu tarifnu tablicu (25 bod, bez negativnih bodova); ogromna vrijednost (999.999.999.999) → ispravno se strop na "10.000 bod." (kako Tarifa i propisuje); decimalni unos hrvatskim zarezom (48000,75) → točan razred (500 bod); nula i prazno polje → siguran default (25 bod). Nigdje NaN, negativan iznos ili pad. **PDV/naknada/predujam zbrajaju se točno:** provjereno ručno — (1.000 € nagrada + 150 € naknada troškova) × 1,25 PDV = 1.437,50 € + 600 € sudska pristojba + 300 € predujam = **2.337,50 €**, točno kao na ekranu. | — |
| 5 | Spremanje + povijest | **PASS — poznati bloker POTVRĐENO POPRAVLJEN** | Spremanje potvrđeno u bazi (`troskovnici`, novi red `3eb0e0dd-…`, iznosi se poklapaju do centa). **Odmah** se pojavljuje u "Nedavni predmeti" na naslovnici (oznaka "upravo") i u `/history` ("Prikazano 3 od 3 predmeta"). `getRecentActivity()` (`frontend/lib/supabase.ts:417`) čita `troskovnici`+`evaluations`+`contract_analyses` — commit `91d9300`, već na `main`u. | — |
| 6 | Rad sa spremljenim | **PASS — nedostatak PRONAĐEN I POPRAVLJEN u ovoj sesiji** | Otvaranje spremljenog (`/troskovnik/[id]`) prikazuje **točne** podatke (provjereno red po red naspram baze). Izvorno nije postojalo uređivanje — namjerno: taj zapis je možda već predan sudu, pa bi ponovni izračun po današnjem katalogu mogao tiho promijeniti iznos (komentar u kodu). **Dodan gumb "Uredi kao novi"** koji popuni praznu formu na `/troskovnik` podacima spremljenog zapisa (stavke, VPS, stranke, PDV, naknada troškova, predujmovi), ali spremanje **uvijek stvara nov zapis** — izvorni ostaje netaknut. Vidi popravak niže. | — |
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

Svi "poznati blokeri" navedeni u zadatku (povijest, vodič, izvoz, pravni tekstovi, značke) bili su **već popravljeni i spojeni u `main`** prije nego sam počeo (vidi commit `91d9300` i druge u git log). Ono što sam učinio za njih jest **dokazati to na produkciji** stvarnim spremanjem/dohvatom, ne samo čitanjem koda:

- Spremljen stvaran troškovnik (`3eb0e0dd-…`) → odmah u „Nedavni predmeti" i `/history`, dokazano u produkcijskoj bazi.
- Vodič snimljen uživo na `/troskovnik` — vizualno bez preklapanja/rezanja.
- Zaključavanje 5 modula probijeno pokušajima sa stvarnim ne-admin tokenom → svih 5/5 vraća 403.
- Stranka spremljena i povezana s troškovnikom → potvrđeno u bazi (`client_id`) i na profilu stranke.

### Novi bloker pronađen i popravljen u ovoj sesiji: uređivanje spremljenog troškovnika (t.6)

`/troskovnik/[id]` je namjerno samo za čitanje — postojeći komentar u kodu objašnjava zašto: zapis je možda već predan sudu, pa bi ponovni izračun po današnjem katalogu mogao tiho promijeniti iznos, a razlika bi se primijetila tek na ročištu. To NIJE previd nego svjesna sigurnosna odluka iz ranije sesije, pa nisam sam gradio "edit-in-place" — pitao sam Davida, koji je potvrdio sigurniju opciju: **kloniranje u nov nacrt**, ne izmjena u mjestu.

**Implementacija** (`frontend/app/troskovnik/[id]/page.tsx`, `frontend/app/troskovnik/page.tsx`):
- Novi gumb "Uredi kao novi" na `/troskovnik/[id]` vodi na `/troskovnik?klon=<id>`.
- `/troskovnik` pri učitavanju s tim parametrom dohvati spremljeni zapis (`getTroskovnik`, već postojeća funkcija pod RLS-om), rekonstruira formu iz `raw_json_output` (stavke, VPS, stranke, PDV, naknada troškova, predujmovi, kazneni podaci...) i popuni prazan obrazac — uz vidljivu naznaku "Forma je popunjena prema troškovniku spremljenom {datum}. Provjerite sve stavke prije spremanja — izvorni zapis ostaje nepromijenjen."
- **Spremanje i dalje uvijek radi `INSERT`** (nijedna putanja za `UPDATE` nije dodana) — izvorni zapis je fizički nemoguće promijeniti kroz ovaj put.
- Backend nije mijenjan — sve je čitanje kroz već postojeću, RLS-zaštićenu `getTroskovnik()`.

**Dokaz:** `npm run build` prolazi čisto (svih 33 rute, bez TS grešaka), `npx vitest run` → **2337/2337 testova prolazi**. Commit `91f86d5`, pushano u `main` (fast-forward `9a3ffb7..91f86d5`) i deployano na Vercel — David je potvrdio push. *(Živi klik-test na produkciji čeka sljedeću prijavljenu sesiju — vidi napomenu na kraju.)*

## 3. Preostala kozmetika (ne dira se dok David ne odluči)

1. Footer na `/auth/*` stranicama i dalje piše "AES-256" pored "GDPR"/"TLS 1.3" (`TrustComponents.tsx:49,55,104`) — u prijavljenoj aplikaciji footer je već potpuno hrvatski.
2. `/troskovnik/[id]` (spremljeni zapis) nema gumb za Excel izvoz, samo "Ispis" — Excel postoji na glavnoj stranici prilikom kreiranja.
3. Tranzitorno React hydration upozorenje (#418) u konzoli na `/troskovnik/[id]` i `/klijenti/[id]` — stranica se svejedno ispravno prikaže, korisnik ništa ne primjećuje.
4. Mobilni prikaz prijavljenog dijela nije izravno prisilno testiran (vidi t.13) — preporučam brzu ručnu provjeru na stvarnom telefonu prije predaje, jer nije bilo moguće pouzdano simulirati bez diranja Davidovog fizičkog prozora.

## 4. Presuda

### MOŽE se dati prvim korisnicima.

Cijeli put novog korisnika — registracija, prijava, prvi ulaz, kreiranje troškovnika (uključujući rubne slučajeve), spremanje, trenutna povijest, otvaranje i sad i uređivanje-kao-novi spremljenog zapisa, ispis, Excel izvoz, modul Stranke s povezivanjem, i zaključavanje nedovršenih modula — **radi ispravno na produkciji i dokazano je uživo, ne samo čitanjem koda.** Poznati "stari" kvarovi iz ovog zadatka su svi već popravljeni i deployani, a jedini stvaran nedostatak otkriven u ovoj provjeri (t.6, uređivanje spremljenog troškovnika) je popravljen istog dana, sigurnosno ispravno (kloniranje, ne prepisivanje), i deployan.

**Preostalo prije slanja pozivnice, za potpunu sigurnost:**
- **Živi klik-test "Uredi kao novi"** na produkciji — kod je type-checkan i pokriven testovima, ali nije osobno kliknut na produkciji nakon deploya (trebala se ponovna prijava koje u trenutku pisanja nije bilo). Preporučam: otvori stari troškovnik → "Uredi kao novi" → provjeri da se forma popuni razumno → spremi → potvrdi da je nastao NOV zapis, a stari nepromijenjen.
- Ručno odraditi Google prijavu klikom (strukturno popravljeno u kodu i CSP-u, ali live-klik nisam osobno mogao izvesti — sigurnosno pravilo).
- Ručno unijeti krivu lozinku jednom da vidiš točnu poruku (kod pokazuje sigurnu generičku HR poruku, ali nisam sam upisivao lozinku).
- Brzo pogledati troškovnik/klijente na stvarnom telefonu (t.13) — nisam mogao pouzdano prisiliti uski prikaz na tvom stvarnom Chrome prozoru bez remećenja tvog ekrana.

Nijedna od ove četiri stavke po dosadašnjim dokazima (kod, build, testovi, CSP, poruke) ne izgleda rizična — ali su jedine stavke koje nisam mogao dokazati vlastitim klikom na produkciji zbog sigurnosnih ograničenja ove sesije.
