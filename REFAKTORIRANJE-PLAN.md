# Plan refaktoriranja — PravoTek

Rezultat Faze 0 (sigurnosna provjera) i Faze 1 (audit). Cilj cijelog zahvata je
**izmjena strukture bez ijedne izmjene ponašanja**: nijedan endpoint, oblik JSON
odgovora, poslovno pravilo izračuna, poruka greške ni ponašanje sučelja ne smiju
se promijeniti.

Datum audita: 6. kolovoza 2026.
Radna grana: `potpuna/refaktor` (commit `9b94eea`)

---

## Faza 0 — polazne brojke

| Provjera | Rezultat |
|---|---|
| `git status` (radna mapa) | čisto ✓ |
| `dotnet build` | **prolazi**, 7 upozorenja |
| `dotnet test` | **1582 prolaza**, 0 padova ✓ |
| `npm run test` (vitest) | **2051 prolaza, 5 padova** ✗ |
| `npx playwright test` | nije pokretano — traži pokrenut dev poslužitelj i Supabase okolinu |

**Ovo su brojke koje se ne smiju smanjiti.** Nakon svakog koraka broj prolaza mora
biti jednak ili veći.

### Upozorenja iz builda (prijavljena, ne diraju se)

| Šifra | Mjesto | Napomena |
|---|---|---|
| `NU1902` | `Backend.csproj` | MailKit 4.9.0 ima poznatu ranjivost srednje ozbiljnosti. **Verzija se ne mijenja** — zabranjeno mijenjati ovisnosti bez odobrenja. |
| `CS8602` ×2 | `Validators/TroskovnikRequestValidator.cs:46,51` | potiskivanje null-provjere uskličnikom |
| `CS8604` | `Controllers/EvaluateController.cs:224` | moguće prosljeđivanje `null` |
| `CS8601` ×2 | `Controllers/EvaluateController.cs:670,691` | moguće pridruživanje `null` |

### Napomena o granama

Tarifa/troškovnik kod nikad nije spojen u `main`. Sav taj rad (33 commita) živio je
samo na `claude/tarifa-parnicni-na-audit`. Zato su napravljene tri **potpune** grane,
svaka s cijelom aplikacijom uključujući troškovnik:

| Grana | Uloga |
|---|---|
| `potpuna/glavna` | referentno potpuno stanje, ne dira se |
| `potpuna/refaktor` | **radna** — odavde granaju koraci |
| `potpuna/kopija-2026-08-06` | sigurnosna kopija, zamrznuta |

⚠️ **Glavna mapa `C:/Users/David/Documents/odvjetnik_ai` ima 164 nespremljene
promjene**, među njima 1223 retka koda koji ne postoji ni u jednom commitu
(`PropertyFeatureParser.cs`, `MarketFactorCalculator.cs`, `SimilarityScorer.cs`,
`ValuationStatistics.cs`, `SupabaseRepository.cs`, `CroatianTextMatcher.cs`,
`CroatianLocationClassifier.cs`). Ta se mapa **ne dira** dok David ne odluči što
s tim radom.

---

## Struktura projekta

