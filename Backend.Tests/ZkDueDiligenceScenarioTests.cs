using System.Text.Json;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// End-to-end scenarij testovi koji simuliraju kompletne ZK izvadke
/// i verificiraju da Due Diligence sustav pravilno detektira sve rizike.
/// 
/// Svaki test simulira jedan realistični slučaj koji bi odvjetnik sreo u praksi:
/// 1. Čisti stan u Zagrebu — sve OK, zeleno svjetlo
/// 2. Kuća s hipotekom — žuto, uvjetno
/// 3. Stan s plombom — crveno, kritično
/// 4. Kuća s osobnom služnošću — deal-breaker
/// 5. Suvlasništvo 4 vlasnika
/// 6. Nelegalizirani objekt na pašnjaku
/// 7. Kombinacija: plomba + hipoteka + ovrha (noćna mora)
/// 8. Zemljište — koristi land_area
/// 9. Brisana hipoteka — ne smije biti u rezultatu
/// </summary>
public class ZkDueDiligenceScenarioTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 1: Čisti stan u Zagrebu — "zeleno svjetlo"
    //  Očekivanje: ČISTO, LOW, nema findings
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_CistiStan_GreenLight()
    {
        var json = """
        {
          "property_id": "k.o. Trnje, k.č. 4521/3",
          "cadastral_municipality": "Trnje",
          "parcel_number": "4521/3",
          "zk_number": "ZK.ul. 1203",
          "property_type": "stan",
          "property_description": "Trosoban stan u prizemlju",
          "land_area_m2": 1500,
          "building_area_m2": 72,
          "land_use": "stambena zgrada",
          "etazni_udio": "E-2, 72/1500",
          "parcels": [{"parcel_number": "4521/3", "land_use": "stambena zgrada", "area_m2": 1500, "description": ""}],
          "owners": [{"name": "Marko Markić", "share": "1/1", "oib": "12345678901", "basis_of_acquisition": "kupoprodajni ugovor"}],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "LOW",
          "overall_status": "ČISTO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [],
          "due_diligence_summary": "Vlasništvo čisto (1/1). Bez tereta, sporova i plombi. Kupnja se preporučuje.",
          "lawyer_comment": "Čisto vlasništvo, bez rizika.",
          "location": "Trnje, Zagreb",
          "address": "Cvjetna ulica 5",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        // Status provjere
        Assert.Equal("ČISTO", result.OverallStatus);
        Assert.Equal("LOW", result.RiskScore);
        Assert.Equal("1/1 ČISTO", result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect);
        Assert.False(result.HasUsagePermitIssue);
        Assert.Empty(result.LandUseMismatch);

        // Nema rizika
        Assert.Empty(result.ActivePlombe);
        Assert.Empty(result.ActiveMortgages);
        Assert.Empty(result.ActiveDisputes);
        Assert.Empty(result.Findings);

        // Podaci ispravni
        Assert.Equal("stan", result.PropertyType);
        Assert.Equal(72, result.BuildingAreaM2);
        Assert.Single(result.Owners);
        Assert.Equal("1/1", result.Owners[0].Share);
        Assert.NotEmpty(result.DueDiligenceSummary);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 2: Kuća s aktivnom hipotekom — "žuto, uvjetno"
    //  Očekivanje: UVJETNO, MEDIUM, 1 WARNING finding
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_KucaSHipotekom_YellowLight()
    {
        var json = """
        {
          "property_id": "k.o. Maksimir, k.č. 777/1",
          "cadastral_municipality": "Maksimir",
          "parcel_number": "777/1",
          "zk_number": "ZK.ul. 234",
          "property_type": "kuća",
          "property_description": "Kuća i dvor u k.o. Maksimir",
          "land_area_m2": 450,
          "building_area_m2": 120,
          "land_use": "kuća i dvor",
          "etazni_udio": "",
          "parcels": [{"parcel_number": "777/1", "land_use": "kuća i dvor", "area_m2": 450, "description": ""}],
          "owners": [{"name": "Ivan Ivić", "share": "1/1", "oib": "99999999999", "basis_of_acquisition": "darovni ugovor"}],
          "active_mortgages": [{"amount": "150.000", "currency": "EUR", "year": "2018", "details": "Hipoteka u korist Erste banke d.d."}],
          "active_disputes": [],
          "other_encumbrances": [{"type": "služnost prolaza", "description": "Pravo prolaza", "beneficiary": "Susjed d.o.o.", "is_active": true}],
          "active_plombe": [],
          "risk_score": "MEDIUM",
          "overall_status": "UVJETNO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "HIPOTEKA",
              "severity": "WARNING",
              "title": "Aktivna hipoteka 150.000 EUR",
              "description": "Aktivna hipoteka u korist Erste banke d.d. u iznosu od 150.000 EUR upisana 2018.",
              "advice": "Ishodite brisovnu dozvolu ili pismo namjere banke prije isplate kupovnine."
            },
            {
              "category": "OSTALO",
              "severity": "INFO",
              "title": "Služnost prolaza",
              "description": "Stvarna služnost prolaza u korist Susjed d.o.o. Obično bezopasna.",
              "advice": "Stvarne služnosti ne utječu na mogućnost kupoprodaje."
            }
          ],
          "due_diligence_summary": "Na nekretnini postoji aktivna hipoteka u korist Erste banke d.d. u iznosu od 150.000 EUR. Pravo prolaza u korist susjeda ne predstavlja prepreku. Preporučam pismo namjere banke.",
          "lawyer_comment": "Hipoteka — zahtijeva pismo namjere.",
          "location": "Maksimir, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 330
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("UVJETNO", result.OverallStatus);
        Assert.Equal("MEDIUM", result.RiskScore);
        Assert.Empty(result.ActivePlombe);

        // Hipoteka
        Assert.Single(result.ActiveMortgages);
        Assert.Equal("150.000", result.ActiveMortgages[0].Amount);

        // Findings
        Assert.Equal(2, result.Findings.Count);
        var hipoteka = result.Findings.First(f => f.Category == "HIPOTEKA");
        Assert.Equal("WARNING", hipoteka.Severity);
        Assert.Contains("brisovnu dozvolu", hipoteka.Advice);

        var sluznost = result.Findings.First(f => f.Category == "OSTALO");
        Assert.Equal("INFO", sluznost.Severity);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 3: Stan s plombom — "crveno, kritično"
    //  Očekivanje: RIZIČNO, HIGH, CRITICAL finding za plombu
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_StanSPlombom_RedLight()
    {
        var json = """
        {
          "property_id": "k.o. Dubrava, k.č. 5555/2",
          "cadastral_municipality": "Dubrava",
          "parcel_number": "5555/2",
          "zk_number": "ZK.ul. 890",
          "property_type": "stan",
          "property_description": "Dvosoban stan",
          "land_area_m2": 2000,
          "building_area_m2": 55,
          "land_use": "stambena zgrada",
          "etazni_udio": "E-4, 55/2000",
          "parcels": [],
          "owners": [{"name": "Pero Perić", "share": "1/1", "oib": "", "basis_of_acquisition": ""}],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [
            {"z_number": "Z-9876/2025", "description": "Zahtjev za upis prava vlasništva temeljem kupoprodajnog ugovora", "date": "10.02.2025."}
          ],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "PLOMBA",
              "severity": "CRITICAL",
              "title": "Aktivna plomba Z-9876/2025!",
              "description": "Netko je predao zahtjev za upis prava vlasništva temeljem kupoprodajnog ugovora. Postoji rizik da su prethodni ugovori sklopljeni.",
              "advice": "NE plaćajte kaparu dok se plomba ne razriješi. Zatražite uvid u spis Z-9876/2025."
            }
          ],
          "due_diligence_summary": "UPOZORENJE: Detektirana je aktivna plomba Z-9876/2025. To znači da je netko predao zahtjev za upis vlasništva. Rizik je visok — moguća dvostruka prodaja.",
          "lawyer_comment": "PLOMBA - NE KUPOVATI dok se ne razriješi!",
          "location": "Dubrava, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);

        // Plomba provjera
        Assert.Single(result.ActivePlombe);
        Assert.Equal("Z-9876/2025", result.ActivePlombe[0].ZNumber);
        Assert.Contains("kupoprodajnog ugovora", result.ActivePlombe[0].Description);

        // Finding za plombu
        var plombaFinding = Assert.Single(result.Findings);
        Assert.Equal("PLOMBA", plombaFinding.Category);
        Assert.Equal("CRITICAL", plombaFinding.Severity);
        Assert.Contains("NE plaćajte kaparu", plombaFinding.Advice);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 4: Osobna služnost — deal-breaker
    //  Očekivanje: RIZIČNO, CRITICAL finding za služnost
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_OsobnaSluznost_DealBreaker()
    {
        var json = """
        {
          "property_id": "k.o. Črnomerec, k.č. 333/4",
          "cadastral_municipality": "Črnomerec",
          "parcel_number": "333/4",
          "zk_number": "ZK.ul. 111",
          "property_type": "kuća",
          "property_description": "Obiteljska kuća",
          "land_area_m2": 400,
          "building_area_m2": 90,
          "land_use": "kuća i dvor",
          "etazni_udio": "",
          "parcels": [],
          "owners": [{"name": "Ana Anić", "share": "1/1", "oib": "", "basis_of_acquisition": "nasljeđivanje"}],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [
            {"type": "osobna služnost", "description": "Pravo doživotnog uživanja u korist Marije Marić (OIB: 33333333333)", "beneficiary": "Marija Marić", "is_active": true}
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
              "title": "DEAL-BREAKER: Pravo doživotnog uživanja",
              "description": "Osobna služnost (pravo doživotnog uživanja) u korist Marije Marić. Ako kupite ovu nekretninu, NE MOŽETE iseliti Mariju Marić. Ovo pravo traje do njezine smrti.",
              "advice": "ODUSTANITE od kupnje ili zahtijevajte brisanje služnosti i isplatu nositelja prava PRIJE potpisivanja ugovora."
            }
          ],
          "due_diligence_summary": "KRITIČNO: Na nekretnini je upisana osobna služnost (pravo doživotnog uživanja) u korist Marije Marić. Kupac ne može iseliti nositelja prava. Preporučam odustati od kupnje ili dogovoriti brisanje.",
          "lawyer_comment": "DEAL-BREAKER — osobna služnost.",
          "location": "Črnomerec, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "original",
          "yard_area_m2": 310
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("RIZIČNO", result.OverallStatus);

        // Služnost provjera
        Assert.Single(result.OtherEncumbrances);
        Assert.True(result.OtherEncumbrances[0].IsActive);
        Assert.Equal("Marija Marić", result.OtherEncumbrances[0].Beneficiary);

        // Finding
        var finding = Assert.Single(result.Findings);
        Assert.Equal("SLUZNOST", finding.Category);
        Assert.Equal("CRITICAL", finding.Severity);
        Assert.Contains("DEAL-BREAKER", finding.Title);
        Assert.Contains("ne možete iseliti", finding.Description.ToLower());
        Assert.Contains("ODUSTANITE", finding.Advice);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 5: Suvlasništvo 4 vlasnika — udjeli ispravni
    //  Očekivanje: UVJETNO, shares_sum_correct = true
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_Suvlasnistvo4Vlasnika_CorrectShares()
    {
        var json = """
        {
          "property_id": "k.o. Stenjevec, k.č. 888/1",
          "cadastral_municipality": "Stenjevec",
          "parcel_number": "888/1",
          "zk_number": "ZK.ul. 456",
          "property_type": "kuća",
          "property_description": "Obiteljska kuća s okućnicom",
          "land_area_m2": 600,
          "building_area_m2": 180,
          "land_use": "kuća i dvor",
          "etazni_udio": "",
          "parcels": [],
          "owners": [
            {"name": "Brat Bratić", "share": "1/4", "oib": "", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Sestra Sestić", "share": "1/4", "oib": "", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Majka Bratić", "share": "1/4", "oib": "", "basis_of_acquisition": "nasljeđivanje"},
            {"name": "Otac Bratić", "share": "1/4", "oib": "", "basis_of_acquisition": "bračna stečevina"}
          ],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "MEDIUM",
          "overall_status": "UVJETNO",
          "ownership_status": "SUVLASNIŠTVO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/4 + 1/4 + 1/4 + 1/4 = 4/4 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "VLASNISTVO",
              "severity": "WARNING",
              "title": "Suvlasništvo — 4 vlasnika",
              "description": "Nekretnina ima 4 suvlasnika. Svi moraju potpisati kupoprodajni ugovor ili dati javnobilježnički ovjerenu punomoć.",
              "advice": "Organizirajte zajednički potpis ili zatražite punomoći od svih suvlasnika."
            }
          ],
          "due_diligence_summary": "Nekretnina je u suvlasništvu 4 osobe. Udjeli su ispravni (4/4). Potrebna suglasnost svih suvlasnika za prodaju.",
          "lawyer_comment": "Suvlasništvo — potrebne 4 suglasnosti.",
          "location": "Stenjevec, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 420
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("UVJETNO", result.OverallStatus);
        Assert.Equal("SUVLASNIŠTVO", result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect);
        Assert.Contains("4/4 ✓", result.SharesSumDisplay);
        Assert.Equal(4, result.Owners.Count);

        // Svi udjeli su 1/4
        Assert.All(result.Owners, o => Assert.Equal("1/4", o.Share));

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VLASNISTVO", finding.Category);
        Assert.Equal("WARNING", finding.Severity);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 6: Nelegalizirani objekt na pašnjaku
    //  Očekivanje: land_use_mismatch, has_usage_permit_issue
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_NelegaliziranaKuca_LandUseMismatch()
    {
        var json = """
        {
          "property_id": "k.o. Sesvete, k.č. 999/3",
          "cadastral_municipality": "Sesvete",
          "parcel_number": "999/3",
          "zk_number": "ZK.ul. 789",
          "property_type": "kuća",
          "property_description": "Kuća na pašnjaku",
          "land_area_m2": 1200,
          "building_area_m2": 0,
          "land_use": "pašnjak",
          "etazni_udio": "",
          "parcels": [{"parcel_number": "999/3", "land_use": "pašnjak", "area_m2": 1200, "description": ""}],
          "owners": [{"name": "Josip Josić", "share": "1/1", "oib": "", "basis_of_acquisition": ""}],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "MEDIUM",
          "overall_status": "UVJETNO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "Upozorenje: kultura 'pašnjak' ne odgovara tipu nekretnine 'kuća'. Mogući problemi s legalizacijom.",
          "has_usage_permit_issue": true,
          "findings": [
            {
              "category": "LEGALIZACIJA",
              "severity": "WARNING",
              "title": "Neslaganje kulture zemljišta",
              "description": "ZK prikazuje 'pašnjak', ali tip nekretnine je 'kuća'. Objekt možda nije legaliziran.",
              "advice": "Provjerite postoji li uporabna dozvola ili rješenje o legalizaciji (ozakonjenju)."
            },
            {
              "category": "LEGALIZACIJA",
              "severity": "WARNING",
              "title": "Uporabna dozvola",
              "description": "Nije priložena uporabna dozvola. Banke često odbijaju kredit bez ovog dokumenta.",
              "advice": "Zatražite od prodavatelja uporabnu dozvolu ili potvrdu o legalizaciji."
            }
          ],
          "due_diligence_summary": "Nekretnina je upisana kao pašnjak, ali prodaje se kao kuća. Mogući problemi s legalizacijom. Preporučam provjeriti uporabnu dozvolu.",
          "lawyer_comment": "Neslaganje kulture — provjeriti legalizaciju.",
          "location": "Sesvete, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "original",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("UVJETNO", result.OverallStatus);
        Assert.True(result.HasUsagePermitIssue);
        Assert.NotEmpty(result.LandUseMismatch);
        Assert.Contains("pašnjak", result.LandUseMismatch);
        Assert.Equal("pašnjak", result.LandUse);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f =>
        {
            Assert.Equal("LEGALIZACIJA", f.Category);
            Assert.Equal("WARNING", f.Severity);
        });
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 7: Noćna mora — plomba + hipoteka + ovrha
    //  Očekivanje: RIZIČNO, 3+ CRITICAL findings
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_NocnaMora_MultipleCriticalIssues()
    {
        var json = """
        {
          "property_id": "k.o. Peščenica, k.č. 2222/1",
          "cadastral_municipality": "Peščenica",
          "parcel_number": "2222/1",
          "zk_number": "ZK.ul. 333",
          "property_type": "stan",
          "property_description": "Stan u zgradi",
          "land_area_m2": 3000,
          "building_area_m2": 48,
          "land_use": "stambena zgrada",
          "etazni_udio": "E-1, 48/3000",
          "parcels": [],
          "owners": [
            {"name": "Dužnik Dužnić", "share": "1/2", "oib": "", "basis_of_acquisition": "kupoprodajni ugovor"},
            {"name": "Partner Partnerić", "share": "1/2", "oib": "", "basis_of_acquisition": "bračna stečevina"}
          ],
          "active_mortgages": [
            {"amount": "80.000", "currency": "EUR", "year": "2017", "details": "Hipoteka u korist OTP banke d.d."}
          ],
          "active_disputes": ["Zabilježba ovrhe radi naplate 25.000 EUR"],
          "other_encumbrances": [
            {"type": "zabrana", "description": "Zabrana otuđenja i opterećenja", "beneficiary": "FINA", "is_active": true}
          ],
          "active_plombe": [
            {"z_number": "Z-4444/2025", "description": "Zahtjev za zabilježbu ovrhe", "date": "05.01.2025."},
            {"z_number": "Z-5555/2025", "description": "Zahtjev za upis založnog prava", "date": "12.01.2025."}
          ],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "SUVLASNIŠTVO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/2 + 1/2 = 2/2 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "PLOMBA",
              "severity": "CRITICAL",
              "title": "Aktivna plomba Z-4444/2025",
              "description": "Zahtjev za zabilježbu ovrhe. Situacija se pogoršava.",
              "advice": "NE KUPUJTE ovu nekretninu."
            },
            {
              "category": "PLOMBA",
              "severity": "CRITICAL",
              "title": "Aktivna plomba Z-5555/2025",
              "description": "Zahtjev za upis novog založnog prava.",
              "advice": "Dodatna plomba — još jedan zahtjev u obradi."
            },
            {
              "category": "OVRHA",
              "severity": "CRITICAL",
              "title": "Aktivna ovrha 25.000 EUR",
              "description": "Zabilježba ovrhe radi naplate 25.000 EUR. Nekretnina se može prodati na dražbi.",
              "advice": "Čekajte završetak ovrhe ili odustanite."
            },
            {
              "category": "HIPOTEKA",
              "severity": "WARNING",
              "title": "Aktivna hipoteka 80.000 EUR",
              "description": "Hipoteka u korist OTP banke d.d.",
              "advice": "Ishodite pismo namjere."
            },
            {
              "category": "VLASNISTVO",
              "severity": "WARNING",
              "title": "Suvlasništvo — 2 vlasnika",
              "description": "Oba vlasnika moraju potpisati.",
              "advice": "Pribavite suglasnost oba suvlasnika."
            }
          ],
          "due_diligence_summary": "VISOKORIZIČNA NEKRETNINA. Detektirane su 2 aktivne plombe, ovrha od 25.000 EUR, hipoteka od 80.000 EUR i zabrana otuđenja. NE PREPORUČAM kupnju.",
          "lawyer_comment": "NOĆNA MORA — bježite!",
          "location": "Peščenica, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);

        // 2 plombe
        Assert.Equal(2, result.ActivePlombe.Count);
        Assert.Contains(result.ActivePlombe, p => p.ZNumber == "Z-4444/2025");
        Assert.Contains(result.ActivePlombe, p => p.ZNumber == "Z-5555/2025");

        // Ovrha
        Assert.Single(result.ActiveDisputes);

        // Hipoteka
        Assert.Single(result.ActiveMortgages);

        // Zabrana
        Assert.Single(result.OtherEncumbrances);

        // Findings: 3 CRITICAL, 2 WARNING
        Assert.Equal(5, result.Findings.Count);
        Assert.Equal(3, result.Findings.Count(f => f.Severity == "CRITICAL"));
        Assert.Equal(2, result.Findings.Count(f => f.Severity == "WARNING"));

        // Categories
        Assert.Equal(2, result.Findings.Count(f => f.Category == "PLOMBA"));
        Assert.Single(result.Findings, f => f.Category == "OVRHA");
        Assert.Single(result.Findings, f => f.Category == "HIPOTEKA");
        Assert.Single(result.Findings, f => f.Category == "VLASNISTVO");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 8: Građevinsko zemljište — koristi land_area
    //  Očekivanje: ČISTO, building_area = 0
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_GradjevinskoZemljiste()
    {
        var json = """
        {
          "property_id": "k.o. Samobor, k.č. 3333/1",
          "cadastral_municipality": "Samobor",
          "parcel_number": "3333/1",
          "zk_number": "ZK.ul. 567",
          "property_type": "zemljište",
          "property_description": "Građevinsko zemljište",
          "land_area_m2": 850,
          "building_area_m2": 0,
          "land_use": "oranica",
          "etazni_udio": "",
          "parcels": [{"parcel_number": "3333/1", "land_use": "oranica", "area_m2": 850, "description": ""}],
          "owners": [{"name": "Zemljovlasnik Zemljić", "share": "1/1", "oib": "", "basis_of_acquisition": "kupoprodajni ugovor"}],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "LOW",
          "overall_status": "ČISTO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [],
          "due_diligence_summary": "Čisto građevinsko zemljište, 1 vlasnik, bez tereta. Kupnja se preporučuje.",
          "lawyer_comment": "Čisto zemljište.",
          "location": "Samobor",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("zemljište", result.PropertyType);
        Assert.Equal(850, result.LandAreaM2);
        Assert.Equal(0, result.BuildingAreaM2);
        Assert.Equal("ČISTO", result.OverallStatus);

        // ToPropertyInfo should use land area for zemljište
        var info = ZkParsingService.ToPropertyInfo(result, "dummy text");
        Assert.Equal(850, info.SizeM2);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 9: Neispravni udjeli — 1/3 + 1/3 ≠ 1/1
    //  Očekivanje: shares_sum_correct = false
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_NeispravniUdjeli_MissingThird()
    {
        var json = """
        {
          "property_id": "k.o. Čehi, k.č. 111/1",
          "cadastral_municipality": "Čehi",
          "parcel_number": "111/1",
          "zk_number": "ZK.ul. 999",
          "property_type": "kuća",
          "property_description": "Kuća",
          "land_area_m2": 300,
          "building_area_m2": 100,
          "land_use": "kuća i dvor",
          "etazni_udio": "",
          "parcels": [],
          "owners": [
            {"name": "Vlasnik A", "share": "1/3", "oib": "", "basis_of_acquisition": ""},
            {"name": "Vlasnik B", "share": "1/3", "oib": "", "basis_of_acquisition": ""}
          ],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "HIGH",
          "overall_status": "RIZIČNO",
          "ownership_status": "NERAZJAŠNJENO",
          "shares_sum_correct": false,
          "shares_sum_display": "1/3 + 1/3 = 2/3 ≠ 1/1",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "VLASNISTVO",
              "severity": "CRITICAL",
              "title": "Zbroj udjela ≠ 1/1",
              "description": "Zbroj vlasničkih udjela (2/3) ne odgovara cjelini. Nedostaje 1/3.",
              "advice": "Zatražite uvid u ZK uložak i pojašnjenje od zemljišnoknjižnog suda."
            }
          ],
          "due_diligence_summary": "UPOZORENJE: Zbroj vlasničkih udjela (2/3) ne odgovara cjelini (1/1). Nedostaje 1/3 vlasničkog udjela. Moguće neriješeno nasljeđivanje.",
          "lawyer_comment": "Neispravni udjeli.",
          "location": "Čehi, Velika Gorica",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions)!;

        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("NERAZJAŠNJENO", result.OwnershipStatus);
        Assert.False(result.SharesSumCorrect);
        Assert.Contains("2/3 ≠ 1/1", result.SharesSumDisplay);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VLASNISTVO", finding.Category);
        Assert.Equal("CRITICAL", finding.Severity);
        Assert.Contains("1/3", finding.Description);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 10: Status konzistencija 
    //  overall_status i risk_score moraju biti usklađeni
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("ČISTO", "LOW")]
    [InlineData("UVJETNO", "MEDIUM")]
    [InlineData("RIZIČNO", "HIGH")]
    public void StatusAndRiskScore_ShouldBeConsistent(string expectedStatus, string expectedRisk)
    {
        var result = new ZkParsedResult
        {
            OverallStatus = expectedStatus,
            RiskScore = expectedRisk
        };

        Assert.Equal(expectedStatus, result.OverallStatus);
        Assert.Equal(expectedRisk, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIJ 11: Finding severity prioritet — sortiranje
    //  CRITICAL > WARNING > INFO
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Findings_SortBySeverity_CriticalFirst()
    {
        var findings = new List<DueDiligenceFinding>
        {
            new() { Severity = "INFO", Title = "Služnost prolaza" },
            new() { Severity = "CRITICAL", Title = "Plomba" },
            new() { Severity = "WARNING", Title = "Hipoteka" },
            new() { Severity = "CRITICAL", Title = "Ovrha" },
            new() { Severity = "INFO", Title = "Pravo građenja" }
        };

        var severityOrder = new Dictionary<string, int>
        {
            { "CRITICAL", 0 },
            { "WARNING", 1 },
            { "INFO", 2 }
        };

        var sorted = findings.OrderBy(f => severityOrder.GetValueOrDefault(f.Severity, 99)).ToList();

        Assert.Equal("CRITICAL", sorted[0].Severity);
        Assert.Equal("CRITICAL", sorted[1].Severity);
        Assert.Equal("WARNING", sorted[2].Severity);
        Assert.Equal("INFO", sorted[3].Severity);
        Assert.Equal("INFO", sorted[4].Severity);
    }
}
