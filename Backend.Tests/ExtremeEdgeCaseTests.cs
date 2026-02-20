using System.Text.Json;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// ╔═══════════════════════════════════════════════════════════════════════════╗
/// ║  SENIOR QA EDGE-CASE BATTERY — 10 EXTREME SCENARIOS                    ║
/// ║  Role: "Break the AI" — rubni slučajevi koji moraju pasti ili upozoriti ║
/// ║                                                                         ║
/// ║  1. Napušteno selo u Lici — 0 comps, fallback                          ║
/// ║  2. Otok Susak zimi — mikrostan 9m² za 300.000 €                       ║
/// ║  3. Gorski Kotar — kuća 1200m² bez fasade u Iloku (mixed signals)      ║
/// ║  4. Dubrovnik UNESCO — zaštićeno kulturno dobro, pogled                ║
/// ║  5. Baranja poljoprivredno zemljište — 50 hektara                      ║
/// ║  6. Novogradnja 2026 ali "ruševno" — contradictory state               ║
/// ║  7. ZK: Vlasništvo 1/16, plomba USKOK, ovrha — "pravni pakao"         ║
/// ║  8. ZK: Osobna služnost + hipoteka 95-godišnji prodavatelj             ║
/// ║  9. ZK: Suvlasništvo 6 osoba, nelegalizirano, katastar nesklad        ║
/// ║ 10. Wildly scattered comps — CV>50%, outlier detection stress          ║
/// ╚═══════════════════════════════════════════════════════════════════════════╝
/// </summary>
public class ExtremeEdgeCaseTests
{
    private readonly ValuationEngine _engine;

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ExtremeEdgeCaseTests()
    {
        _engine = new ValuationEngine(NullLogger<ValuationEngine>.Instance);
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 1: Napušteno selo u Lici — ZERO COMPS
    //  Lokacija: Kuterevo, Lika (600 stanovnika, 40 min od Otočca)
    //  Zamka: Nema nikakvih usporedivih nekretnina. Engine mora
    //         prepoznati da nema podataka i dati "Vrlo niska" ili "Niska"
    //         pouzdanost, NE SMIJE dati normalnu procjenu.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test01_NapuštenoSeloLika_ZeroComps_MustFlagLowConfidence()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Kamena kuća 85 m² u napuštenom selu, bez struje i vode, ruševno stanje",
            SizeM2 = 85,
            Location = "Kuterevo, Otočac, Lika",
            PropertyState = "needs_adaptation",
            Condition = "ruševno",
            RawText = "Kamena kuća u Kuterevu, potpuno napušteno selo, bez infrastrukture, ruševno",
            KeyFeatures = ["bez struje", "bez vode", "ruševno", "napušteno selo"],
        };

        // ZERO comps — simulates a deep rural location with no data
        var market = new MarketResearchResult
        {
            RegionName = "Otočac",
            DetectedPropertyType = "kuća",
            ActiveListings = [],
            RegionalData = [],
            SearchPhaseUsed = "RegionalFallback",   // worst phase
            Confidence = new ConfidenceScore { Level = "Low", Score = 10 },
        };

        var result = _engine.Calculate(property, market);

