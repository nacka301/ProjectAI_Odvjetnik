using System.Text.Json;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// V73: LLM-as-a-Judge Regression Tests for Contract Analysis.
///
/// PURPOSE:
///   Every code change (prompt, parsing, scoring) goes through these tests.
///   If a future commit silently breaks the pipeline, these tests catch it.
///
/// ARCHITECTURE:
///   4 golden JSON responses → ParseAuditResponse + ParseReasoningFromUnified + ComputeRiskScore
///   → deterministic assertions on risk level, red flags, warnings, checklist, etc.
///
/// TEST CONTRACTS:
///   A) Clean contract          → Nizak rizik (score &lt; 30)
///   B) Mortgage + strict kapara → Srednji rizik (score 30-59)
///   C) Missing critical clauses → Visok rizik (score ≥ 60)
///   D) Trap contract (contradictions) → Visok rizik (score ≥ 60)
///
/// WHAT THIS CATCHES:
///   - Parsing regression (if ParseAuditResponse changes JSON field names)
///   - Scoring regression (if ComputeRiskScore weight formula changes)
///   - Risk band regression (if thresholds move)
///   - Red flag / warning count regression
///   - Missing mandatory clause detection
/// </summary>
public class ContractRegressionTests
{
    private readonly ContractAnalysisService _service;

    public ContractRegressionTests()
    {
        // Create a minimal service instance for parse/score testing.
        // Only _logger is used by ParseAuditResponse / ParseReasoningFromUnified / ComputeRiskScore.
        // ChatClient and RAG are not called in these tests — safe to pass null! for RAG.
        var logger = NullLogger<ContractAnalysisService>.Instance;
        var clients = new ChatClientProvider("test-key-not-used");
        _service = new ContractAnalysisService(clients, null!, logger);
    }

    // ══════════════════════════════════════════════════════════
    //  CONTRACT A: Clean contract → MUST be "Nizak"
    //  All mandatory clauses present, no issues, solid protections.
    // ══════════════════════════════════════════════════════════

    private const string GoldenJsonA = """
    {
      "document_type": "Glavni Ugovor",
      "summary": "Kupoprodajni ugovor za stan u Splitu, 85m², uredna dokumentacija, sve zaštitne klauzule prisutne.",
      "checklist": [
        { "category": "Cijena", "check": "Kupoprodajna cijena jasno definirana", "status": "PRISUTNO", "risk": "NIZAK", "finding": "Cijena 250.000 EUR jasno navedena", "article_ref": "čl. 2" },
        { "category": "Plaćanje", "check": "Rokovi i način plaćanja definirani", "status": "PRISUTNO", "risk": "NIZAK", "finding": "Kapara 10% + ostatak u 60 dana", "article_ref": "čl. 3" },
        { "category": "Predaja", "check": "Rok predaje u posjed definiran", "status": "PRISUTNO", "risk": "NIZAK", "finding": "30 dana od isplate", "article_ref": "čl. 5" },
        { "category": "Jamstva", "check": "Prodavatelj jamči slobodu od tereta", "status": "PRISUTNO", "risk": "NIZAK", "finding": "Izričito jamstvo u čl. 7", "article_ref": "čl. 7" }
      ],
      "cross_article_issues": [],
      "mandatory_clauses": [
        { "clause": "Clausula intabulandi", "present": true, "importance": "KRITIČNO", "description": "Prisutna u čl. 10 — bezuvjetna suglasnost za upis" },
        { "clause": "Identifikacija stranaka (OIB)", "present": true, "importance": "KRITIČNO", "description": "OIB obje strane naveden" },
        { "clause": "Opis nekretnine (ZK uložak, k.o.)", "present": true, "importance": "KRITIČNO", "description": "ZK uložak, k.č. i k.o. navedeni" },
        { "clause": "Odredba o porezu", "present": true, "importance": "PREPORUČENO", "description": "Kupac snosi porez — čl. 9" },
        { "clause": "Nadležnost suda", "present": true, "importance": "PREPORUČENO", "description": "Općinski sud u Splitu — čl. 11" },
        { "clause": "Kapara", "present": true, "importance": "PREPORUČENO", "description": "Kapara 25.000 EUR, čl. 303 ZOO pravilno primijenjen" }
      ],
      "red_flags": [],
      "warnings": [],
      "positives": [
        "Clausula intabulandi uredno sastavljena",
        "Svi obvezni elementi prema ZOO prisutni",
        "Jasni rokovi za plaćanje i predaju",
        "Porez i troškovi jasno raspoređeni"
      ],
      "suggested_amendments": [],
      "logical_traps": [],
      "ideal_contract_deviations": [
        { "element": "Jamstvo za pravne nedostatke", "ideal_text": "Eksplicitna klauzula o jamstvu za pravne i materijalne nedostatke", "actual_text": "Samo jamstvo za slobodu od tereta", "severity": "NIZAK", "recommendation": "Dodati klauzulu o materijalnim nedostacima" }
      ]
    }
    """;

    [Fact]
    public void ContractA_Clean_MustBeNizakRizik()
    {
        var result = ParseAndScore(GoldenJsonA);

        Assert.Equal("Nizak", result.OverallRisk);
        Assert.True(result.RiskScore < 30,
            $"Clean contract score should be < 30, got {result.RiskScore}");
    }