| Sloj | Datoteka | Redaka |
|---|---|---|
| Backend (C# / .NET 8) | 139 `.cs` | ~28 200 |
| Backend.Tests (xunit) | 25 + 28 tarifa datoteka | ~14 400 |
| Frontend (TS / React 19 / Next 16) | 95 `.ts`/`.tsx` | ~26 300 |
| Baza | `supabase/migrations/**` | **NE DIRA SE** |

### Pokrivenost testovima po modulu

| Modul | Pokrivenost | Rizik diranja |
|---|---|---|
| Troškovnik / tarifa | **vrlo dobra** — 27 backend + 14 frontend test datoteka | nizak |
| Valuation | dobra — 8 test datoteka + scenarij JSON od 2262 retka | srednji |
| Ugovori | dobra — `ContractAnalysisTests`, `ContractRegressionTests` | srednji |
| ZK parsing | dobra — 4 test datoteke | srednji |
| Auth / 2FA | **slaba** — samo `TwoFactorServiceTests` (193 retka) | **visok** |
| Krediti / Usage | slaba — `UsageLimitTests` (146 redaka) | **visok** |
| Kontroleri | **nema** izravnih testova | **visok** |
| React komponente | **nema** — vitest pokriva samo `lib/` | **visok** |

---

## Popis nalaza

`T` = troškovnik, `O` = ostatak sustava.

| # | Datoteka:redak | Nalaz | Načelo / miris | Ozbiljnost | Rizik izmjene | Ima testove? |
|---|---|---|---|---|---|---|
| T1 | `Backend/Services/TroskovnikService.cs:761–1019` | `Izracunaj` ima ~258 redaka: provjera stavki, kazneni razred, petlja po stavkama, Tbr. 40, Tbr. 41, PDV, sudska pristojba i ~60 redaka preslikavanja odgovora — sve u jednoj metodi | Velika funkcija; više razina apstrakcije; SRP | visoka | srednji | da |
| T2 | `Backend/Services/TroskovnikService.cs:238–742` | `_katalog` — ~505 redaka podatkovnog literala unutar servisa koji ujedno računa | SRP; božanska klasa; divergentne promjene | visoka | srednji | da |
| T3 | `Backend/Services/TroskovnikService.cs:11` | `public sealed class TroskovnikService` pa u novom retku `:ITroskovnikService {` | Vertikalno i horizontalno formatiranje | niska | **nula** | da |
| T4 | `Backend/Services/TroskovnikService.cs:180,183` | `Tbr41MaxPovisenje` i `Tbr41MaxSnizenje` deklarirane **ispod** metode koja ih koristi (`RazrijesiTbr41`, r. 154) | Vertikalno formatiranje — deklaracija blizu prve uporabe | niska | **nula** | da |
| T5 | `Backend/Services/TroskovnikService.cs:204–235` | `Tbr71Bodovi`: 332.00, 664.00, 1327.00, 13272.00, 33181.00, 66361.00, 133.00, 265.00, 4991, 7496, 10000 — svi goli | Magični brojevi | visoka | nizak | da (`TarifaRazrediTests`) |
| T6 | `Backend/Services/TroskovnikService.cs:209,212,236` | Engleski u hrvatskoj domeni: `// Fixed bands`, `// Progressive bands`, `// Catalog of all tariff items`, `GetKatalog()`, `_katalog` | Nedosljedno nazivlje | srednja | nizak | da |
| T7 | `Backend/Services/TroskovnikService.cs:763–765,790,798` | Engleske lokalne varijable: `catalogMap`, `lines`, `rbr`, `isSamoKazneni`, `trebaKazneniTier`, `kazneniTierBodovi`, `useVat` | Nedosljedno nazivlje; STUPID — Indescriptive naming | srednja | nizak | da |
| T8 | `Backend/Services/TroskovnikService.cs:~975,~1005` | `Math.Max(1, request.BrojZastupanihOsoba)` izračunato dvaput; `IsPdvUkljucen ?? HasVat` izračunato dvaput (`useVat`, pa opet u odgovoru) | DRY | srednja | nizak | da |
| T9 | `Backend/Services/TroskovnikService.cs:~1008` vs `:154–185` | Tbr. 41 postoci izvan raspona u `RazrijesiTbr41` **bacaju iznimku**, a u odgovoru se isti podaci tiho `Math.Clamp`-aju | Nedosljedno rukovanje greškama | srednja | srednji | da — **PITANJE 2** |
| T10 | `Backend/Controllers/TroskovnikController.cs:36–41` i `:88–94` | Ista provjera (`VrijednostSpora < 0`, prazne `Stavke`) i isti `try/catch (TroskovnikValidationException)` doslovno ponovljeni u `Izracunaj` i `Spremi` | DRY; anti-obrazac cut-and-paste | srednja | nizak | **ne** |
| T11 | `Backend/Controllers/TroskovnikController.cs:29,74` | Kontroler ubrizgava `ITroskovnikService`, a **istovremeno statički zove** `TroskovnikService.GetKatalog()` i `TroskovnikService.Tbr71Bodovi()` | DIP; tijesno vezanje; STUPID — Tight coupling, Untestability | visoka | **visok** | **ne** |
| T12 | `Backend/Services/TroskovnikService.cs:27–122` | `IzracunajOsnovicu` — 95 redaka, `switch` po `OsnovicaTbr7` sa 7 grana | Velika funkcija; miris `switch` | srednja | **visok** | da — **PITANJE 3** |
| T13 | `Backend/Models/TroskovnikModels.cs` | `FiksniBody` (treba `FiksniBodovi`), `KazneniBase`, `SortOrder` — mješavina jezika u domenskom modelu | Dezinformativno ime | srednja | **visok** | da — **PITANJE 4** |
| T14 | `frontend/app/troskovnik/page.tsx` | 2215 redaka, **56 `useState`**, jedna komponenta `TroskovnikContent`, 5 uporaba `any` | Božanska komponenta; SRP; velika klasa | visoka | **visok** | **ne** |
| T15 | `Backend/Validators/TroskovnikRequestValidator.cs:46,51` | `predmet!.Sud` — `CS8602`, potiskivanje null-provjere | Rukovanje greškama | niska | nizak | djelomično |
| O1 | `Backend/Services/ValuationEngine.cs:217–227` **(+190 mjesta)** | **`double` za novčane iznose**: `MinSalePriceEur_Building = 35_000`, `MinPricePerM2_*` itd. | ⚠️ Anti-obrazac: realni tipovi (double/float) za novac | **visoka** | **visok** | da — **NE MIJENJA SE** |
| O2 | `ValuationEngine.cs` (3707), `MarketResearchService.cs` (3054) | Dvije klase preko 3000 redaka | Božanska klasa; SRP | visoka | **visok** | da |
| O3 | `Backend/Controllers/EvaluateController.cs:224,670,691` | `CS8604`/`CS8601` — moguće prosljeđivanje `null` | Rukovanje greškama | srednja | srednji | **ne** |
| O4 | `frontend/__tests__/delete.test.ts:12` | Mockira `@supabase/supabase-js`, a `frontend/lib/supabase.ts:1` koristi `createBrowserClient` iz `@supabase/ssr` → mock ne hvata, test izvodi **stvarni mrežni poziv** i pada | Test nije izoliran (krši FIRST) | **visoka** | nizak | — |
| O5 | `zk-procjena/page.tsx` (1925), `ugovor/page.tsx` (1295), `valuation/page.tsx` (1089) | Divovske stranice-komponente | Božanska komponenta; SRP | visoka | **visok** | **ne** |

### Pozitivan nalaz — novac u troškovniku

**Troškovnik nigdje ne koristi `double` ni `float` za novčane iznose.** Sve je
`decimal` (77 pojava). Bodovi ostaju decimalni kroz cijeli izračun i zaokružuju se
**jednom, na kraju**, na dvije decimale. Jedini pogodak na „float" u tarifa kodu je
CSS `float:right` u generiranom HTML-u.

Anti-obrazac „realni tipovi za novac" postoji **izvan** troškovnika — nalaz O1.
Prijavljen je, ali se **ne mijenja** jer bi promijenio rezultate procjene
nekretnina, što je izmjena ponašanja.

---

## Prioritetni redoslijed koraka

Svaki korak ide na vlastitu granu i vlastiti commit. Ciklus: izmjene →
`dotnet build` + `npm run build` → `dotnet test` + `npm run test` → commit →
izvještaj → stanka.

| Korak | Grana | Sadržaj | Nalazi | Rizik |
|---|---|---|---|---|
| 00 | `refactor/00-popravak-mocka-supabase` | Popravak zastarjelog mocka: mockirati `@supabase/ssr`. Mijenja se **samo test datoteka**, nijedna produkcijska | O4 | nula |
| 01 | `refactor/01-formatiranje-troskovnik` | Vertikalno i horizontalno formatiranje: deklaracija klase, konstante iznad prve uporabe, prazni redci između koncepata, ~120 znakova | T3, T4 | nula |
| 02 | `refactor/02-mrtvi-kod-i-komentari` | Brisanje mrtvog i zakomentiranog koda, redundantnih komentara. **Pravni citati iz Tarife ostaju** — objašnjavaju namjeru i izvor odredbe | — | nula |
| 03 | `refactor/03-imena-troskovnik` | Hrvatska imena: `catalogMap`→`katalogPoId`, `lines`→`stavkeRetci`, `rbr`→`redniBroj`, `useVat`→`obracunajPdv`, `GetKatalog()`→`DohvatiKatalog()`, engleski komentari na hrvatski | T6, T7 | nizak |
| 04 | `refactor/04-magicni-brojevi-tbr71` | Razredi Tbr. 7. toč. 1 → imenovana tablica `Tbr71Razredi`, uz svaki prag citat odredbe | T5 | nizak |
| 05 | `refactor/05-razlamanje-izracunaj` | `Izracunaj` (258 r.) → `ProvjeriIRazrijesiStavke`, `RazrijesiKazneniRazred`, `IzracunajRetke`, `IzracunajZbrojeve`, `SastaviOdgovor` | T1 | srednji |
| 06 | `refactor/06-dry-troskovnik` | Uklanjanje dvostrukih izračuna; izdvajanje ponovljene provjere iz kontrolera | T8, T10 | srednji |
| 07 | `refactor/07-katalog-izdvajanje` | `_katalog` (505 r.) → zasebna klasa `TarifaKatalog`; servis računa, katalog nosi podatke | T2 | srednji |
| 08 | `refactor/08-dip-kontroler` | Kontroler prestaje statički zvati servis; sve ide kroz `ITroskovnikService` | T11 | visok |
| 09 | `refactor/09-frontend-troskovnik` | Razlamanje `troskovnik/page.tsx`: custom hookovi (`useTroskovnikObrazac`, `useKatalog`) i pod-komponente; smanjenje 56 `useState` | T14 | visok |

Koraci za SOLID na ostatku sustava i za oblikovne obrasce predlažu se tek nakon
ovih devet, i samo uz karakterizacijske testove za module koji ih nemaju.

---

## Rupe u testovima

Karakterizacijski test bilježi **zatečeno ponašanje, kakvo god bilo**, da bi se
poslije vidjelo je li ga refaktoriranje promijenilo.

| Modul | Zašto je rizičan | Prijedlog |
|---|---|---|
| `TroskovnikController` | **nema nijedan test**, a koraci 06 i 08 ga diraju | Karakterizacijski testovi za sva četiri endpointa (`katalog`, `izracunaj`, `tbr71`, `spremi`) — statusni kod i oblik JSON-a — **prije koraka 06** |
| `frontend/app/troskovnik/page.tsx` | **nema testova komponente**, a korak 09 ga razlama | Testovi ponašanja obrasca **prije koraka 09**. Bez njih se korak 09 ne izvodi |
| Auth / 2FA, Krediti, ostali kontroleri, ostale React stranice | slaba ili nikakva pokrivenost | Ne diraju se u ovom ciklusu |

---

## Pitanja koja čekaju odgovor

1. **Nespremljeni rad u glavnoj mapi.** 1223 retka novog koda postoji samo na
   disku, ni u jednom commitu. Ostaviti netaknuto, spremiti u zasebnu granu, ili
   ne dirati i ne spominjati?

2. **T9 — Tbr. 41: iznimka ili tiho podrezivanje?** `RazrijesiTbr41` baca
   `TroskovnikValidationException` za postotak izvan raspona, ali se u odgovoru
   isti podaci tiho `Math.Clamp`-aju na 0–100 odnosno 0–50. Je li podrezivanje
   namjerno (npr. zbog starih spremljenih troškovnika) ili ostatak od prije
   uvođenja iznimke? **Ne dira se dok se ne razjasni.**

3. **T12 — `switch` po `OsnovicaTbr7`.** Preporuka je **ostaviti kako jest**.
   Sedam grana čita kao sama Tarifa i svaka nosi citat odredbe; rastavljanje na
   strategije razbilo bi taj tekst na sedam datoteka bez stvarne koristi (YAGNI).

4. **T13 — `FiksniBody` → `FiksniBodovi`.** ⚠️ Serijalizacija koristi
   `JsonNamingPolicy.SnakeCaseLower`, pa bi preimenovanje C# svojstva promijenilo
   JSON ključ s `fiksni_body` na `fiksni_bodovi` — a to je **izmjena ugovora
   odgovora**, koja je zabranjena. Izvedivo je samo uz
   `[JsonPropertyName("fiksni_body")]` koji zadržava stari ključ.

5. **Playwright.** Mogu li se e2e testovi pokrenuti lokalno, ili se trajno
   bilježe kao nedostupni?

6. **Stare prazne grane.** Pet grana identično je `main`-u i nema troškovnik.
   Ostaviti ih ili obrisati?

---

## Provjera nakon svakog koraka

```
dotnet build ProjectAI_Odvjetnik.sln
dotnet test  ProjectAI_Odvjetnik.sln
npm --prefix frontend run test
npm --prefix frontend run build
```

Broj prolaza mora biti **jednak ili veći** od polaznog (1582 backend / 2051+
frontend). Ako padne: objašnjenje, `git checkout -- .`, i javljanje. Test se
nikad ne „popravlja" da bi prošao — mijenja se samo ako se promijenilo ime
metode ili klase koju testira.
