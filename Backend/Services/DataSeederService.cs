using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Seeds realistic Croatian demo data: 10 clients → 10 cases → 100 events.
/// Uses ClientCaseService (PostgREST) — best-effort, collects errors.
/// Admin-only, triggered via POST /api/admin/seed-demo-data.
/// </summary>
public sealed class DataSeederService
{
    private readonly ClientCaseService _clientCaseService;
    private readonly ILogger<DataSeederService> _logger;
    private static readonly Random Rng = new();

    public DataSeederService(ClientCaseService clientCaseService, ILogger<DataSeederService> logger)
    {
        _clientCaseService = clientCaseService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    //  Croatian demo data arrays
    // ═══════════════════════════════════════════════════════════

    private static readonly string[] FirstNamesMale =
        ["Ivan", "Marko", "Luka", "Petar", "Ante", "Josip", "Tomislav", "Nikola", "Stjepan", "Matej"];

    private static readonly string[] FirstNamesFemale =
        ["Ana", "Marija", "Ivana", "Petra", "Maja", "Lucija", "Katarina", "Martina", "Sara", "Elena"];

    private static readonly string[] LastNames =
        ["Horvat", "Kovačević", "Babić", "Marić", "Jurić", "Novak", "Knežević", "Vuković", "Matić", "Pavlović"];

    private static readonly string[] Companies =
    [
        "Adriatic Građevinski d.o.o.", "Dalmatina Trade d.o.o.", "Zagreb Consulting d.o.o.",
        "Pannonia Invest d.o.o.", "Slavonija Agrar d.o.o."
    ];

    private static readonly string[] Cities =
        ["Zagreb", "Split", "Rijeka", "Osijek", "Zadar", "Velika Gorica", "Slavonski Brod", "Pula", "Karlovac", "Sisak"];

    private static readonly string[] Streets =
    [
        "Ilica 42", "Vukovarska 15", "Maksimirska 100", "Savska 88",
        "Držićeva 5", "Heinzelova 70", "Dubrovačka 3", "Zvonimirova 22",
        "Petrinjska 9", "Jurišićeva 14"
    ];

    private static readonly string[] PostalCodes =
        ["10000", "21000", "51000", "31000", "23000", "10410", "35000", "52100", "47000", "44000"];

    private static readonly string[] Courts =
    [
        "Općinski građanski sud u Zagrebu", "Općinski sud u Splitu",
        "Općinski sud u Rijeci", "Trgovački sud u Zagrebu",
        "Županijski sud u Osijeku", "Općinski sud u Zadru",
        "Trgovački sud u Splitu", "Općinski sud u Puli",
        "Općinski sud u Karlovcu", "Županijski sud u Zagrebu"
    ];

    private static readonly string[] Judges =
    [
        "mr. sc. Ivica Perić", "Mirjana Kovačić", "dr. sc. Branko Šimić",
        "Anita Tomić", "Davor Lukić", "Jelena Radić",
        "Robert Kolarić", "Vesna Matijević", "Igor Bošnjak", "Tanja Filipović"
    ];

    private static readonly (string Naziv, string Vrsta, string Opis)[] CaseTemplates =
    [
        ("Parnica radi naknade štete", "parnica",
         "Tužba radi naknade materijalne i nematerijalne štete nastale u prometnoj nesreći."),
        ("Ovrha na temelju vjerodostojne isprave", "ovrha",
         "Ovrha radi naplate neplaćenih faktura za obavljene usluge."),
        ("Razvod braka s podjelom imovine", "izvanparnični",
         "Postupak sporazumnog razvoda braka s prijedlogom za podjelu bračne stečevine."),
        ("Radni spor — otkaz ugovora o radu", "parnica",
         "Tužba radi poništenja odluke o izvanrednom otkazu ugovora o radu."),
        ("Kupoprodaja nekretnine — zastupanje", "savjetovanje",
         "Pravna pomoć pri kupoprodaji stana u Zagrebu, uključujući due diligence ZK izvadaka."),
        ("Tužba radi isplate plaće", "parnica",
         "Tužba radi isplate zaostalih plaća i doprinosa za 6 mjeseci."),
        ("Ostavinski postupak", "izvanparnični",
         "Zastupanje nasljednika u ostavinskom postupku s osporavanom oporukom."),
        ("Stečajni postupak — vjerovnik", "stečajni",
         "Prijava tražbine u stečajnom postupku nad društvom Građevinar d.o.o."),
        ("Ugovor o zakupu poslovnog prostora", "savjetovanje",
         "Izrada i pregovaranje ugovora o zakupu poslovnog prostora na 5 godina."),
        ("Žalba na porezno rješenje", "upravni",
         "Žalba protiv rješenja Porezne uprave o utvrđivanju porezne obveze."),
    ];

    private static readonly string[] EventDescriptions =
    [
        "Sastavljen podnesak prema sudu — tužba s dokaznim prijedlozima.",
        "Održano ročište za glavnu raspravu — ispitana dva svjedoka.",
        "Pravni savjet klijentu o mogućnostima nagodbe.",
        "Pristup sudu radi uvida u spis i preuzimanja preslike odluke.",
        "Pregled spisa u sudskoj pisarnici — kopiranje ključnih dokumenata.",
        "Sastanak s klijentom radi pripreme za ročište.",
        "Upućen dopis protivnoj strani s prijedlogom nagodbe.",
        "Izjavljena žalba protiv prvostupanjske presude.",
        "Podnesen prigovor na rješenje o ovrsi.",
        "Postignuta nagodba izvansudskim putem — potpisan sporazum.",
        "Podnesen ovršni prijedlog na temelju pravomoćne presude.",
        "Uvid u dokaze kod vještaka — medicinska dokumentacija.",
        "Angažiran sudski vještak za procjenu vrijednosti nekretnine.",
        "Telefonska konzultacija s klijentom o statusu predmeta.",
        "Priprema dokaznog materijala za sljedeće ročište.",
        "Sastavljeno očitovanje na odgovor na tužbu protivne strane.",
        "Analiza sudske prakse za konkretan pravni problem.",
        "Podnesen zahtjev za odgodu ročišta iz opravdanih razloga.",
        "Primljen i analiziran nalaz sudskog vještaka.",
        "Održan završni sastanak s klijentom — pregled troškova."
    ];

    // ═══════════════════════════════════════════════════════════
    //  Seed method
    // ═══════════════════════════════════════════════════════════

    public sealed class SeedResult
    {
        public int ClientsCreated { get; set; }
        public int CasesCreated { get; set; }
        public int EventsCreated { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    /// <summary>
    /// Seeds 10 clients, 10 cases (1 per client), and 100 events (10 per case).
    /// Returns counts and any errors encountered.
    /// </summary>
    public async Task<SeedResult> SeedAsync(string userId, string? tenantId)
    {
        var result = new SeedResult();
        _logger.LogInformation("DataSeeder: Starting seed for user {UserId}, tenant {TenantId}", userId, tenantId);

        // ── 1. Create 10 clients ──
        var createdClients = new List<ClientReadRecord>();
        for (var i = 0; i < 10; i++)
        {
            try
            {
                var client = GenerateClient(i, userId);
                var created = await _clientCaseService.CreateClientAsync(client);
                if (created != null)
                {
                    createdClients.Add(created);
                    result.ClientsCreated++;
                }
                else
                {
                    result.Errors.Add($"Klijent #{i + 1} ({client.ImeNaziv}): insert vratio null.");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Klijent #{i + 1}: {ex.Message}");
                _logger.LogError(ex, "DataSeeder: Failed to create client #{Index}", i + 1);
            }
        }

        // ── 2. Create 10 cases (1 per client) ──
        var createdCases = new List<CaseReadRecord>();
        for (var i = 0; i < createdClients.Count && i < CaseTemplates.Length; i++)
        {
            try
            {
                var caseRecord = GenerateCase(i, userId, createdClients[i].Id);
                var created = await _clientCaseService.CreateCaseAsync(caseRecord);
                if (created != null)
                {
                    createdCases.Add(created);
                    result.CasesCreated++;
                }
                else
                {
                    result.Errors.Add($"Predmet #{i + 1} ({CaseTemplates[i].Naziv}): insert vratio null.");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Predmet #{i + 1}: {ex.Message}");
                _logger.LogError(ex, "DataSeeder: Failed to create case #{Index}", i + 1);
            }
        }

        // ── 3. Create 10 events per case (100 total) ──
        foreach (var caseRec in createdCases)
        {
            for (var j = 0; j < 10; j++)
            {
                try
                {
                    var eventRecord = GenerateEvent(j, userId, tenantId, caseRec.Id);
                    var created = await _clientCaseService.CreateCaseEventAsync(eventRecord);
                    if (created != null)
                    {
                        result.EventsCreated++;
                    }
                    else
                    {
                        result.Errors.Add($"Događaj #{j + 1} za predmet {caseRec.BrojPredmeta}: insert vratio null.");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Događaj #{j + 1} za predmet {caseRec.BrojPredmeta}: {ex.Message}");
                    _logger.LogError(ex, "DataSeeder: Failed to create event for case {CaseId}", caseRec.Id);
                }
            }
        }

        _logger.LogInformation(
            "DataSeeder: Complete — {Clients} clients, {Cases} cases, {Events} events, {Errors} errors",
            result.ClientsCreated, result.CasesCreated, result.EventsCreated, result.Errors.Count);

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  Generators
    // ═══════════════════════════════════════════════════════════

    private static ClientRecord GenerateClient(int index, string userId)
    {
        var isCompany = index >= 7; // last 3 are companies
        string name;
        string tip;

        if (isCompany)
        {
            name = Companies[index - 7];
            tip = "pravna";
        }
        else
        {
            var isFemale = index % 2 == 1;
            var firstNames = isFemale ? FirstNamesFemale : FirstNamesMale;
            name = $"{firstNames[index % firstNames.Length]} {LastNames[index]}";
            tip = "fizicka";
        }

        return new ClientRecord
        {
            UserId = userId,
            ImeNaziv = name,
            Oib = GenerateOib(),
            Adresa = Streets[index % Streets.Length],
            Grad = Cities[index % Cities.Length],
            PostanskiBroj = PostalCodes[index % PostalCodes.Length],
            Email = $"demo{index + 1}@pravotek.hr",
            Telefon = $"+385 {Rng.Next(91, 99)} {Rng.Next(100, 999)} {Rng.Next(1000, 9999)}",
            Tip = tip,
            Napomena = "Demo podatak — generirano automatski."
        };
    }

    private static CaseRecord GenerateCase(int index, string userId, string clientId)
    {
        var tmpl = CaseTemplates[index];
        var year = 2024;
        var startDate = new DateOnly(year, Rng.Next(1, 12), Rng.Next(1, 28));

        return new CaseRecord
        {
            UserId = userId,
            ClientId = clientId,
            BrojPredmeta = $"P-{Rng.Next(100, 999)}/{year}",
            Naziv = tmpl.Naziv,
            Opis = tmpl.Opis,
            Vrsta = tmpl.Vrsta,
            Status = "aktivan",
            VrijednostSpora = Math.Round((decimal)(Rng.NextDouble() * 95_000 + 5_000), 2),
            Sud = Courts[index % Courts.Length],
            Sudac = Judges[index % Judges.Length],
            DatumPokretanja = startDate.ToString("yyyy-MM-dd")
        };
    }

    private static CaseEventRecord GenerateEvent(int eventIndex, string userId, string? tenantId, string caseId)
    {
        var types = CaseEventTypes.Allowed.ToArray();
        var eventType = types[eventIndex % types.Length];
        var defaultPoints = CaseEventTypes.DefaultPoints.GetValueOrDefault(eventType, 25);

        // Spread events over ~6 months from 2024-01
        var daysOffset = eventIndex * Rng.Next(12, 25);
        var eventDate = new DateOnly(2024, 1, 15).AddDays(daysOffset);
        if (eventDate > DateOnly.FromDateTime(DateTime.Today))
            eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-Rng.Next(1, 30)));

        return new CaseEventRecord
        {
            CaseId = caseId,
            UserId = userId,
            TenantId = tenantId,
            EventDate = eventDate.ToString("yyyy-MM-dd"),
            EventType = eventType,
            Description = EventDescriptions[eventIndex % EventDescriptions.Length],
            TariffItem = $"Tbr. {Rng.Next(1, 35)}/1",
            TariffPoints = defaultPoints,
            PointValue = 0.50m,
            IsBillable = eventIndex % 5 != 0, // ~80% billable
            BillingStatus = "pending"
        };
    }

    /// <summary>Generates a random 11-digit OIB (not Mod11-validated, but realistic format).</summary>
    private static string GenerateOib()
    {
        var digits = new char[11];
        digits[0] = (char)('1' + Rng.Next(0, 9)); // first digit 1-9
        for (var i = 1; i < 11; i++)
            digits[i] = (char)('0' + Rng.Next(0, 10));
        return new string(digits);
    }
}