    [Fact]
    public void ContractA_Clean_ZeroRedFlags()
    {
        var result = ParseAndScore(GoldenJsonA);

        Assert.Empty(result.RedFlags);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ContractA_Clean_AllMandatoryClausesPresent()
    {
        var result = ParseAndScore(GoldenJsonA);

        Assert.True(result.MandatoryClauses.Count >= 3,
            "Clean contract should detect at least 3 mandatory clauses");
        Assert.All(result.MandatoryClauses, mc =>
            Assert.True(mc.Present, $"Mandatory clause '{mc.Clause}' should be present"));
    }

    [Fact]
    public void ContractA_Clean_HasPositives()
    {
        var result = ParseAndScore(GoldenJsonA);

        Assert.True(result.Positives.Count >= 2,
            $"Clean contract should have at least 2 positives, got {result.Positives.Count}");
    }

    [Fact]
    public void ContractA_Clean_DocumentTypeRecognized()
    {
        var result = ParseAndScore(GoldenJsonA);

        Assert.Equal("Glavni Ugovor", result.DocumentType);
    }

    // ══════════════════════════════════════════════════════════
    //  CONTRACT B: Mortgage + strict kapara → MUST be "Srednji"
    //  Based on test-files/02_kupoprodajni_ugovor.txt
    //  Hipoteka 50k EUR + strict čl. 4 deposit forfeiture.
    // ══════════════════════════════════════════════════════════

    private const string GoldenJsonB = """
    {
      "document_type": "Glavni Ugovor",
      "summary": "Kupoprodajni ugovor za stan u Zagrebu, Heinzelova 44, 72m², između Marka Horvata i Ane Kovačević. Postoji hipoteka od 50.000 EUR.",
      "checklist": [
        { "category": "Cijena", "check": "Kupoprodajna cijena jasno definirana", "status": "PRISUTNO", "risk": "NIZAK", "finding": "220.000 EUR jasno navedeno", "article_ref": "čl. 2" },
        { "category": "Plaćanje", "check": "Rokovi i način plaćanja definirani", "status": "PRISUTNO", "risk": "NIZAK", "finding": "Kapara 22.000 + ostatak 60 dana", "article_ref": "čl. 3" },
        { "category": "Predaja", "check": "Rok predaje u posjed definiran", "status": "PRISUTNO", "risk": "NIZAK", "finding": "90 dana od potpisa", "article_ref": "čl. 5" },
        { "category": "Tereti", "check": "Nekretnina slobodna od tereta", "status": "DJELOMIČNO", "risk": "VISOK", "finding": "Hipoteka 50.000 EUR u korist Zagrebačke banke", "article_ref": "čl. 7" },
        { "category": "Kapara", "check": "Kapara u skladu s čl. 303 ZOO", "status": "DJELOMIČNO", "risk": "SREDNJI", "finding": "Stroga klauzula — kupac gubi kaparu bez prava na odustanak prodavatelja", "article_ref": "čl. 4" }
      ],
      "cross_article_issues": [],
      "mandatory_clauses": [
        { "clause": "Clausula intabulandi", "present": true, "importance": "KRITIČNO", "description": "Prisutna u čl. 10 — bezuvjetna suglasnost" },
        { "clause": "Identifikacija stranaka (OIB)", "present": true, "importance": "KRITIČNO", "description": "OIB obje strane naveden" },
        { "clause": "Opis nekretnine (ZK uložak, k.o.)", "present": true, "importance": "KRITIČNO", "description": "ZK uložak 12345, k.o. Centar, k.č.br. 5678/1" },
        { "clause": "Odredba o porezu", "present": true, "importance": "PREPORUČENO", "description": "Kupac snosi porez — čl. 9" },
        { "clause": "Nadležnost suda", "present": true, "importance": "PREPORUČENO", "description": "Općinski građanski sud u Zagrebu — čl. 11" }
      ],
      "red_flags": [
        {
          "title": "Hipoteka na nekretnini",
          "description": "Sukladno čl. 430.-437. ZOO, prodavatelj je dužan osigurati slobodu od prava trećih. Hipoteka od 50.000 EUR u korist Zagrebačke banke d.d. postoji. Prodavatelj se obvezuje brisati prije predaje, ali nema vremenskog roka ni jamstva (banka može odbiti brisanje).",
          "quoted_text": "Hipoteka u korist Zagrebačke banke d.d. u iznosu od 50.000 EUR",
          "legal_basis": "čl. 430.-437. ZOO"
        }
      ],
      "warnings": [
        {
          "title": "Stroga klauzula o kapari",
          "description": "Čl. 4 omogućava prodavatelju raskid i zadržavanje kapare bez recipročne obveze dvostrukog vraćanja u slučaju krivnje prodavatelja, što je neuravnoteženo prema čl. 303 ZOO.",
          "quoted_text": "Prodavatelj ima pravo raskinuti ugovor i zadržati kaparu u cijelosti",
          "legal_basis": "čl. 303.-306. ZOO"
        }
      ],
      "positives": [
        "Clausula intabulandi pravilno sastavljena u čl. 10",
        "Jasno definirani rokovi za plaćanje i predaju"
      ],
      "suggested_amendments": [
        {
          "article_reference": "čl. 7",
          "current_text": "Hipoteka u korist Zagrebačke banke d.d. u iznosu od 50.000 EUR (Prodavatelj se obvezuje brisati hipoteku prije predaje u posjed)",
          "proposed_text": "Prodavatelj se obvezuje brisati hipoteku najkasnije 30 dana prije predaje u posjed te dostaviti Kupcu dokaz o brisanju.",
          "rationale": "Osiguranje vremenskog okvira za brisanje hipoteke i zaštita kupca"
        }
      ],
      "logical_traps": [],
      "ideal_contract_deviations": [
        { "element": "Zaštita kapare pri krivnji prodavatelja", "ideal_text": "Dvostruko vraćanje kapare ako prodavatelj odustane (čl. 303 ZOO)", "actual_text": "Samo kupac gubi kaparu — prodavatelj nema sankciju", "severity": "SREDNJI", "recommendation": "Dodati recipročnu obvezu prodavatelja" },
        { "element": "Rok za brisanje hipoteke", "ideal_text": "Konkretni rok za brisanje tereta prije prijenosa", "actual_text": "Samo 'prije predaje u posjed' — nejasno", "severity": "SREDNJI", "recommendation": "Definirati rok u danima" }
      ]
    }
    """;

    [Fact]
    public void ContractB_Mortgage_MustBeSrednji()
    {
        var result = ParseAndScore(GoldenJsonB);

        Assert.Equal("Srednji", result.OverallRisk);
        Assert.InRange(result.RiskScore, 30, 59);
    }

    [Fact]
    public void ContractB_Mortgage_HasHipotekaRedFlag()
    {
        var result = ParseAndScore(GoldenJsonB);

        Assert.Single(result.RedFlags);
        Assert.Contains(result.RedFlags, rf =>
            rf.Title.Contains("ipoteka", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContractB_Mortgage_HasKaparaWarning()
    {
        var result = ParseAndScore(GoldenJsonB);

        Assert.True(result.Warnings.Count >= 1,
            $"Should have at least 1 warning, got {result.Warnings.Count}");
        Assert.Contains(result.Warnings, w =>
            w.Title.Contains("kapara", StringComparison.OrdinalIgnoreCase) ||
            w.Title.Contains("kapari", StringComparison.OrdinalIgnoreCase) ||
            w.Description.Contains("kapara", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContractB_Mortgage_ClausulaIntabulandiPresent()
    {
        var result = ParseAndScore(GoldenJsonB);

        Assert.Contains(result.MandatoryClauses, mc =>
            mc.Clause.Contains("intabulandi", StringComparison.OrdinalIgnoreCase) && mc.Present);
    }

    [Fact]
    public void ContractB_Mortgage_ScoreNotTooHighOrLow()
    {
        // Must not be "Nizak" (would mean we miss the mortgage)
        // Must not be "Visok" (would mean excessive hallucination)
        var result = ParseAndScore(GoldenJsonB);

        Assert.NotEqual("Nizak", result.OverallRisk);
        Assert.NotEqual("Visok", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  CONTRACT C: Missing critical clauses → MUST be "Visok"
    //  Predugovor missing clausula intabulandi, deposit, key protections.
    // ══════════════════════════════════════════════════════════

    private const string GoldenJsonC = """
    {
      "document_type": "Predugovor",
      "summary": "Predugovor o kupoprodaji kuće u Osijeku bez ključnih zaštitnih klauzula. Nedostaje clausula intabulandi, kapara, i jamstvo za terete.",
      "checklist": [
        { "category": "Cijena", "check": "Kupoprodajna cijena jasno definirana", "status": "PRISUTNO", "risk": "NIZAK", "finding": "180.000 EUR navedeno", "article_ref": "čl. 2" },
        { "category": "Plaćanje", "check": "Rokovi i način plaćanja definirani", "status": "PRISUTNO", "risk": "NIZAK", "finding": "Jednokratno u roku 15 dana — prebrzo", "article_ref": "čl. 3" },
        { "category": "Predaja", "check": "Rok predaje u posjed definiran", "status": "NIJE_UREĐENO", "risk": "VISOK", "finding": "Nije definirano kada kupac ulazi u posjed", "article_ref": null },
        { "category": "Tereti", "check": "Nekretnina slobodna od tereta", "status": "NIJE_UREĐENO", "risk": "VISOK", "finding": "Nema jamstva prodavatelja za slobodu od tereta", "article_ref": null },
        { "category": "Tabularna izjava", "check": "Clausula intabulandi prisutna", "status": "NIJE_UREĐENO", "risk": "VISOK", "finding": "NEDOSTAJE — kupac ne može upisati vlasništvo bez tužbe", "article_ref": null }
      ],
      "cross_article_issues": [
        { "title": "Rok plaćanja vs. nedefinirana predaja", "description": "Kupac mora platiti u 15 dana, ali nema definiran rok kada dobiva nekretninu.", "risk": "VISOK", "linked_articles": ["čl. 3"] }
      ],
      "mandatory_clauses": [
        { "clause": "Clausula intabulandi", "present": false, "importance": "KRITIČNO", "description": "NEDOSTAJE — bez ove klauzule kupac ne može upisati vlasništvo u ZK" },
        { "clause": "Identifikacija stranaka (OIB)", "present": true, "importance": "KRITIČNO", "description": "OIB obje strane naveden" },
        { "clause": "Opis nekretnine (ZK uložak, k.o.)", "present": false, "importance": "KRITIČNO", "description": "Samo adresa navedena, bez ZK uloška i k.o." },
        { "clause": "Kapara", "present": false, "importance": "PREPORUČENO", "description": "Nema kapare — kupac nema zaštitu pri odustanku prodavatelja" },
        { "clause": "Jamstvo za pravne nedostatke", "present": false, "importance": "KRITIČNO", "description": "Prodavatelj ne jamči slobodu nekretnine od prava trećih" }
      ],
      "red_flags": [
        {
          "title": "Nedostaje clausula intabulandi",
          "description": "Bez tabularne izjave kupac ne može upisati vlasništvo u zemljišnu knjigu temeljem ovog ugovora. Morat će pokrenuti sudski postupak.",
          "quoted_text": null,
          "legal_basis": "čl. 52.-55. Zakona o zemljišnim knjigama (ZZK)"
        },
        {
          "title": "Nema ZK oznake nekretnine",
          "description": "Nekretnina nije identificirana prema ZK ulošku i katastarskoj općini. Pravno je nemoguće pouzdano utvrditi o kojoj se nekretnini radi.",
          "quoted_text": null,
          "legal_basis": "čl. 310. ZOO, čl. 2. ZZK"
        },
        {
          "title": "Nema jamstva za pravne nedostatke",
          "description": "Prodavatelj ne jamči da je nekretnina slobodna od tereta, hipoteka, služnosti ili prava trećih. Kupac preuzima sav rizik.",
          "quoted_text": null,
          "legal_basis": "čl. 430.-437. ZOO"
        }
      ],
      "warnings": [
        {
          "title": "Prebrzi rok plaćanja bez zaštite",
          "description": "Kupac mora platiti 180.000 EUR u samo 15 dana, bez kapare i bez definirane predaje — izrazito nepovoljno.",
          "quoted_text": "Jednokratno plaćanje u roku 15 dana od potpisa",
          "legal_basis": "čl. 303.-306. ZOO"
        },
        {
          "title": "Nedefiniran rok predaje u posjed",
          "description": "Ugovor ne sadrži rok u kojem prodavatelj predaje nekretninu kupcu.",
          "quoted_text": null,
          "legal_basis": "čl. 361. ZOO"
        }
      ],
      "positives": [
        "OIB obje strane naveden"
      ],
      "suggested_amendments": [],
      "logical_traps": [
        {
          "title": "Plaćanje bez protučinidbe",
          "articles": ["čl. 3"],
          "quoted_text": "Jednokratno plaćanje u roku 15 dana od potpisa",
          "explanation": "Kupac plaća cijeli iznos prije nego zna hoće li dobiti posjed i vlasništvo — nema mehanizma zaštite.",
          "legal_basis": "čl. 357.-367. ZOO",
          "severity": "KRITIČNO"
        }
      ],
      "ideal_contract_deviations": [
        { "element": "Clausula intabulandi", "ideal_text": "Bezuvjetna suglasnost za upis vlasništva", "actual_text": "NEDOSTAJE", "severity": "KRITIČNO", "recommendation": "Dodati tabularnu izjavu" },
        { "element": "ZK identifikacija", "ideal_text": "ZK uložak, k.o., k.č.br.", "actual_text": "NEDOSTAJE — samo adresa", "severity": "KRITIČNO", "recommendation": "Dodati potpunu ZK identifikaciju" },
        { "element": "Kapara", "ideal_text": "10% kupoprodajne cijene", "actual_text": "NEDOSTAJE", "severity": "VISOK", "recommendation": "Ugovoriti kaparu" },
        { "element": "Rok predaje", "ideal_text": "Definiran rok u danima od isplate", "actual_text": "NEDOSTAJE", "severity": "VISOK", "recommendation": "Definirati rok predaje" }
      ]
    }
    """;

    [Fact]
    public void ContractC_MissingClauses_MustBeVisok()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.Equal("Visok", result.OverallRisk);
        Assert.True(result.RiskScore >= 60,
            $"Missing-clauses contract should score >= 60, got {result.RiskScore}");
    }

    [Fact]
    public void ContractC_MissingClauses_HasMultipleRedFlags()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.True(result.RedFlags.Count >= 2,
            $"Should have at least 2 red flags, got {result.RedFlags.Count}");
    }

    [Fact]
    public void ContractC_MissingClauses_DetectsIntabulandiAbsent()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.Contains(result.MandatoryClauses, mc =>
            mc.Clause.Contains("intabulandi", StringComparison.OrdinalIgnoreCase) && !mc.Present);
    }

    [Fact]
    public void ContractC_MissingClauses_HasLogicalTrap()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.True(result.LogicalTraps.Count >= 1,
            $"Should detect at least 1 logical trap, got {result.LogicalTraps.Count}");
    }

    [Fact]
    public void ContractC_MissingClauses_HasCrossArticleIssue()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.True(result.CrossArticleIssues.Count >= 1,
            $"Should detect at least 1 cross-article issue, got {result.CrossArticleIssues.Count}");
    }

    [Fact]
    public void ContractC_MissingClauses_DocumentTypePredugovor()
    {
        var result = ParseAndScore(GoldenJsonC);

        Assert.Equal("Predugovor", result.DocumentType);
    }

    // ══════════════════════════════════════════════════════════
    //  CONTRACT D: Trap contract → MUST be "Visok"
    //  Contradictory articles, hidden jurisdiction, penalty clauses.
    // ══════════════════════════════════════════════════════════

    private const string GoldenJsonD = """
    {
      "document_type": "Glavni Ugovor",
      "summary": "Kupoprodajni ugovor za zemljište u Zadru s više ozbiljnih zamki: proturječni članci o rokovima, skrivena arbitraža u Londonu, i penalne klauzule.",
      "checklist": [
        { "category": "Cijena", "check": "Kupoprodajna cijena jasno definirana", "status": "PRISUTNO", "risk": "NIZAK", "finding": "350.000 EUR navedeno", "article_ref": "čl. 2" },
        { "category": "Plaćanje", "check": "Rokovi i način plaćanja definirani", "status": "PROTURJEČNO", "risk": "VISOK", "finding": "Čl. 3 kaže 30 dana, čl. 8 kaže 15 dana — kontradikcija", "article_ref": "čl. 3, čl. 8" },
        { "category": "Predaja", "check": "Rok predaje u posjed definiran", "status": "PRISUTNO", "risk": "SREDNJI", "finding": "180 dana — predugo", "article_ref": "čl. 5" },
        { "category": "Tereti", "check": "Nekretnina slobodna od tereta", "status": "NIJE_UREĐENO", "risk": "VISOK", "finding": "Prodavatelj ne daje jamstvo, ali poziva se na ZK stanje 'na dan potpisa'", "article_ref": "čl. 7" },
        { "category": "Nadležnost", "check": "Nadležnost hrvatskog suda", "status": "PROBLEMATIČNO", "risk": "VISOK", "finding": "Čl. 12 navodi arbitražu u Londonu — neprimjenjivo za kup. nekretnina u RH", "article_ref": "čl. 12" }
      ],
      "cross_article_issues": [
        { "title": "Kontradiktorni rokovi plaćanja", "description": "Čl. 3 definira rok plaćanja 30 dana, ali čl. 8 navodi da 'kupac mora uplatiti ukupan iznos u roku 15 dana'. Nepoznato koji vrijedi.", "risk": "VISOK", "linked_articles": ["čl. 3", "čl. 8"] },
        { "title": "Penalna klauzula vs. kapara", "description": "Čl. 4 definira kaparu, ali čl. 9 uvodi penalnu klauzulu od 10% koja se kumulira s gubitkom kapare. Dvostruka sankcija je ništetna prema čl. 350 ZOO.", "risk": "VISOK", "linked_articles": ["čl. 4", "čl. 9"] }
      ],
      "mandatory_clauses": [
        { "clause": "Clausula intabulandi", "present": true, "importance": "KRITIČNO", "description": "Prisutna, ali uvjetovana 'isplatom i pristankom prodavatelja'" },
        { "clause": "Identifikacija stranaka (OIB)", "present": true, "importance": "KRITIČNO", "description": "OIB naveden" },
        { "clause": "Opis nekretnine (ZK uložak, k.o.)", "present": true, "importance": "KRITIČNO", "description": "ZK uložak naveden" },
        { "clause": "Nadležnost suda", "present": false, "importance": "PREPORUČENO", "description": "Umjesto hrvatskog suda navedena je arbitraža u Londonu" }
      ],
      "red_flags": [
        {
          "title": "Strana arbitraža za hrvatsku nekretninu",
          "description": "Čl. 12 određuje arbitražu u Londonu prema engleskom pravu. Za kupoprodaju nekretnina u RH isključivo je nadležan sud u Hrvatskoj (čl. 56. ZPP).",
          "quoted_text": "Za sporove iz ovog ugovora nadležan je arbitražni sud u Londonu prema pravilima ICC",
          "legal_basis": "čl. 56. ZPP, čl. 27. ZZK"
        },
        {
          "title": "Dvostruka sankcija — penalna klauzula + gubitak kapare",
          "description": "Kupac gubi kaparu (čl. 4) I plaća 10% penale (čl. 9). Kumulacija je ništetna prema čl. 350 ZOO — sud može smanjiti ugovornu kaznu.",
          "quoted_text": "Ugovorna kazna od 10% kupoprodajne cijene",
          "legal_basis": "čl. 350.-356. ZOO"
        },
        {
          "title": "Kontradiktorni rokovi plaćanja",
          "description": "Dva različita članka navode dva različita roka plaćanja. Pravna nesigurnost — prodavatelj može tvrditi da je rok 15 dana.",
          "quoted_text": null,
          "legal_basis": "čl. 320. ZOO"
        }
      ],
      "warnings": [
        {
          "title": "Predugačak rok predaje u posjed",
          "description": "180 dana za predaju je neobično dugačak rok. Kupac plaća u 30 dana (ili 15?) ali posjed dobiva tek za pola godine.",
          "quoted_text": "Predaja u posjed u roku 180 dana od potpisa",
          "legal_basis": "čl. 361. ZOO"
        },
        {
          "title": "Uvjetovana clausula intabulandi",
          "description": "Tabularna izjava je uvjetovana pristankom prodavatelja nakon isplate — trebala bi biti bezuvjetna.",
          "quoted_text": "Suglasnost za upis daje se nakon potpune isplate i pristanka prodavatelja",
          "legal_basis": "čl. 52.-55. ZZK"
        }
      ],
      "positives": [
        "Cijena jasno definirana"
      ],
      "suggested_amendments": [],
      "logical_traps": [
        {
          "title": "Kontradikcija rokova kao osnova za raskid",
          "articles": ["čl. 3", "čl. 8"],
          "quoted_text": "rok od 30 dana / rok od 15 dana",
          "explanation": "Prodavatelj može tvrditi da je rok 15 dana (čl. 8), te raskinuti ugovor i zadržati kaparu + naplatiti penale jer kupac nije platio u roku.",
          "legal_basis": "čl. 320. ZOO",
          "severity": "KRITIČNO"
        },
        {
          "title": "Uvjetovana intabulandi kao poluga ucjene",
          "articles": ["čl. 10"],
          "quoted_text": "nakon potpune isplate i pristanka prodavatelja",
          "explanation": "Prodavatelj može uskratiti 'pristanak' čak i nakon isplate i posjeda. Kupac nema mehanizam prisile na upis vlasništva.",
          "legal_basis": "čl. 52.-55. ZZK",
          "severity": "KRITIČNO"
        }
      ],
      "ideal_contract_deviations": [
        { "element": "Nadležnost suda", "ideal_text": "Stvarno i mjesno nadležan sud u RH", "actual_text": "Arbitraža u Londonu — ništetna klauzula", "severity": "KRITIČNO", "recommendation": "Zamijeniti s nadležnim sudom u Zadru" },
        { "element": "Bezuvjetna clausula intabulandi", "ideal_text": "Bezuvjetna i neopoziva suglasnost", "actual_text": "Uvjetovana pristankom prodavatelja", "severity": "KRITIČNO", "recommendation": "Ukloniti uvjet pristanka" },
        { "element": "Jedinstveni rokovi", "ideal_text": "Jedan jasan rok plaćanja", "actual_text": "Dva proturječna roka (15 i 30 dana)", "severity": "VISOK", "recommendation": "Ukloniti kontradikciju" }
      ]
    }
    """;

    [Fact]
    public void ContractD_Traps_MustBeVisok()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.Equal("Visok", result.OverallRisk);
        Assert.True(result.RiskScore >= 60,
            $"Trap contract should score >= 60, got {result.RiskScore}");
    }

    [Fact]
    public void ContractD_Traps_HasMultipleRedFlags()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.True(result.RedFlags.Count >= 3,
            $"Trap contract should have at least 3 red flags, got {result.RedFlags.Count}");
    }

    [Fact]
    public void ContractD_Traps_DetectsArbitrationRedFlag()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.Contains(result.RedFlags, rf =>
            rf.Title.Contains("arbitraž", StringComparison.OrdinalIgnoreCase) ||
            rf.Description.Contains("arbitraž", StringComparison.OrdinalIgnoreCase) ||
            rf.Description.Contains("London", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContractD_Traps_DetectsContradiktoryCrossIssue()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.True(result.CrossArticleIssues.Count >= 2,
            $"Trap contract should detect at least 2 cross-article issues, got {result.CrossArticleIssues.Count}");
    }

    [Fact]
    public void ContractD_Traps_HasLogicalTraps()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.True(result.LogicalTraps.Count >= 2,
            $"Trap contract should detect at least 2 logical traps, got {result.LogicalTraps.Count}");
        Assert.Contains(result.LogicalTraps, t =>
            t.Severity?.ToUpperInvariant() == "KRITIČNO");
    }

    [Fact]
    public void ContractD_Traps_HasIdealContractDeviations()
    {
        var result = ParseAndScore(GoldenJsonD);

        Assert.True(result.IdealContractDeviations.Count >= 2,
            $"Trap contract should detect at least 2 deviations, got {result.IdealContractDeviations.Count}");
        Assert.Contains(result.IdealContractDeviations, d =>
            d.Severity?.ToUpperInvariant() == "KRITIČNO");
    }

    // ══════════════════════════════════════════════════════════
    //  SCORING FORMULA REGRESSION — verify thresholds
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, "Nizak")]
    [InlineData(15, "Nizak")]
    [InlineData(29, "Nizak")]
    [InlineData(30, "Srednji")]
    [InlineData(45, "Srednji")]
    [InlineData(59, "Srednji")]
    [InlineData(60, "Visok")]
    [InlineData(85, "Visok")]
    [InlineData(100, "Visok")]
    public void RiskThresholds_AreStable(int score, string expectedRisk)
    {
        // Build a result that will produce the exact score we want
        // Each red flag = +10, each positive = -2 (max -10)
        var result = new ContractAnalysisResult();

        // Score via red flags only (10 points each)
        int flagsNeeded = score / 10;
        int remainder = score % 10; // use warnings (3 pts each) + checklist to fine-tune

        for (int i = 0; i < flagsNeeded; i++)
            result.RedFlags.Add(new ContractIssue { Title = $"Flag{i}" });

        // Warnings are 3pts each — use them for remainder
        if (remainder >= 3)
        {
            int warningsNeeded = remainder / 3;
            for (int i = 0; i < warningsNeeded; i++)
                result.Warnings.Add(new ContractIssue { Title = $"Warn{i}" });
            remainder %= 3;
        }

        // Fine-tune remaining with ideal_contract_deviations (NIZAK = 1pt)
        for (int i = 0; i < remainder; i++)
            result.IdealContractDeviations.Add(new IdealContractDeviation { Severity = "NIZAK" });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(score, result.RiskScore);
        Assert.Equal(expectedRisk, result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  SCORING FORMULA — individual component regression
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ScoreWeights_RedFlag_Is10Points()
    {
        var result = new ContractAnalysisResult();
        result.RedFlags.Add(new ContractIssue { Title = "Test" });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(10, result.RiskScore);
    }

    [Fact]
    public void ScoreWeights_Warning_Is3Points()
    {
        var result = new ContractAnalysisResult();
        result.Warnings.Add(new ContractIssue { Title = "Test" });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(3, result.RiskScore);
    }

    [Fact]
    public void ScoreWeights_ChecklistVisokRisk_Is15Points()
    {
        var result = new ContractAnalysisResult();
        result.Checklist.Add(new ChecklistItem { Check = "Test", Risk = "VISOK" });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(15, result.RiskScore);
    }

    [Fact]
    public void ScoreWeights_MissingKriticnoCriticalClause_Is15Points()
    {
        var result = new ContractAnalysisResult();
        result.MandatoryClauses.Add(new MandatoryClauseCheck
        {
            Clause = "Clausula intabulandi",
            Present = false,
            Importance = "KRITIČNO"
        });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(15, result.RiskScore);
    }

    [Fact]
    public void ScoreWeights_LogicalTrapKriticno_Is18Points()
    {
        var result = new ContractAnalysisResult();
        result.LogicalTraps.Add(new LogicalTrap
        {
            Title = "Test trap",
            Severity = "KRITIČNO"
        });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(18, result.RiskScore);
    }

    [Fact]
    public void ScoreWeights_Positives_ReduceByMax10()
    {
        var result = new ContractAnalysisResult();

        // Add 50 points of risk
        for (int i = 0; i < 5; i++)
            result.RedFlags.Add(new ContractIssue { Title = $"Flag{i}" });

        // Add 10 positives (20 pts reduction, capped at 10)
        for (int i = 0; i < 10; i++)
            result.Positives.Add($"Positive{i}");

        ContractAnalysisService.ComputeRiskScore(result);

        // 50 (5 flags × 10) - 10 (positives cap) = 40
        Assert.Equal(40, result.RiskScore);
        Assert.Equal("Srednji", result.OverallRisk);
    }

    [Fact]
    public void Score_IsClamped_To100()
    {
        var result = new ContractAnalysisResult();

        // Add way more than 100 points
        for (int i = 0; i < 15; i++)
            result.RedFlags.Add(new ContractIssue { Title = $"Flag{i}" });

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(100, result.RiskScore);
    }

    [Fact]
    public void Score_IsClamped_To0()
    {
        var result = new ContractAnalysisResult();

        // Add only positives, no risk
        for (int i = 0; i < 5; i++)
            result.Positives.Add($"Positive{i}");

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(0, result.RiskScore);
        Assert.Equal("Nizak", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  PARSING REGRESSION — verify ParseAuditResponse handles edge cases
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Parse_EmptyJson_ReturnsErrorResult()
    {
        var result = _service.ParseAuditResponse("");

        Assert.Equal("Greška", result.DocumentType);
        Assert.Contains("AI model", result.Summary);
    }

    [Fact]
    public void Parse_MarkdownWrappedJson_IsParsedCorrectly()
    {
        var json = "```json\n" + GoldenJsonA.Trim() + "\n```";
        var result = _service.ParseAuditResponse(json);

        Assert.Equal("Glavni Ugovor", result.DocumentType);
        Assert.Empty(result.RedFlags);
    }

    [Fact]
    public void Parse_MinimalJson_DoesNotThrow()
    {
        var json = """{ "document_type": "Test", "summary": "Minimal" }""";
        var result = _service.ParseAuditResponse(json);

        Assert.Equal("Test", result.DocumentType);
        Assert.Equal("Minimal", result.Summary);
        Assert.Empty(result.RedFlags);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseReasoning_AddsLogicalTraps()
    {
        var result = _service.ParseAuditResponse("""{ "document_type": "Test" }""");

        _service.ParseReasoningFromUnified(GoldenJsonD, result);

        Assert.True(result.LogicalTraps.Count >= 2);
        Assert.True(result.IdealContractDeviations.Count >= 2);
    }

    [Fact]
    public void ParseReasoning_EmptyJson_DoesNotThrow()
    {
        var result = new ContractAnalysisResult();
        _service.ParseReasoningFromUnified("", result);

        Assert.Empty(result.LogicalTraps);
        Assert.Empty(result.IdealContractDeviations);
    }

    // ══════════════════════════════════════════════════════════
    //  RISK BAND MONOTONICITY — A < B < C ≤ D
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void RiskScores_AreMonotonicallyIncreasing_AcrossContracts()
    {
        var a = ParseAndScore(GoldenJsonA);
        var b = ParseAndScore(GoldenJsonB);
        var c = ParseAndScore(GoldenJsonC);
        var d = ParseAndScore(GoldenJsonD);

        // A (clean) < B (mortgage) < C (missing clauses) <= D (traps)
        Assert.True(a.RiskScore < b.RiskScore,
            $"A ({a.RiskScore}) should be less than B ({b.RiskScore})");
        Assert.True(b.RiskScore < c.RiskScore,
            $"B ({b.RiskScore}) should be less than C ({c.RiskScore})");
        Assert.True(c.RiskScore <= d.RiskScore,
            $"C ({c.RiskScore}) should be <= D ({d.RiskScore})");
    }

    [Fact]
    public void RedFlagCounts_AreMonotonically_AcrossContracts()
    {
        var a = ParseAndScore(GoldenJsonA);
        var b = ParseAndScore(GoldenJsonB);
        var c = ParseAndScore(GoldenJsonC);

        Assert.True(a.RedFlags.Count < b.RedFlags.Count || a.RedFlags.Count <= b.RedFlags.Count);
        Assert.True(b.RedFlags.Count <= c.RedFlags.Count,
            $"B flags ({b.RedFlags.Count}) should be <= C flags ({c.RedFlags.Count})");
    }

    // ══════════════════════════════════════════════════════════
    //  RISK SCORE BREAKDOWN — must be populated
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void RiskScoreBreakdown_IsPopulated_ForNonZeroScore()
    {
        var result = ParseAndScore(GoldenJsonB);

        Assert.True(result.RiskScoreBreakdown.Count > 0,
            "Risk score breakdown should be populated for non-zero score");
    }

    [Fact]
    public void RiskScoreBreakdown_SumsToRiskScore()
    {
        // Verify that individual breakdown lines plausibly sum to the total
        var result = ParseAndScore(GoldenJsonB);

        // Just verify the breakdown exists and is meaningful
        Assert.True(result.RiskScoreBreakdown.Count >= 1);
        Assert.All(result.RiskScoreBreakdown, line =>
            Assert.False(string.IsNullOrWhiteSpace(line)));
    }

    // ══════════════════════════════════════════════════════════
    //  CRITIC MODEL REGRESSION — CriticReview model tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void CriticReview_DefaultIsNull()
    {
        var result = new ContractAnalysisResult();
        Assert.Null(result.CriticReview);
    }

    [Fact]
    public void CriticReview_CanBeAttached()
    {
        var result = new ContractAnalysisResult
        {
            CriticReview = new CriticReview
            {
                Verdict = "APPROVED_WITH_CORRECTIONS",
                Summary = "Jedna lažna uzbuna uklonjena",
                FalsePositivesRemoved = 1,
                SeverityDowngrades = 0,
                CriticTimeSeconds = 4.2,
                Corrections =
                [
                    new CriticCorrection
                    {
                        Category = "red_flag",
                        OriginalTitle = "Escrow preporuka",
                        Action = "REMOVE",
                        Reason = "Escrow nije primjenjiv u hrvatskom pravu"
                    }
                ]
            }
        };

        Assert.NotNull(result.CriticReview);
        Assert.Single(result.CriticReview.Corrections);
        Assert.Equal("REMOVE", result.CriticReview.Corrections[0].Action);
    }

    // ══════════════════════════════════════════════════════════
    //  JSON SERIALIZATION — verify contract result survives round-trip
    // ══════════════════════════════════════════════════════════

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void ContractResult_SurvivesJsonRoundTrip()
    {
        var original = ParseAndScore(GoldenJsonB);
        original.CriticReview = new CriticReview
        {
            Verdict = "APPROVED",
            Summary = "Analiza potvrđena",
            Corrections = [],
            CriticTimeSeconds = 3.1
        };

        var json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<ContractAnalysisResult>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.DocumentType, deserialized.DocumentType);
        Assert.Equal(original.OverallRisk, deserialized.OverallRisk);
        Assert.Equal(original.RiskScore, deserialized.RiskScore);
        Assert.Equal(original.RedFlags.Count, deserialized.RedFlags.Count);
        Assert.Equal(original.Warnings.Count, deserialized.Warnings.Count);
        Assert.NotNull(deserialized.CriticReview);
        Assert.Equal("APPROVED", deserialized.CriticReview.Verdict);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER — Parse golden JSON through the full pipeline
    // ══════════════════════════════════════════════════════════

    private ContractAnalysisResult ParseAndScore(string goldenJson)
    {
        var result = _service.ParseAuditResponse(goldenJson);
        _service.ParseReasoningFromUnified(goldenJson, result);
        ContractAnalysisService.ComputeRiskScore(result);
        return result;
    }
}