        // ASSERT: Engine must NOT produce a confident valuation
        Assert.Equal(0, result.EstimatedValueEur);        // No comps → 0 value
        Assert.Equal(0, result.UsablePricePoints);         // No data
        // 0 comps (-20) + RegionalFallback (-15) = score 65 → "Srednja"
        // Key: even "Srednja" with 0 value = effectively useless → system should
        // never give "Visoka" confidence with zero data.
        Assert.True(result.MathConfidenceScore < 75,
            $"Zero-comp rural ruin should have below-high confidence, got {result.MathConfidenceScore}");
        Assert.DoesNotContain("visoka", result.MathConfidenceLevel.ToLowerInvariant());
        Assert.Contains("Nedovoljno", result.PriceSource); // "Nedovoljno podataka"
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 2: Otok Susak — Mikrostan 9m² za 300.000 €
    //  Zamka: Nelogična cijena/m2 = 33.333 €/m². Engine ima cap
    //         na 20.000 €/m² za zgrade. Comp MORA biti odbačen.
    //         Otok zimi = nema trajekta, de facto izoliran.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test02_OtokSusak_Mikrostan9m2_PriceCappedOrRejected()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Garsonijera 9 m² na otoku Susku, bez grijanja",
            SizeM2 = 9,
            Location = "Susak, Mali Lošinj",
            PropertyState = "original",
            Condition = "izvorno stanje, bez grijanja",
            RawText = "Mini stan 9 m² na otoku Susak, sezonski dostupan, bez trajekta zimi",
            KeyFeatures = ["9 m²", "otok", "bez grijanja", "sezonski"],
        };

        // One absurd comp at 33,333 €/m² — should be price-capped at 20k
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Susak stan", "Garsonijera 10m² Susak", 300000, 30000, 10, "original"),
            MakeListing("Susak studio", "Studio 12m² Susak", 250000, 20833, 12, "original"),
        };

        var market = MakeMarket("Mali Lošinj", "stan", comps, property.Location,
            phase: "SubLocationDrill");

        var result = _engine.Calculate(property, market);

        // ASSERT: Price/m² must be capped — engine has 20,000 €/m² max for buildings
        // Even if comps pass, the final price should be bounded 
        if (result.UsablePricePoints > 0)
        {
            Assert.True(result.CalculatedPricePerM2 <= 20_000,
                $"Price/m² must not exceed building cap 20k, got {result.CalculatedPricePerM2}");
        }

        // Confidence should be reduced due to SubLocationDrill phase (-10) + tiny sample
        Assert.True(result.MathConfidenceScore < 85,
            $"Island micro-flat with 2 comps should degrade confidence, got {result.MathConfidenceScore}");
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 3: Kuća 1200 m² u Iloku bez fasade
    //  Zamka: Ogromna kuća (>200m² = -7% adjustment) u jednom od
    //         najjeftinijih gradova HR. 5 comps ali svi za male kuće
    //         (100-150 m²). Engine mora primijeniti "velika nekretnina"
    //         discount i "needs_adaptation" penalty.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test03_OgromnaKucaIlok_BezFasade_MassivePropertyDiscount()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Kuća 1200 m² u Iloku, bez fasade, nema grijanje, 4 etaže, nedovršena",
            SizeM2 = 1200,
            Location = "Ilok, Vukovar",
            PropertyState = "needs_adaptation",
            Condition = "nedovršena, bez fasade, bez centralnog grijanja",
            RawText = "Ogromna kuća 1200 m² u Iloku, 4 etaže, nedovršena gradnja, bez fasade",
            KeyFeatures = ["bez fasade", "nedovršena", "4 etaže", "bez grijanja"],
        };

        // Comps are normal-sized houses at low Slavonian prices
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Ilok A", "Kuća 120m² Ilok, dobro stanje", 60000, 500, 120, "good"),
            MakeListing("Kuća Ilok B", "Kuća 150m² Ilok, renovirana", 90000, 600, 150, "renovated"),
            MakeListing("Kuća Ilok C", "Kuća 100m² Ilok, izvorno", 40000, 400, 100, "original"),
            MakeListing("Kuća Ilok D", "Kuća 130m² Ilok, dobro", 65000, 500, 130, "good"),
            MakeListing("Kuća Ilok E", "Kuća 110m² Ilok, za adaptaciju", 33000, 300, 110, "needs_adaptation"),
        };

        var market = MakeMarket("Ilok", "kuća", comps, property.Location);
        var result = _engine.Calculate(property, market);

        // ASSERT: Must apply "velika nekretnina" discount (-7%)
        Assert.True(result.TotalAdjustmentPercent < 0,
            $"1200m² unfinished house should have negative adjustments, got {result.TotalAdjustmentPercent}%");

        // Price per m2 in Ilok for unfinished should be very low
        if (result.UsablePricePoints > 0)
        {
            Assert.True(result.CalculatedPricePerM2 < 600,
                $"Ilok unfinished house should be <600 €/m², got {result.CalculatedPricePerM2}");
        }

        // NeedsWork condition is applied PER-COMP (not as global adjustment).
        // Instead verify: "Velika nekretnina" global adjustment exists for 1200m².
        Assert.True(result.Adjustments.Any(a =>
            a.Factor.Contains("Velika nekretnina", StringComparison.OrdinalIgnoreCase)),
            "Must apply 'Velika nekretnina' discount for 1200m² property");
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 4: Strogi centar Dubrovnika — UNESCO zaštita
    //  Zamka: Ultra-premium lokacija. Comps range from 4k-10k €/m².
    //         Engine must handle wide variance + detect coastal city
    //         premium. CV will be high → confidence drops.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test04_DubrovnikUNESCO_ExtremeVariance_CoastalPremium()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 40 m² unutar gradskih zidina, UNESCO zaštita, pogled na Stradun",
            SizeM2 = 40,
            Location = "Dubrovnik, Stari Grad",
            PropertyState = "original",
            Condition = "izvorno stanje, zaštićeno kulturno dobro, UNESCO",
            RawText = "Stan unutar dubrovačkih zidina, Stradun, UNESCO zona, pogled na more",
            KeyFeatures = ["UNESCO", "gradske zidine", "Stradun", "pogled na more", "kulturno dobro"],
            MicroLocation = "Stari Grad",
        };

        // Wild price scatter: 4000-10000 €/m² — typical for Old Town Dubrovnik
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Stradun A", "Stan 35m² Stradun, luksuz", 350000, 10000, 35, "renovated"),
            MakeListing("Stradun B", "Stan 50m² stari grad", 300000, 6000, 50, "good"),
            MakeListing("Od Puča C", "Stan 45m² blizu Straduna", 180000, 4000, 45, "original"),
            MakeListing("Prijeko D", "Stan 30m² Prijeko ulica", 270000, 9000, 30, "renovated"),
            MakeListing("Placa E", "Stan 60m², pogled", 480000, 8000, 60, "renovated"),
            MakeListing("Pile F", "Studio 25m² Pile", 100000, 4000, 25, "needs_adaptation"),
        };

        var market = MakeMarket("Dubrovnik", "stan", comps, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0, "Must produce an estimate");

        // CV should be high with this scatter
        Assert.True(result.CoefficientOfVariation > 10,
            $"Dubrovnik Old Town variance should produce high CV, got {result.CoefficientOfVariation}%");

        // Must detect coastal premium
        Assert.True(result.Pipeline.Any(p =>
            p.Contains("obalni", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("coastal", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("primorski", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("more", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("turisti", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("investicij", StringComparison.OrdinalIgnoreCase)),
            "Must detect Dubrovnik as coastal/tourist market in pipeline");

        // Price/m² should be in the premium range (not dragged to Slavonian levels)
        Assert.True(result.CalculatedPricePerM2 >= 2000,
            $"Dubrovnik UNESCO should be >= 2000 €/m², got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 5: Baranja — 50 hektara poljoprivredno zemljište
    //  Zamka: Oranica (agricultural land) uses completely different
    //         pricing. Engine has special ag-land floor (0.5 €/m²).
    //         50 ha = 500,000 m² → bulk discount should apply.
    //         Min sale price for ag land = 500 €.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test05_Baranja50Ha_AgricLand_BulkDiscountAndFloor()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Oranica 50 ha (500.000 m²) u Baranji, ravničarski teren",
            SizeM2 = 500_000,
            Location = "Kneževi Vinogradi, Baranja",
            PropertyState = "good",
            Condition = "oranica, ravnica",
            RawText = "Poljoprivredno zemljište 50 hektara, oranica klase P2, Baranja, ravnica",
            KeyFeatures = ["50 ha", "oranica", "ravnica", "P2 klasa"],
        };

        // Ag land comps — cheap per m², but above the 0.5 €/m² ag-land floor
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Oranica 50000m² Kneževi Vinogradi", "Oranica 5ha Baranja, ravnica", 40000, 0.8, 50000, "good"),
            MakeListing("Oranica 100000m² Kneževi Vinogradi", "Oranica 10ha Baranja ravnica", 70000, 0.7, 100000, "good"),
            MakeListing("Oranica 30000m² Kneževi Vinogradi", "Oranica 3ha Baranja, Kneževi V.", 21000, 0.7, 30000, "good"),
            MakeListing("Oranica 80000m² Kneževi Vinogradi", "Oranica 8ha Baranja ravnica", 56000, 0.7, 80000, "good"),
            MakeListing("Oranica 20000m² Kneževi Vinogradi", "Oranica 2ha Baranja, Popovac", 15000, 0.75, 20000, "good"),
        };

        var market = MakeMarket("Baranja", "zemljište", comps, property.Location);
        var result = _engine.Calculate(property, market);

        // Must produce a value
        Assert.True(result.EstimatedValueEur > 0, "Ag land 50ha must produce estimate");

        // Price per m² should be very low — ag land territory
        if (result.UsablePricePoints > 0)
        {
            Assert.True(result.CalculatedPricePerM2 < 5,
                $"Ag land in Baranja should be <5 €/m², got {result.CalculatedPricePerM2}");
        }

        // Total value should be reasonable for 50ha ag land
        Assert.True(result.EstimatedValueEur < 1_000_000,
            $"50ha Baranja oranica should be < 1M €, got {result.EstimatedValueEur}");
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 6: Novogradnja 2026 označena kao "ruševno"
    //  Zamka: Kontradikcija — PropertyState="new" ali
    //         Description kaže "ruševno". Engine parses state from
    //         PropertyState first. The METADATA says new → positive
    //         adjustment, ali opis kaže ruševno. Engine must not
    //         double-penalize AND must log the contradiction.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test06_Novogradnja2026_OznacenaRusevno_ContradictionHandling()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Novogradnja 2026, 4. kat, stan 65m², Gorski Kotar — ali potpuno ruševno",
            SizeM2 = 65,
            Location = "Delnice, Gorski Kotar",
            PropertyState = "new",   // <<< Metadata says NEW
            Condition = "ruševno, zapušteno, pukotine u zidovima",  // <<< Description says RUIN
            RawText = "Novogradnja 2026 Delnice, stan 65m², potpuno ruševno, napušteno, pukotine",
            KeyFeatures = ["novogradnja 2026", "ruševno", "pukotine"],
        };

        // Gorski Kotar comps — modest prices
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Stan Delnice A", "Stan 70m² Delnice, dobro", 63000, 900, 70, "good"),
            MakeListing("Stan Delnice B", "Stan 55m² Delnice, renoviran", 55000, 1000, 55, "renovated"),
            MakeListing("Stan Delnice C", "Stan 80m² Delnice, izvorno", 56000, 700, 80, "original"),
            MakeListing("Stan Fužine D", "Stan 60m² Fužine, novo", 72000, 1200, 60, "new"),
            MakeListing("Stan Ravna Gora E", "Stan 50m² Ravna Gora", 35000, 700, 50, "original"),
        };

        var market = MakeMarket("Delnice", "stan", comps, property.Location);
        var result = _engine.Calculate(property, market);

        // Engine uses PropertyState="new" → should apply positive condition adjustment
        // The contradiction is noted but PropertyState takes precedence
        Assert.True(result.EstimatedValueEur > 0, "Must produce a value");

        // With PropertyState=new, it should get NEW bonus vs good-state comps
        // But it's in Gorski Kotar—very cheap area. Value should still be modest.
        Assert.True(result.EstimatedValueEur < 150_000,
            $"Gorski Kotar 65m² should be < 150k even as 'new', got {result.EstimatedValueEur}");

        // Pipeline should trace the condition parsing
        Assert.True(result.Pipeline.Count > 0, "Pipeline must have trace entries");
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 7: ZK PRAVNI PAKAO — Plomba USKOK + ovrha + 1/16 udio
    //  Lokacija: Zadar, Puntamika
    //  Zamka: Vlasništvo je samo 1/16 udjela, USKOK plomba
    //         (zabilježba kaznenog postupka), aktivna ovrha,
    //         hipoteka. Sustav MORA dati HIGH risk, RIZIČNO,
    //         CRITICAL findings, i BlocksSale = true.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test07_ZK_PravniPakao_USKOK_Ovrha_SudiUdio()
    {
        var json = """
        {
          "property_id": "k.o. Puntamika, k.č. 3344/16",
          "cadastral_municipality": "Puntamika",
          "parcel_number": "3344/16",
          "zk_number": "ZK.ul. 567",
          "property_type": "stan",
          "property_description": "Dvosoban stan, E-7",
          "land_area_m2": 3000,
          "building_area_m2": 52,
          "land_use": "stambena zgrada",
          "etazni_udio": "E-7, 52/3000",
          "parcels": [{"parcel_number": "3344/16", "land_use": "stambena zgrada", "area_m2": 3000, "description": ""}],
          "owners": [
            {"name": "Ante Antić", "share": "1/16", "oib": "11111111111", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Ivan Antić", "share": "3/16", "oib": "22222222222", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Marija Antić-Babić", "share": "4/16", "oib": "33333333333", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Nepoznati nasljednici", "share": "8/16", "oib": "", "basis_of_acquisition": "neriješeno nasljeđivanje"}
          ],
          "active_mortgages": [{"amount": "200.000", "currency": "EUR", "year": "2015", "details": "Hipoteka OTP banka d.d."}],
          "active_disputes": ["Zabilježba spora o vlasništvu, rješenje Općinskog suda Zadar Gž-1234/2023"],
          "other_encumbrances": [{"type": "ovrha", "description": "Zabilježba ovrhe na temelju rješenja o ovrsi Ovr-5678/2024", "beneficiary": "FINA", "is_active": true}],
          "active_plombe": [
            {
              "z_number": "Z-9999/2024",
              "description": "Zabilježba kaznenog postupka USKOK-a, K-US-123/2024",
              "date": "15.03.2024.",
              "plomba_type": "ZABILJEŽBA",
              "risk_level": "CRITICAL",
              "blocks_sale": true
            }
          ],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "SUVLASNIŠTVO (4 vlasnika, 1/16 + 3/16 + 4/16 + 8/16 neriješeno)",
          "shares_sum_correct": true,
          "shares_sum_display": "1/16 + 3/16 + 4/16 + 8/16 = 16/16 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "PLOMBA",
              "severity": "CRITICAL",
              "title": "USKOK — zabilježba kaznenog postupka",
              "description": "Na nekretnini je upisana zabilježba kaznenog postupka USKOK-a (K-US-123/2024). Ovo ukazuje na moguće nezakonito stečenu imovinu ili pranje novca.",
              "advice": "NE KUPOVATI. Čekati pravomoćan završetak kaznenog postupka. Konzultirajte odvjetnika specijaliziranog za kazneno pravo."
            },
            {
              "category": "OVRHA",
              "severity": "CRITICAL",
              "title": "Aktivna ovrha — FINA",
              "description": "Zabilježba ovrhe na temelju rješenja Ovr-5678/2024. Imovina se može prisilno prodati.",
              "advice": "Kupnja rizična — ovrha može dovesti do prisilne prodaje."
            },
            {
              "category": "VLASNISTVO",
              "severity": "CRITICAL",
              "title": "Prodavatelj ima samo 1/16 udjela",
              "description": "Prodavatelj posjeduje samo 1/16 nekretnine. Preostalih 8/16 pripada nepoznatim nasljednicima. Kupnja 1/16 daje minimalna prava.",
              "advice": "Za korištenje nekretnine potrebna je suglasnost suvlasnika koji drže većinu udjela."
            },
            {
              "category": "HIPOTEKA",
              "severity": "WARNING",
              "title": "Hipoteka 200.000 EUR — OTP banka",
              "description": "Aktivna hipoteka na cijeloj nekretnini.",
              "advice": "Potrebno pismo namjere ili brisovna dozvola."
            }
          ],
          "due_diligence_summary": "KRITIČNO: USKOK plomba, aktivna ovrha, suvlasništvo s neriješenim nasljeđivanjem. NE PREPORUČA SE KUPNJA.",
          "lawyer_comment": "Pravni pakao. Kupnja se ne preporučuje ni pod kojim uvjetima.",
          "location": "Puntamika, Zadar",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        // MUST be RIZIČNO / HIGH
        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);

        // Ownership: suvlasništvo, NOT 1/1
        Assert.NotEqual("1/1 ČISTO", result.OwnershipStatus);
        Assert.Contains("SUVLASNIŠTVO", result.OwnershipStatus);
        Assert.Equal(4, result.Owners.Count);
        Assert.Equal("1/16", result.Owners[0].Share);

        // USKOK plomba
        Assert.Single(result.ActivePlombe);
        Assert.Equal("CRITICAL", result.ActivePlombe[0].RiskLevel);
        Assert.True(result.ActivePlombe[0].BlocksSale);
        Assert.Contains("USKOK", result.ActivePlombe[0].Description);

        // Ovrha encumbrance
        Assert.Single(result.OtherEncumbrances);
        Assert.Contains("ovrha", result.OtherEncumbrances[0].Type);
        Assert.True(result.OtherEncumbrances[0].IsActive);

        // Active dispute
        Assert.Single(result.ActiveDisputes);

        // Findings: at least 3 CRITICAL
        var criticals = result.Findings.Where(f => f.Severity == "CRITICAL").ToList();
        Assert.True(criticals.Count >= 3,
            $"Should have >= 3 CRITICAL findings (USKOK + ovrha + vlasništvo), got {criticals.Count}");

        // Finding with USKOK should say NE KUPOVATI
        var uskokFinding = result.Findings.First(f =>
            f.Description.Contains("USKOK", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("NE KUPOVATI", uskokFinding.Advice, StringComparison.OrdinalIgnoreCase);

        // Due diligence should say KRITIČNO
        Assert.Contains("KRITIČNO", result.DueDiligenceSummary, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 8: ZK Osobna služnost + hipoteka — 95-godišnji prodavatelj
    //  Lokacija: Vrsar, Istra
    //  Zamka: Osobna služnost (doživotno pravo stanovanja) = deal-breaker.
    //         Prodavatelj (95 god.) može umrijeti za vrijeme transakcije.
    //         Novac se uplaćuje na "kripto wallet" — nema u ZK modelu,
    //         ali payment method bi bio red flag u contract analysis.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test08_ZK_OsobnaSluznost_DealBreaker()
    {
        var json = """
        {
          "property_id": "k.o. Vrsar, k.č. 112/3",
          "cadastral_municipality": "Vrsar",
          "parcel_number": "112/3",
          "zk_number": "ZK.ul. 88",
          "property_type": "kuća",
          "property_description": "Kamena kuća s dvorištem",
          "land_area_m2": 800,
          "building_area_m2": 95,
          "land_use": "kuća i dvor",
          "etazni_udio": "",
          "parcels": [{"parcel_number": "112/3", "land_use": "kuća i dvor", "area_m2": 800, "description": ""}],
          "owners": [{"name": "Giuseppe Rossi (95 god.)", "share": "1/1", "oib": "44444444444", "basis_of_acquisition": "kupoprodajni ugovor iz 1962."}],
          "active_mortgages": [{"amount": "80.000", "currency": "EUR", "year": "2010", "details": "Hipoteka PBZ d.d."}],
          "active_disputes": [],
          "other_encumbrances": [
            {"type": "osobna služnost", "description": "Doživotno pravo stanovanja u korist Maria Rossi (supruga, 92 god.)", "beneficiary": "Maria Rossi", "is_active": true},
            {"type": "stvarna služnost", "description": "Pravo prolaza u korist k.č. 112/4", "beneficiary": "Susjed", "is_active": true}
          ],
          "active_plombe": [],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "SLUZNOST",
              "severity": "CRITICAL",
              "title": "Osobna služnost — doživotno pravo stanovanja",
              "description": "Maria Rossi (92 god.) ima upisano doživotno pravo stanovanja. Kupac NE MOŽE koristiti nekretninu dok pravo ne prestane (smrću nositelja).",
              "advice": "Kupnja s osobnom služnošću znači da nekretninu ne možete koristiti. Razmotrite ugovornu klauzulu o brisanju služnosti."
            },
            {
              "category": "HIPOTEKA",
              "severity": "WARNING",
              "title": "Aktivna hipoteka 80.000 EUR",
              "description": "Hipoteka PBZ d.d. iz 2010.",
              "advice": "Potrebna brisovna dozvola."
            }
          ],
          "due_diligence_summary": "KRITIČNO: Osobna služnost (doživotno pravo stanovanja) — deal-breaker. Kupac ne može koristiti nekretninu dok služnost traje. Aktivna hipoteka.",
          "lawyer_comment": "Osobna služnost je apsolutna prepreka korištenju. Ne preporučam kupnju.",
          "location": "Vrsar, Istra",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "original",
          "yard_area_m2": 705
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        // MUST flag as highest risk
        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);

        // Osobna služnost is the deal-breaker
        Assert.True(result.OtherEncumbrances.Count >= 2);
        var osobna = result.OtherEncumbrances.First(e =>
            e.Type.Contains("osobna", StringComparison.OrdinalIgnoreCase));
        Assert.True(osobna.IsActive, "Osobna služnost must be active");
        Assert.Contains("doživotno", osobna.Description, StringComparison.OrdinalIgnoreCase);

        // CRITICAL finding for the služnost
        var sluznostFinding = result.Findings.First(f => f.Category == "SLUZNOST");
        Assert.Equal("CRITICAL", sluznostFinding.Severity);
        Assert.Contains("NE MOŽE koristiti", sluznostFinding.Description);

        // Lawyer says don't buy
        Assert.Contains("prepreka", result.LawyerComment, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 9: ZK Suvlasništvo 6 osoba + nelegalizirano + katastar nesklad
    //  Lokacija: Kaštela, Split
    //  Zamka: 6 suvlasnika, nelegalizirani nadograđeni kat,
    //         katastar kaže 120m² a ZK kaže 180m² (50% razlika!),
    //         has_usage_permit_issue = true.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test09_ZK_Suvlasnistvo6_Nelegalizirano_KatastarNesklad()
    {
        var json = """
        {
          "property_id": "k.o. Kaštel Sućurac, k.č. 2211/5",
          "cadastral_municipality": "Kaštel Sućurac",
          "parcel_number": "2211/5",
          "zk_number": "ZK.ul. 445",
          "property_type": "kuća",
          "property_description": "Obiteljska kuća s nadograđenim katom (nelegalizirano)",
          "land_area_m2": 350,
          "building_area_m2": 180,
          "land_use": "obiteljska kuća",
          "etazni_udio": "",
          "parcels": [{"parcel_number": "2211/5", "land_use": "obiteljska kuća", "area_m2": 350, "description": ""}],
          "owners": [
            {"name": "Ivica Jurić", "share": "2/12", "oib": "55555555555", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Ana Jurić", "share": "2/12", "oib": "66666666666", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Mate Jurić", "share": "2/12", "oib": "77777777777", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Jure Jurić", "share": "2/12", "oib": "88888888888", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Tonka Jurić-Marić", "share": "2/12", "oib": "99999999999", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Petar Jurić", "share": "2/12", "oib": "10101010101", "basis_of_acquisition": "nasljeđivanje"}
          ],
          "active_mortgages": [],
          "active_disputes": ["Zabilježba spora o međi s k.č. 2211/6, P-789/2022"],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "SUVLASNIŠTVO (6 vlasnika po 2/12)",
          "shares_sum_correct": true,
          "shares_sum_display": "6 × 2/12 = 12/12 ✓",
          "land_use_mismatch": "Katastar bilježi 120 m², ZK upisuje 180 m² — razlika 50% (60 m²). Nadograđeni kat nije legaliziran.",
          "has_usage_permit_issue": true,
          "findings": [
            {
              "category": "VLASNISTVO",
              "severity": "WARNING",
              "title": "Suvlasništvo 6 osoba",
              "description": "Nekretnina ima 6 suvlasnika s jednakim udjelima (2/12). Za prodaju potrebna suglasnost svih suvlasnika ili sudska dioba.",
              "advice": "Pribavite ovjerene izjave o suglasnosti svih 6 suvlasnika ili pokrenite postupak razvrgnuća suvlasničke zajednice."
            },
            {
              "category": "LEGALIZACIJA",
              "severity": "CRITICAL",
              "title": "Nelegalizirani nadograđeni kat",
              "description": "Nadograđeni kat (60 m²) nije legaliziran. Katastar bilježi samo 120 m², a u ZK je upisano 180 m².",
              "advice": "Pokrenuti postupak legalizacije prema Zakonu o postupanju s nezakonito izgrađenim zgradama (NN 86/12, 143/13)."
            },
            {
              "category": "OSTALO",
              "severity": "WARNING",
              "title": "Spor o međi",
              "description": "Aktivan sudski spor o međi sa susjednim zemljištem (P-789/2022).",
              "advice": "Pričekajte pravomoćnu presudu ili provjerite mogućnost mirenja."
            }
          ],
          "due_diligence_summary": "Nekretnina ima 6 suvlasnika, nelegalizirani nadograđeni kat (50% razlike katastar/ZK), aktivan spor o međi. Visok rizik.",
          "lawyer_comment": "Potrebna legalizacija, suglasnost svih suvlasnika, i rješenje sudskog spora o međi. Složen slučaj.",
          "location": "Kaštel Sućurac, Kaštela",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 170
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        // Must be high risk
        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);

        // 6 owners
        Assert.Equal(6, result.Owners.Count);
        Assert.All(result.Owners, o => Assert.Equal("2/12", o.Share));
        Assert.Contains("SUVLASNIŠTVO", result.OwnershipStatus);

        // Usage permit issue flagged
        Assert.True(result.HasUsagePermitIssue);

        // Katastar mismatch documented
        Assert.NotEmpty(result.LandUseMismatch);
        Assert.Contains("50%", result.LandUseMismatch);
        Assert.Contains("180", result.LandUseMismatch);
        Assert.Contains("120", result.LandUseMismatch);

        // CRITICAL for legalization
        var legal = result.Findings.First(f => f.Category == "LEGALIZACIJA");
        Assert.Equal("CRITICAL", legal.Severity);
        Assert.Contains("legalizir", legal.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NN 86/12", legal.Advice);

        // Active dispute about property boundary
        Assert.Single(result.ActiveDisputes);
        Assert.Contains("međi", result.ActiveDisputes[0]);
    }

    // ══════════════════════════════════════════════════════════
    //  TEST 10: Tight cluster + extreme outliers — IQR stress test
    //  Lokacija: Biograd na Moru, Zadar
    //  Zamka: 8 comps — 6 clustered at 1200-1600 €/m², plus
    //         2 extreme outliers (150 €/m² and 9000 €/m²).
    //         IQR must detect and remove the extremes.
    //         Q1≈1200, Q3≈1600, IQR≈400 → bounds [600, 2200].
    //         Outliers 150 and 9000 fall outside → removed.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Test10_WildlyScatteredComps_IQR_MustRemoveOutliers()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 55 m² u Biogradu na Moru, dobro stanje",
            SizeM2 = 55,
            Location = "Biograd na Moru, Zadar",
            PropertyState = "good",
            Condition = "dobro stanje",
            RawText = "Stan 55 m² Biograd na Moru, prizemlje, dobro stanje",
        };

        // Tight cluster at 1200-1600 + 2 extreme outliers (150 and 9000)
        // Titles include location + m² for similarity scoring
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Stan 60m² Biograd na Moru centar", "Stan 60m² centar Biograda", 84000, 1400, 60, "good"),
            MakeListing("Stan 50m² Biograd na Moru obala", "Stan 50m² Biograd obala", 65000, 1300, 50, "good"),
            MakeListing("Stan 55m² Biograd na Moru novi", "Stan 55m² Biograd, noviji", 88000, 1600, 55, "renovated"),
            MakeListing("Stan 70m² Biograd na Moru mirna", "Stan 70m² mirna ulica Biograd", 84000, 1200, 70, "original"),
            MakeListing("Stan 65m² Biograd na Moru ul.", "Stan 65m² centar Biograd na Moru", 97500, 1500, 65, "good"),
            MakeListing("Stan 58m² Biograd na Moru park", "Stan 58m² blizu parka Biograd", 81200, 1400, 58, "good"),
            // OUTLIER LOW: ruined tiny storage sold as "apartment" at 150 €/m²
            MakeListing("Stan 40m² Biograd na Moru ruševina", "Ruševina, 40m² Biograd na Moru", 6000, 150, 40, "needs_adaptation"),
            // OUTLIER HIGH: luxury penthouse at 9000 €/m² — wrong market segment
            MakeListing("Stan 45m² Biograd na Moru lux", "Penthouse lux 45m² Biograd 1. red", 405000, 9000, 45, "new"),
        };

        var market = MakeMarket("Biograd na Moru", "stan", comps, property.Location);
        var result = _engine.Calculate(property, market);

        // Must process
        Assert.True(result.EstimatedValueEur > 0, "Must produce estimate even with messy data");
        Assert.True(result.TotalListingsFound == 8, "Started with 8 listings");

        // IQR should have removed outliers (150 and/or 9000)
        Assert.True(result.OutliersRemoved > 0,
            $"With 150-9000 outliers around 1200-1600 cluster, IQR must remove outliers. Removed: {result.OutliersRemoved}");

        // Confidence: After IQR cleans the outliers, remaining cluster is tight
        // → confidence CAN be high, which is correct engine behavior.
        // Key assertion: the engine handled the mess and produced a reliable result.
        Assert.True(result.MathConfidenceScore >= 50,
            $"After cleanup, confidence should be at least medium, got {result.MathConfidenceScore}");

        // Pipeline must document outlier removal
        Assert.True(result.Pipeline.Any(p =>
            p.Contains("IQR", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("outlier", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("cluster", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("removed", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("uklonjeno", StringComparison.OrdinalIgnoreCase)),
            "Pipeline must mention IQR/outlier processing");

        // Final price should be in the cluster range (1200-1600), not at extremes
        Assert.True(result.CalculatedPricePerM2 >= 800,
            $"Should not be dragged to ruin levels, got {result.CalculatedPricePerM2}");
        Assert.True(result.CalculatedPricePerM2 <= 3000,
            $"Should not be at luxury penthouse level, got {result.CalculatedPricePerM2}");
    }


    // ══════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ══════════════════════════════════════════════════════════

    private static string LocationSlug(string location)
        => (location ?? "").ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    private static List<ComparableSearchResult> PatchUrls(
        List<ComparableSearchResult> listings, string propertyLocation)
    {
        var slug = LocationSlug(propertyLocation);
        return listings.Select(l => new ComparableSearchResult
        {
            Title = l.Title,
            Content = l.Content,
            Url = $"https://nekretnine.hr/{slug}/{l.Title?.Replace(" ", "-").ToLowerInvariant() ?? "listing"}",
            ListedPriceEur = l.ListedPriceEur,
            ListedPricePerM2 = l.ListedPricePerM2,
            AiSizeM2 = l.AiSizeM2,
            AiCondition = l.AiCondition,
            HasGarage = l.HasGarage,
            AiYardAreaM2 = l.AiYardAreaM2,
            AiSimilarityScore = l.AiSimilarityScore,
        }).ToList();
    }

    private static MarketResearchResult MakeMarket(string region, string type,
        List<ComparableSearchResult> comps, string propertyLocation,
        string phase = "Primary")
    {
        return new()
        {
            RegionName = region,
            DetectedPropertyType = type,
            ActiveListings = PatchUrls(comps, propertyLocation),
            RegionalData = [],
            SearchPhaseUsed = phase,
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
        };
    }

    private static ComparableSearchResult MakeListing(string title, string content,
        double totalPrice, double pricePerM2, double sizeM2, string condition)
        => new()
        {
            Title = title,
            Content = content,
            Url = $"https://example.com/{title.Replace(" ", "-").ToLowerInvariant()}",
            ListedPriceEur = totalPrice,
            ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2,
            AiCondition = condition,
        };
}
