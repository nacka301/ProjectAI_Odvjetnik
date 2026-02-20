# PravoTek — Sveobuhvatna Analiza Sustava

## Pregled Arhitekture

**PravoTek** je AI platforma za hrvatske odvjetnike i procjenitelje nekretnina. Tehnološki stog:

| Sloj | Tehnologija |
|------|-------------|
| **Frontend** | Next.js (TypeScript, React, Tailwind CSS) |
| **Backend** | ASP.NET Core (C#), .NET 8 |
| **Baza podataka** | Supabase (PostgreSQL + PostgREST + pgvector) |
| **AI Gateway** | OpenRouter → GPT-4o-mini (Fast), DeepSeek V3 (Heavy/Reasoning) |
| **Tržišno istraživanje** | Tavily API (web search) + BrightData (web scraping) |
| **Audit sloj** | Perplexity API (sonar-pro model s web pretragom) |
| **Email** | Brevo (Sendinblue) transakcijski API |
| **Embeddinzi** | OpenAI text-embedding-3-small (1536 dimenzija) |
| **PDF ekstrakcija** | PdfPig (dual-strategy: word-level layout + basic) |
| **e-Fakturiranje** | UBL 2.1 XML (EN 16931 standard) |

### AI Model Routing (ChatClientProvider)

```
Fast      (GPT-4o-mini)       → ekstrakcija, klasifikacija, sažeci — jeftino i brzo
Heavy     (DeepSeek V3)       → kompleksno pravno rezoniranje, strategija, narativ
Reasoning (DeepSeek V3)       → duboka logika, detekcija zamki, drugo mišljenje
```

### Supabase Shema (PostgreSQL)

Tablice: `evaluations`, `zk_analyses`, `contract_analyses`, `chronologies`, `legal_strategies`, `invoices`, `valuation_audits`, `audit_logs`, `norms`, `norm_amendments`, `law_chunks` (pgvector)

RLS politike: `service_role` ima puni pristup, `anon` ima samo SELECT. Backend piše isključivo putem service_role ključa.

---

## MODUL 1: Procjena Nekretnina (Valuation)

### Frontend

**Ruta:** `/valuation` (`frontend/app/valuation/page.tsx`, 1129 redaka)

**Korisnički unos:**
- Upload PDF opisa nekretnine ILI copy-paste tekst
- Izborne opcije: vrsta nekretnine, lokacija, veličina

**SSE Streaming** — 6 faza prikazanih korisniku:
1. `reading` → Čitanje opisa
2. `searching` → Tržišno istraživanje
3. `ai_features` → AI ekstrakcija značajki
4. `engine` → Deterministička procjena
5. `ai_opinion` → AI stručno mišljenje
6. `saving` → Spremanje u bazu

**Frontend tipovi** uključuju V4 Robust Estimator polja: `std_dev_per_m2`, `coefficient_of_variation`, `outlier_method`, `used_comparables` (s per-comp korekcijama), `adjustments`, `pipeline`, `math_confidence_score`.

**Rezultat:** Kartica s procijenjenom vrijednošću, rasponom cijena (konzervativno/realistično/optimistično), tablicom usporedivih nekretnina, grafikonom prilagodbi, i AI stručnim mišljenjem.

### API Endpoints (EvaluateController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/evaluate/stream` | SSE pipeline iz PDF-a |
| POST | `/api/evaluate/text/stream` | SSE pipeline iz teksta |

### Backend Pipeline (detaljan tok)

```
1. PDF Upload → PdfService.ExtractTextAsync()
   - Dual-strategy: word-level spatial reconstruction + basic extraction
   - Quality scoring (hrvatska pravna/nekretninska terminologija)
   - 50MB limit, detekcija skeniranih PDF-ova

2. AI Ekstrakcija → ValuationService.ExtractPropertyInfoAsync()
   - Fast model (GPT-4o-mini)
   - Izvlači 12+ polja: tip, lokacija, veličina, stanje, kat, godina, garaža, okućnica...
   - Vraća ExtractedPropertyInfo objekt

3. Tržišno Istraživanje → MarketResearchService
   - "Shotgun" pristup — paralelne pretrage na 4 portala (Njuškalo, Index, Crozilla, Nekretnine.hr)
   - Višestruke varijacije upita, sinonimi tipova, hijerarhija lokacija
   - Price-First gate (regex mora pronaći €)
   - PrestigePeers mapiranje (kvartovske fallback grupe za Zagreb, Split, Rijeka...)
   - BroaderRegion mapiranje (otoci→obala, predgrađa→grad)
   - ForbiddenCityPairs (sprečava kontaminaciju između tržišta)
   - BrightData WebSocket za duboki scraping detalja oglasa

4. Sherlock Analiza → TextAnalysisService.AnalyzeListingsAsync()
   - V29 "Sherlock Mode" — semantička analiza opisa oglasa
   - Fast model izvlači 16 strukturiranih polja po oglasu
   - Override scraper podataka s tekst-deriviranim činjenicama

5. Kontekstualna Analiza → ValuationAgentService.AnalyzePropertyContextAsync()
   - V36 "The Legal Scholar" — kontekstualna AI analiza nekretnine
   - Heavy model (DeepSeek V3) pronalazi prilagodbe koje keyword sustavi propuštaju
   - Geometrija, zoniranje, infrastruktura, pravni aspekti
   - Vraća postotne prilagodbe s obrazloženjem

6. DETERMINISTIČKI ENGINE → ValuationEngine.Calculate()
   ═══ JEZGRA SUSTAVA (detaljno opisana u nastavku) ═══

7. AI Stručno Mišljenje → ValuationService.GenerateAutoValuationAsync()
   - Heavy model (DeepSeek V3) generira narativ
   - Integrira podatke engine-a: realizacijski gap, outlier detekcija, novogradnja vs starogradnja
   - Korekcija veličine, sparse data protokol
   - Hrvatski format brojeva

8. Spremanje → SupabaseService.SaveEvaluationAsync()
   - PostgREST INSERT u `evaluations` tablicu
   - JSON serijalizacija kompletnog rezultata u `raw_json_output`

9. (Opcijski) Email → EmailService.SendValuationEmailAsync()
   - Brevo transakcijski API
   - HTML template sa sažetkom, tablicom usporedivih, prilagodba
```

### Supabase Tablica

```sql
evaluations (
    id UUID PK, created_at, property_type TEXT, location TEXT,
    size_m2 NUMERIC, estimated_value NUMERIC, pdf_url TEXT,
    raw_json_output JSONB
)
```

---

## DETALJNA ANALIZA: ValuationEngine.cs — Deterministički Procjenski Pipeline

**Datoteka:** `Backend/Services/ValuationEngine.cs` (3.464 redaka)  
**Ulaz:** `ExtractedPropertyInfo` + `MarketResearchResult`  
**Izlaz:** `DeterministicValuation`

### Korak 1: Kontekst

Postavljanje regije, broja oglasa, inicijalizacija pipeline trace loga.

### Korak 2: Ekstrakcija Cijena

`ExtractPricesFromListings()` — trorazinski sustav:

| Tier | Izvor | Logika |
|------|-------|--------|
| **Tier 1** | `ListedPriceEur` + poznata veličina | Računa `price / sizeM2` — najsigurnije |
| **Tier 2** | `ListedPricePerM2` (s portala) | Koristi se kad nema ukupne cijene |
| **Tier 3** | Regex ekstrakcija iz teksta | `PricePerM2Regex`, `PriceRegex`, `SizeRegex` |

**Sigurnosni filteri:**
- Minimalne cijene: zgrade ≥200 €/m², građevinsko zemljište ≥15 €/m², poljoprivredno ≥0,5 €/m²
- Maksimum: zgrade ≤20.000 €/m², zemljišta ≤5.000 €/m² (V76: premium tržišta — Dubrovnik, Hvar)
- Rental-trap detekcija — preskače oglase za najam
- V71: Fallback za estimiranu veličinu kad `SizeM2=0`
- V76: Uklonjena kružna referenca (korištenje ciljne veličine za comp izračun)

**V28 Deduplikacija:** Uklanja duplikate (isti €/m² ±3% I ista površina ±5%) — sprečava da ista nekretnina objavljena kod 2 agencije napuše uzorak.

**Smart Size Extraction (V11):** `ExtractMainSize()` — preskače garaže, parkirna mjesta, terase, balkone (gleda kontekst 40 znakova ispred m² oznake), uzima NAJVEĆU nekvalificirani veličinu.

**Croatian Number Parsing:** `ParseCroatianNumber()` — punkt = tisućice, zarez = decimala. "1.088" → 1088, "2.500,50" → 2500.50.

### Korak 3: IQR Outlier Detekcija

```
Ako ≥5 cijena: Puni IQR (Q1 - 1.5×IQR do Q3 + 1.5×IQR)
Ako 2-4 cijene: Pass-through (premalo podataka za statistiku)
Ako 1 cijena:  Single price mode
```

**IQR-too-aggressive fallback:** Ako IQR ostavi <3 cijene, vraća originalne podatke.

### Korak 3b: Sales-Only Gatekeeper

Minimalni podovi cijena:
- Zgrade: 200 €/m² (ukupno ≥35.000 €)
- Građevinsko zemljište: 15 €/m² (ukupno ≥5.000 €)
- Poljoprivredno: 0,5 €/m² (ukupno ≥500 €)

Detekcija ključnih riječi za najam (`/mj`, `najam`, `rent`, `zakup`).

### Korak 3c: 10x Ratio Sanity Check

Ako je `max/min > 10`, uklanja ekstremno niske vrijednosti.

### Korak 3e: V23 Location-Aware Price Clustering

Kad je raspon >2,5× (spread ratio), detektira 2 klaster cijena. Bira klaster koji odgovara subjektu (premium vs. periferno) koristeći analizu ključnih riječi lokacije.

### Korak 4a: Condition-Based Price Adjustment

`ConditionAdjustmentFactor()` — petorazinska skala:

| Rang | Stanje |
|------|--------|
| 0 | NeedsWork (za adaptaciju) |
| 1 | Original (izvorno) |
| 2 | Good (dobro) |
| 3 | Renovated (renovirano) |
| 4 | New (novogradnja) |

Formula: Subjekt lošiji → -10% po rangu razlike. Subjekt bolji → +7% po rangu.  
Uzajamno isključivo s V12 Twin Comparison renovacijskim odbitkom.

### Korak 4a+: V12 Twin Comparison

`DeriveAdjustmentFactors()` — pronalazi "blizance" u datasetu (slični po veličini/lokaciji, različiti po jednoj značajci) za dinamično izračunavanje tržišne vrijednosti svake značajke:

| Značajka | Metrika | Izračun | Fallback (Coastal/Inland/Rural) |
|----------|---------|---------|----------------------------------|
| Garaža | Flat EUR | Median(s garažom) − Median(bez) × prosj.veličina, clamp 0-60k | 35k / 20k / 10k |
| Parking | Flat EUR | Median(s parkingom) − Median(bez) × prosj.veličina, clamp 0-30k | 15k / 8k / 3k |
| Renovacija | % | (Median(renovirano) − Median(staro)) / Median(staro), cap 50% | 25% / 20% / 15% |
| Pogled more | % | (Median(s pogledom) − Median(bez)) / Median(bez), cap 40% | 15% / 0% / 0% |
| Orijentacija | % | (Median(jug) − Median(sjever)) / Median(sjever), cap 30% | 12% / 3% / 0% |

**Market Context Detection:** `DetectMarketContext()` — Coastal (Split, Dubrovnik, Rijeka, Zadar...), Inland (Zagreb, Osijek, Varaždin...), Rural (sve ostalo).

**V25:** Investicijske/premium nekretnine — cap renovacijske penalizacije na 10%.

### Korak 4b: Neighborhood Drift + Exact-Match

- **3× težinski boost** za egzaktni kvart
- **Strict Neighborhood Purge:** Ako ≥3 cijena iz egzaktnog kvarta → odbaci sve ostale
- **V26 Prime Zone Lock ("Anti-Barutana pravilo"):** Isključuje suburbane comps kada je subjekt u premium zoni (Centar, Stari Grad, Bol)

### Korak 4b++: V13 Cross-Type Hard Exclusion

- Zabranjuje usporedbu kuća ↔ stanova ↔ zemljišta
- V15: Poljoprivredno vs. građevinsko zemljište isključenje
- V29: Sherlock tekst authority override

### Korak 4c: Similarity Scoring

**Formula:** `Score = Location(70%) + Size(20%) + Condition(10%)`

**Lokacijski scoring (70% težina):**

| Match | Score |
|-------|-------|
| Kvart + grad | 0.70 |
| Samo kvart | 0.60 |
| Bilo koji dio lokacije | 0.25 |
| Samo regija | 0.15 |
| Nepoznato | 0.03 |

- V48: Stripping hrvatskih dijakritičkih znakova za URL matching (`č→c`, `š→s`, `ž→z`, `ć→c`, `đ→d`)
- V38: Cross-city safety cap
- V29: Sherlock boosting/penalizing (garaža match, stanje mismatch, hitnost, oštećenja, pravni status)
- V48e: Math floor za AI score-ove

### Korak 4c½: Similarity Floor

Prag: **0.45** — uklanja comps ispod minimalne sličnosti.

### Korak 4c+: Top-N Limiter

Zadržava samo **top 7** comps po sličnosti.

### Korak 4d: Reality Check Cap

Isključuje comps >**140%** ili <**60%** medijana.

### Korak 4d+: V26 Zone-Aware Outlier Purge

Kad je cjenovni raspon >100% i subjekt je u premium zoni, odbacuje jeftiniji tier.

### Korak 5: Weighted Median

Algoritam:
1. Sortiraj comps po cijeni (€/m²)
2. V7: 3× boost težina za egzaktni kvart
3. Akumuliraj težine do 50% — interpoliraj

**Sanity check:** Ako je WM >140% najniže egzaktne cijene, ponovno izračunavanje samo s egzaktnim matchevima.

### Korak 6: Standard Deviation & Coefficient of Variation

Standardna statistička mjera raspršenosti cijena. CV = (σ / μ) × 100.

### Korak 7: Adaptivni Realizacijski Gap

**5 razina** na temelju broja comps + CV:

| Comps | CV | Gap | Opis |
|-------|-----|-----|------|
| ≥8 | <15% | -8% | Visok likvidnost |
| ≥5 | <20% | -10% | Srednje-visoka |
| ≥3 | <30% | -13% | Srednja |
| ≥3 | ≥30% | -16% | Niska |
| <3 | — | -20% | Vrlo niska / nema podataka |

**Phase penalty:** SubLocationDrill → -4%, SynonymBlitz → -2%  
**V26:** Prime zone investicije — cap na -10%  
**Clamp:** Nikad manje od -25%, minimum -5%

### Korak 8: Kontekstualne Prilagodbe (±40% hard cap)

`CalculateAdjustments()` — keyword-based prilagodbe na razini subjekta:

| Faktor | % | Uvjet |
|--------|---|-------|
| Premium centar grada | +20% | Centar + landmark |
| Centar grada | +12% | Centar bez landmarka |
| Blizina orijentira | +10% | Landmark bez centra |
| Periferija | -8% | Periferija |
| Premium sadržaji | +5% | ≥2 premium amenity keywords |
| T2 zona investicijski | +20% | Turističko zemljište >2000m² + blizu mora |
| Turistička zona | +8% | Zona T1/T2 |
| Investicijski potencijal | +15% | Komercijalni/turistički keywords (ne zemljište) |
| Prvi red (Dubrovnik/Hvar) | +50% | 1. red + Dubrovnik/Hvar |
| Prvi red (ostalo) | +25% | 1. red do mora |
| Blizina mora | +10% | Blizu mora (ne 1. red) |
| Novogradnja | +15% | "novogradnja", "novouređ" |
| Potrebna adaptacija | -15% / -5% | Ruševina/zapušteno (cap za investicije) |
| Adaptirana | +8% | Renovirano/adaptirano |
| Sjever bez pogleda (obala) | -10% | Sjevernja orijentacija, bez mora, obalni grad |
| Sjeverna orijentacija | -6% | Sjever na obali |
| Bez pogleda | -5% | Ulični pogled na obali |
| Otočna lokacija | +8% | Otok |
| Mali stan (<35m²) | +7% | Stan < 35m² |
| Velika nekretnina (>200m²) | -7% | >200m² (ne zemljište, ne investicija) |
| Veliki zemljišni kompleks | -10% do -15% | Parcela ≥3× prosj. comp (10x pravilo) |
| Loša infrastruktura | -10% | Neasfaltirani pristup |
| Legalizacija | -12% | Neriješena legalizacija |
| Građevinsko zemljište | +5% | Parcela s dozvolom |
| Poljoprivredno | 0% | Ako su comps već agro-cijene |

### Korak 9: Final Value

```
finalPricePerM2 = adjustedPricePerM2 (= weightedMedian × (1 + gap) × (1 + adjustments))
estimatedValue  = finalPricePerM2 × sizeM2
```

**V11 Safety Floor:** Minimalno 50% prosječne čiste comp cijene.

### Korak 10: Mathematical Confidence Score (0–100)

`CalculateMathConfidence()`:

| Penalizacija | Uvjet |
|-------------|-------|
| -20 | Nema cijena iz oglasa |
| -15 | <3 usporedivih |
| -5 | <5 usporedivih |
| -15 | Vrlo visok CV (>35%) |
| -10 | Visok CV (>20%) |
| -3 | Umjeren CV (>10%) |
| -15 | Regionalni fallback faza |
| -10 | Kasna faza pretrage |
| -5 | Synonym blitz faza |
| -5 | IQR fallback korišten |
| -10 | V71: Estimirana površina |
| **+5** | **Bonus:** ≥8 comps i CV <15% |

**Razine:** ≥75 Visoka, ≥50 Srednja, ≥25 Niska, <25 Vrlo niska.

### Per-Comparable Korekcije (V20, Usporedna Metoda)

`CalculatePerCompCorrection()` — 7 dimenzija korekcije za svaki comp:

1. **Veličina:** Comp >25% veći → +adj (do +8%), >25% manji → -adj (do -8%)
2. **Stanje (zgrade):** V29 Sherlock override. ±5% po rangu razlike, clamp ±15%
3. **Starost (zgrade):** ≥15 godina razlike → ±3% per dekada, clamp ±8%
4. **Garaža (zgrade):** V29 Sherlock override. ±3%
5. **Okućnica (zgrade):** ±3%
6. **Komercijalni potencijal:** -15% (Super-Extractor), oštećenja +20%, legalizacija +30%
7. **Dizalo (stanovi):** >3. kat bez dizala → +10%

**Cap:** ±35% per comp.

---

## MODUL 2: Wizard (Strukturirani Unos)

### Frontend

**Ruta:** `/wizard` (`frontend/app/wizard/page.tsx`, 571 redaka)

**3-koračni čarobnjak:**
1. **Odabir tipa:** Stan / Kuća / Zemljište
2. **Formular:** Polja ovisna o tipu (površina, lokacija, katnost, stanje, garaža, okućnica, napomene...)
   - Validacija drafta putem `/api/valuation/draft`
   - `LocationSelector` komponenta za izbor grada/kvarta
3. **Potvrda:** Pregled podataka + pokretanje SSE streaming procjene

**Payload:** `buildPayload()` konstruira polimorfni JSON (`ApartmentData` / `HouseData` / `LandData`).

### API Endpoints (ValuationDraftController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/valuation/draft` | Polimorfna validacija unosa |

**Validacija:** FluentValidation s tipski-specifičnim pravilima.  
**Izlaz:** Validirani podaci + čitljiv opis (`PropertyDescriptionGenerator.Generate()`).

Kod potvrde, wizard poziva `/api/evaluate/text/stream` — isti pipeline kao Modul 1.

---

## MODUL 3: ZK Due Diligence (Zemljišnoknjižni Izvadak)

### Frontend

**Ruta:** `/zk-procjena` (`frontend/app/zk-procjena/page.tsx`, 2007 redaka)

**Korisnički unos:** Upload ZK izvadak PDF ili copy-paste tekst.

**Kompleksni tipovi:**
- `OwnerInfo` — ime, udio, je li potpuno
- `MortgageInfo` — vjerovnik, iznos, valuta, datum
- `EncumbranceInfo` (V44 OCR 2.0) — fiducijarni prijenos, služnosti, zabilježbe
- `PlombaInfo` — aktivne plombe (blokade)
- `DueDiligenceFinding` — nalaz, ozbiljnost, preporuka
- `KatastarComparison` — usporedba ZK vs. katastar
- `HistoricalEncumbrance` — izbrisani tereti
- `EncumbranceRiskSummary` — matrica rizika

**Risk scoring:** `"ČISTO"` | `"UVJETNO"` | `"BLOKIRANO"`

### API Endpoints (ZkUnifiedController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/zk/stream` | Unificiran ZK parse + valuation (PDF) |
| POST | `/api/zk/stream-text` | Isto iz pasteiranog teksta |

### Backend Pipeline

```
1. PDF ekstrakcija → PdfService.ExtractTextAsync()
2. ZK Parsing → ZkParsingService.ParseZkIzvadakAsync()
   - Fast model (GPT-4o-mini)
   - 5 sekcija analize:
     a) Plomba hunting — aktivne plombe
     b) List A (Posjedovnica) — katastarska oznaka, vrsta, površina
     c) List B (Vlastovnica) — vlasnici, udjeli, matematička verifikacija
     d) List C (Teretovnica) — hipoteke, služnosti, ograničenja
     e) V44 OCR 2.0 — fiducijarno vlasništvo, povijesni tereti, matrica rizika
3. Due Diligence Analiza → Strukturirani nalaz s preporukama
4. (Opcijski) Valuation Pipeline → Isti kao Modul 1 (reuses EvaluateController logiku)
5. Spremanje → SupabaseService u `zk_analyses` tablicu
```

### Supabase Tablica

```sql
zk_analyses (
    id UUID PK, created_at, property_id TEXT, cadastral_municipality TEXT,
    parcel_number TEXT, property_type TEXT, location TEXT,
    land_area_m2, building_area_m2, risk_score TEXT,
    owners JSONB, mortgages JSONB, disputes JSONB, encumbrances JSONB,
    lawyer_comment TEXT, evaluation_id UUID FK→evaluations,
    raw_json_output JSONB, pdf_url TEXT
)
```

---

## MODUL 4: Analiza Ugovora (Contract Analysis)

### Frontend

**Ruta:** `/ugovor` (`frontend/app/ugovor/page.tsx`, 1365 redaka)

**Korisnički unos:** Upload PDF ugovora ili copy-paste tekst.

**Verzije analize:**
- **V49:** Strukturirani checklist, cross-article analiza, obavezne klauzule
- **V50:** Deep reasoning (logičke zamke putem Reasoning modela)
- **V51:** Trostruka arhitektura (3 prolaza), kritička recenzija
- **V58:** Generiranje poboljšanog ugovora

**V42 Temporal Mode:** `eventDate` za normativno verzioniranje (koji zakon vrijedi na datum događaja).

### API Endpoints (ContractController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/contract/analyze-pdf` | PDF analiza (ne-streaming) |
| POST | `/api/contract/analyze-text` | Tekstualna analiza (ne-streaming) |
| POST | `/api/contract/stream` | SSE streaming analiza |

### Backend Pipeline (ContractAnalysisService.cs, 1764 redaka)

```
V51 Trostruka Arhitektura:

PROLAZ 1 — Heavy model (DeepSeek V3):
  - Strukturirani audit checklist
  - 4-koračna obavezna revizija:
    1. Tereti i opterećenja
    2. Kapara i plaćanje
    3. Prijenos vlasništva (clausula intabulandi)
    4. Zaštitne klauzule
  - Cross-article analiza
  - Detekcija obaveznih klauzula

PROLAZ 2 — Reasoning model (DeepSeek V3):
  - Deep logical trap detection
  - Logičke zamke, proturječnosti, skriveni rizici
  - Chain-of-thought rasuđivanje

PROLAZ 3 — Engine (deterministički):
  - Risk scoring (0-100)
  - Red flags brojanje
  - Ukupna ocjena rizika: VISOK / SREDNJI / NIZAK

V69: Semantic RAG — LawRAGService dohvaća relevantne članke zakona
  iz law_chunks tablice za injektiranje u prompt
```

**Jurisdikcija:** Striktno hrvatsko pravo — eksplicitna zabrana anglo-saksonskih pravnih koncepata.

### Supabase Tablica

```sql
contract_analyses (
    id UUID PK, created_at, document_type TEXT, summary TEXT,
    overall_risk TEXT, risk_score INTEGER, red_flags_count INTEGER,
    raw_json_output JSONB
)
```

---

## MODUL 5: Kronologija (Chronology)

### Frontend

**Ruta:** `/kronologija` (`frontend/app/kronologija/page.tsx`, 702 redaka)

**Korisnički unos:** Upload do 50 PDF datoteka (sudski spis, dopisi, presude...).

**SSE streaming** — per-file progress.

**Rezultat:** Vremenska crta s događajima, razinama povjerenja (High/Medium/Low), DOCX export putem `docx` biblioteke.

### API Endpoints (ChronologyController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/chronology/stream` | Multi-PDF SSE kronologija |

### Backend Pipeline

```
Za svaki PDF sekvencijalno:
  1. PdfService.ExtractTextAsync() → tekst
  2. ChronologyService.ExtractEventsAsync()
     - Fast model (GPT-4o-mini)
     - Persona: "Forenzički pravni analitičar"
     - Fokus: prekluzivni rokovi, teret dokazivanja, proceduralni slijed
     - Klasifikacija povjerenja: High/Medium/Low
  3. Sortiranje kronološki

Spremanje → SupabaseService u `chronologies` tablicu + `audit_logs`
```

### Supabase Tablica

```sql
chronologies (
    id UUID PK, created_at, total_documents INTEGER,
    total_events INTEGER, date_range TEXT, raw_json_output JSONB
)
```

---

## MODUL 6: Pravna Strategija (Legal Intelligence)

### Frontend

**Ruta:** `/sudska-praksa` (`frontend/app/sudska-praksa/page.tsx`, 1497 redaka)

**Korisnički unos:**
- Opis slučaja (slobodni tekst)
- Pozicija klijenta: tužitelj / tuženik / treća strana

**Rezultat:**
- Gauge vjerojatnosti pobjede s rasponom (min/max/midpoint)
- V34: Score breakdown, confidence faktori, data provenance
- Precedent sidebar s citiranom sudskom praksom
- Norm citation badges (zakoni s NN referencama)
- V45: AI Act audit trail (EU 2024/1689)

### API Endpoints (LegalController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| GET | `/api/legal/feed` | Dashboard pravni feed |
| POST | `/api/legal/strategy` | Puna pravna analiza |
| POST | `/api/legal/strategy/stream` | SSE streaming verzija |
| POST | `/api/legal/precedents` | Precedent sidebar pretraga |

### Backend Pipeline (LegalIntelligenceService.cs, 860 redaka)

```
V36 "The Legal Scholar" — Syllogismus Iuridicus:

1. Feed Generator (Fast model):
   - Generira dashboard summary s ključnim informacijama

2. Strategy Analyzer (Heavy model — DeepSeek V3):
   - Syllogismus Iuridicus metodologija
   - Analiza precedenata sa stabilnošću (STABILNA/PODIJELJENA/NOVA)
   - Raspon vjerojatnosti pobjede sa score breakdownom
   - Confidence faktori i data provenance

3. Precedent Search (Heavy model):
   - RAG simulacija sudske prakse
   - Rangiranje po relevantnosti

4. V45: AI Act Audit Trail (EU 2024/1689 čl. 13):
   - Transparentnost zaključivanja
   - Model, temperatura, reasoning SVAKA korekcija pravne baze zapisana

Spremanje → `legal_strategies` + `audit_logs` (jedan log per citirani precedent)
```

### Supabase Tablice

```sql
legal_strategies (
    id UUID PK, created_at, key_legal_issue TEXT, client_position TEXT,
    win_probability INTEGER, win_probability_min INTEGER, win_probability_max INTEGER,
    precedents_count INTEGER, raw_json_output JSONB, audit_trail JSONB
)

audit_logs (
    id UUID PK, created_at, module TEXT, analysis_id UUID,
    step INTEGER, action TEXT, legal_basis TEXT, citation TEXT,
    reasoning TEXT, confidence TEXT, model_used TEXT, model_temperature NUMERIC
)
```

---

## MODUL 7: e-Fakturiranje (Fiskalizacija)

### Frontend

**Ruta:** `/fiskalizacija` (`frontend/app/fiskalizacija/page.tsx`, 748 redaka)

**Korisnički unos:**
- Podaci izdavatelja: ime, OIB, adresa, grad, poštanski broj
- Podaci klijenta: ime, OIB, adresa, grad, poštanski broj
- Stavke: opis usluge, neto iznos, PDV stopa (0/5/13/25%)
- Datum izdavanja, datum dospijeća
- Modul veza (valuation/contract/chronology/legal), referenca predmeta

**OIB validacija:** ISO 7064 MOD 11,10 algoritam.

**Rezultat:** UBL 2.1 XML preview i download.

### API Endpoints (InvoiceController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/invoice/generate` | Generiranje UBL 2.1 e-računa |
| POST | `/api/invoice/validate-oib` | OIB validacija |

### Backend Pipeline (InvoiceService.cs, 364 redaka)

```
1. Validacija inputa (PDV stope, iznosi, OIB-ovi)
2. Thread-safe sekvencijalno numeriranje (YYYY-NNNNN format)
3. UBL 2.1 XML generiranje:
   - Sukladnost s EN 16931 (europski e-invoicing standard)
   - XML namespaces: urn:oasis:names:specification:ubl:schema:xsd:Invoice-2
   - PayableAmount, TaxTotal, InvoiceLine, AllowanceCharge
4. Spremanje → SupabaseService u `invoices` tablicu
```

### Supabase Tablica

```sql
invoices (
    id UUID PK, created_at, invoice_number TEXT UNIQUE,
    issue_date TEXT, due_date TEXT,
    issuer_name TEXT, issuer_oib TEXT,
    client_name TEXT, client_oib TEXT,
    net_amount NUMERIC, pdv_rate NUMERIC, pdv_amount NUMERIC, gross_amount NUMERIC,
    module TEXT, analysis_id UUID, case_reference TEXT,
    status TEXT, ubl_xml TEXT, raw_json_output JSONB
)
```

---

## MODUL 8: Registar Normi (Norm Validity)

### Frontend

Integriran u ostale module (Contract Analysis koristi temporal lookup za izbor verzije zakona).

### API Endpoints (NormController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| GET | `/api/norm` | Lista svih normi |
| GET | `/api/norm/{abbreviation}` | Lookup po kraticama (ZOO, ZZK, ZV...) |
| GET | `/api/norm/search?q=` | Pretraga po imenu |
| GET | `/api/norm/{abbreviation}/at/{date}` | Temporalni lookup |
| POST | `/api/norm/cite` | Sudski stil citiranja |
| POST | `/api/norm/temporal` | Batch temporalni lookup |

### Backend (NormValidityService.cs, 691 redaka)

```
In-memory registar hrvatskih zakona:
  ZOO (Zakon o obveznim odnosima), ZZK (Zakon o zemljišnim knjigama),
  ZV (Zakon o vlasništvu), OZ (Ovršni zakon), ZPP (Zakon o parničnom postupku),
  ObZ (Obiteljski zakon), i drugi

Funkcionalnosti:
  - Temporalni lookup: koja verzija zakona vrijedi na zadani datum
  - Sudski stil citiranja (genitivni padež za ime zakona)
  - Prompt instruction block generator za AI modele
  - Kronologija amandmana s NN referencama
```

### Supabase Tablice

```sql
norms (
    id UUID PK, abbreviation TEXT UNIQUE, full_name TEXT,
    status TEXT, nn_reference TEXT, nn_reference_short TEXT,
    category TEXT, original_date DATE, last_amendment_date DATE, nn_url TEXT
)

norm_amendments (
    id UUID PK, norm_id UUID FK→norms, nn_number TEXT,
    publication_date DATE, amendment_type TEXT, description TEXT
)
```

---

## MODUL 9: Perplexity Audit (AI Revizija Procjena)

### Frontend

**Ruta:** `/audit` (`frontend/app/audit/page.tsx`, 624 redaka)

**Prikazuje:**
- Verdikti: `CONFIRMED` (zeleno) / `SUSPICIOUS` (žuto) / `REJECTED` (crveno)
- 4 dimenzije s score-ovima (0-100): cijena, lokacija, metodologija, kvaliteta podataka
- Nalazi po ozbiljnosti: INFO / WARNING / CRITICAL
- Tržišni kontekst iz Perplexity web pretrage
- Decision reasoning — detaljno obrazloženje s URL-ovima i brojevima

### API Endpoints (AuditController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| GET | `/api/audit/status` | Provjera Perplexity konfiguracije |
| POST | `/api/audit/single` | Audit jedne procjene |
| POST | `/api/audit/batch` | Batch audit (max 20, 1s delay) |

### Backend Pipeline (PerplexityAuditService.cs, 388 redaka)

```
1. Dohvat procjene iz Supabase → deserijalizacija FullEvaluationResult
2. Konstrukcija prompta:
   - System: "Ti si stručni AI revizor za procjene nekretnina u Hrvatskoj"
   - User: Kompletni podaci procjene (lokacija, cijena, comps, korekcije, mišljenje)
3. API poziv → Perplexity (sonar-pro model, temperature=0.1, web search enabled)
4. Parse strukturiranog JSON odgovora:
   - verdict, overall_confidence, summary, recommendation
   - price_check, location_check, methodology_check, data_quality_check (0-100 svaki)
   - findings[] s category, severity, description, evidence
   - market_context, decision_reasoning
   - citations (URL-ovi koje je Perplexity koristio)
5. Verdikt pravila:
   - CONFIRMED: Svi dimenzijski score-ovi ≥60, nema CRITICAL
   - SUSPICIOUS: Barem 1 score <60 ILI 1 CRITICAL
   - REJECTED: 2+ score-a <40 ILI 2+ CRITICAL
6. Spremanje → `valuation_audits` tablica (UNIQUE po evaluation_id)
```

### Supabase Tablica

```sql
valuation_audits (
    id UUID PK, created_at, evaluation_id UUID FK→evaluations (UNIQUE),
    verdict TEXT, overall_confidence INTEGER,
    summary TEXT, recommendation TEXT,
    price_score INTEGER, location_score INTEGER,
    methodology_score INTEGER, data_quality_score INTEGER,
    findings_count INTEGER, critical_count INTEGER,
    model_used TEXT, prompt_tokens INTEGER, completion_tokens INTEGER,
    raw_json_output JSONB
)
```

---

## MODUL 10: Povijest i Dashboard

### Frontend

**Ruta:** `/history` (`frontend/app/history/page.tsx`, 368 redaka)

**Tabovi:** Sve / Procjene / Ugovori / Kronologije / Pravne analize / e-Računi

**Funkcionalnosti:**
- Dohvat iz Supabase: `getRecentActivity()` i `getEvaluations()`
- PDF export za procjene: `generateValuationPdf()`
- Pretraga i filtriranje

### Backend

**Nema posebnog controllera** — frontend dohvaća direktno iz Supabase putem anon ključa (SELECT only).

---

## MODUL 11: Semantic RAG — Pravne Odredbe

### Frontend

Nema dedicirane stranice — integriran u Contract Analysis (Modul 4).

### API Endpoints (RagController.cs)

| Metoda | Ruta | Opis |
|--------|------|------|
| POST | `/api/rag/seed` | Embeddanje svih članaka zakona u Supabase |
| GET | `/api/rag/test?q=` | Test RAG dohvata s uzorkovim upitom |

### Backend Pipeline (LawRAGService.cs, 411 redaka + EmbeddingService.cs)

```
═══ SEED (jednosmjerni, idempotentni) ═══

1. Učitaj JSON datoteke iz Backend/Data/Laws/*.json
   - Svaka datoteka: { law, fullName, nnReference, articles: [{article, title, content, tags}] }
2. Embed u batchevima od 20 članaka:
   - EmbeddingService → OpenAI text-embedding-3-small (1536 dimenzija) via OpenRouter
   - Tekst za embedding: "{law} čl. {article} — {title}: {content}"
3. Upsert u Supabase putem RPC `upsert_law_chunk()`:
   - ON CONFLICT (law, article) DO UPDATE — siguran za ponovljeno pokretanje

═══ RETRIEVE (poziva se iz ContractAnalysisService) ═══

1. Priprema upita:
   - BuildQueryText() — prvih 2000 znakova ugovora + ključni pravni pojmovi
     ("kupoprodaja nekretnina, cijena, kapara, clausula intabulandi,
      zemljišna knjiga, hipoteka, služnost...")
2. Embedding upita → EmbeddingService.EmbedAsync()
3. Vektorska pretraga → Supabase RPC `match_law_chunks()`:
   - Cosine distance: 1 - (embedding <=> query_embedding) > threshold (0.45)
   - IVFFlat index za brzu pretragu
   - Top-K (default 10) rezultata
4. Formatiranje za prompt:
   - FormatForPrompt() → strukturirani blok teksta:
     "RELEVANTNI ČLANCI HRVATSKOG PRAVA (RAG — stvarni tekst zakona)"
   - Upute modelu: "OBAVEZNO citiraj iz ovih članaka", "NE izmišljaj članke"
   - Sličnost prikazana za svaki članak
5. Injektiranje u AI system prompt za Contract Analysis

═══ FALLBACK ═══
- Ako Supabase nije konfiguriran → graceful degradacija (prazan RAG kontekst)
- Full-text search RPC: search_law_chunks_text() (PostgreSQL tsvector)
- Tag-based search RPC: search_law_chunks_by_tags() (GIN index)
```

### Supabase Tablica + RPC Funkcije

```sql
law_chunks (
    id UUID PK, created_at, law TEXT, full_name TEXT, nn_reference TEXT,
    article TEXT, title TEXT, content TEXT, topic_tags TEXT[],
    embedding vector(1536)  -- pgvector
)

-- Indexes:
--   UNIQUE (law, article)
--   IVFFlat index za cosine distance (lists=10)
--   GIN index za full-text search
--   GIN index za topic_tags

-- RPC Funkcije:
--   match_law_chunks(query_embedding, threshold, count) — vektorska pretraga
--   search_law_chunks_text(query, max) — full-text fallback
--   search_law_chunks_by_tags(tags[], max) — tag pretraga
--   upsert_law_chunk(...) — idempotentni upsert
```

---

## Sažetak Integracija

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND (Next.js)                       │
│  /valuation  /wizard  /zk-procjena  /ugovor  /kronologija      │
│  /sudska-praksa  /fiskalizacija  /audit  /history               │
└────────────────────────────┬────────────────────────────────────┘
                             │ SSE / REST (fetch)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     BACKEND (ASP.NET Core)                      │
│                                                                 │
│  Controllers:                                                   │
│    EvaluateController ──→ ValuationService                      │
│                           ├─ PdfService (PdfPig)                │
│                           ├─ MarketResearchService (Tavily+BD)  │
│                           ├─ TextAnalysisService (Sherlock)     │
│                           ├─ ValuationAgentService (AI context) │
│                           ├─ ValuationEngine (deterministički)  │
│                           └─ EmailService (Brevo)               │
│                                                                 │
│    ZkUnifiedController ──→ ZkParsingService                     │
│                           └─ (reuses Evaluation pipeline)       │
│                                                                 │
│    ContractController ──→ ContractAnalysisService                │
│                           └─ LawRAGService (pgvector)           │
│                                                                 │
│    ChronologyController → ChronologyService                     │
│    LegalController ─────→ LegalIntelligenceService              │
│    InvoiceController ───→ InvoiceService                        │
│    NormController ──────→ NormValidityService                   │
│    AuditController ─────→ PerplexityAuditService                │
│    RagController ───────→ LawRAGService + EmbeddingService      │
│    ValuationDraftController → FluentValidation                  │
│    HealthController ────→ /api/health                           │
│                                                                 │
│  Shared:                                                        │
│    ChatClientProvider (Fast/Heavy/Reasoning via OpenRouter)      │
│    SupabaseService (PostgREST client)                           │
│    SseHelpers (SSE streaming utilities)                         │
└──────────────────────────┬──────────────────────────────────────┘
                           │ PostgREST / RPC
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    SUPABASE (PostgreSQL)                         │
│                                                                 │
│  evaluations        — procjene nekretnina                       │
│  zk_analyses        — ZK due diligence                          │
│  contract_analyses  — analize ugovora                           │
│  chronologies       — kronologije                               │
│  legal_strategies   — pravne strategije (+ audit_trail JSONB)   │
│  invoices           — e-računi (UBL 2.1)                        │
│  valuation_audits   — Perplexity revizije                       │
│  audit_logs         — EU AI Act compliance trail                │
│  norms              — registar zakona                           │
│  norm_amendments    — amandmani zakona                          │
│  law_chunks         — pgvector embeddinzi za RAG               │
│                                                                 │
│  RLS: service_role=full, anon=SELECT only                       │
│  Extensions: uuid-ossp, vector (pgvector)                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Ključne Arhitektonske Odluke

1. **Deterministički Engine + AI Narativ** — ValuationEngine koristi čistu matematiku (nema AI u calculu), AI generira samo stručno mišljenje na temelju engine podataka. Ovo osigurava ponovljivost i revizijsku uporabu.

2. **Multi-Model Routing** — Jeftini model (GPT-4o-mini) za ekstrakciju, skupi model (DeepSeek V3) za rezoniranje. Jedna API ključ pokriva sve modele.

3. **SSE Streaming** — Svi dugi pipeline-ovi koriste Server-Sent Events za real-time feedback korisniku. `SseHelpers` klasa centralizira logiku.

4. **Graceful Degradation** — Svaki vanjski servis (Perplexity, Supabase, BrightData) ima fallback. RAG radi bez vektorske pretrage, audit radi bez Perplexity-ja, tržišno istraživanje radi bez BrightData-e.

5. **EU AI Act Compliance (V45)** — Svaka pravna analiza zapisuje audit trail s modelom, temperaturom, pravnom bazom i razlozima. Pohranjeno u `audit_logs` tablici.

6. **Temporal Law Versioning (V42)** — Ugovori iz prošlosti se analiziraju prema zakonu koji je vrijedio na datum potpisivanja, ne prema trenutnom zakonu.

7. **V28 Anti-Duplikacija** — Sprečava da ista nekretnina objavljena na više portala/agencija napuše uzorak usporedivih.

8. **V29 Sherlock Authority** — Semantička analiza teksta oglasa preuzima autoritet nad scraper podacima (npr. Sherlock kaže "renovirano" → override AI condition "good").
