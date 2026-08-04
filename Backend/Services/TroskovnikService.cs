using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Odvjetnička tarifa – izračun troškovnika prema Tarifi o nagradama
/// i naknadi troškova za rad odvjetnika (NN 2023).
/// Tbr. 54: Vrijednost boda = 2,00 EUR.
/// </summary>
public sealed class TroskovnikService
:ITroskovnikService {
    // ── Tbr. 7, toč. 1 — base point lookup ───────────────────────

    // ── Osnovica: "Tbr. 7. toč. 1., 4., 7. i 8." ──────────────────

    /// <summary>
    /// Razrješava osnovicu na koju se primjenjuju postoci iz Tbr. 8. i Tbr. 9.
    ///
    /// Tarifa na devet mjesta upućuje na „Tbr. 7. toč. 1., 4., 7. i 8." (Tbr. 8. toč. 1, 3, 4;
    /// Tbr. 9. toč. 1, 2, 3, 5, 6; Tbr. 10.). To nisu četiri različita iznosa nego četiri
    /// varijante ISTE osnovice — koja se primjenjuje ovisi o vrsti postupka.
    ///
    /// Prije uvođenja ove metode osnovica je uvijek bila toč. 1, pa Tbr. 8. toč. 1 nije
    /// radio za autorskopravne sporove, arbitražu ni mirenje, a neprocjenjivi predmeti
    /// padali su na 25 bodova umjesto na 100/200/500 iz Tbr. 45.
    /// </summary>
    public static OsnovicaRezultat IzracunajOsnovicu(TroskovnikRequest request)
    {
        var vps = EfektivniVps(request);

        switch (request.OsnovicaTbr7)
        {
            // Tbr. 7. toč. 4 al. 1: "nagrada po Tbr. 7. toč. 1., ali ne manje od 100 bodova"
            case OsnovicaTbr7.Toc4Autorsko:
                return new OsnovicaRezultat(
                    Math.Max(Tbr71Bodovi(vps), 100),
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 4 al. 1 — nagrada po Tbr. 7. toč. 1, ali ne manje od 100 bodova",
                    false);

            // Tbr. 7. toč. 4 al. 2: osnovica je ista kao toč. 1, ali VPS je petogodišnji iznos.
            case OsnovicaTbr7.Toc4Renta:
                return new OsnovicaRezultat(
                    Tbr71Bodovi(vps),
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 4 al. 2 — kao vrijednost spora uzima se petogodišnji iznos rente, zakupa ili najma",
                    false);

            // Tbr. 7. toč. 7 st. 1: "Tužbe i obrazloženi podnesci pred domaćom arbitražom po Tbr. 7. toč. 1."
            case OsnovicaTbr7.Toc7DomacaArbitraza:
                return new OsnovicaRezultat(
                    Tbr71Bodovi(vps),
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 st. 1 — nagrada po Tbr. 7. toč. 1",
                    false);

            // Tbr. 7. toč. 7 st. 2: "...pred međunarodnom arbitražom (...) po Tbr. 7. toč. 1. s povišenjem od 100%."
            case OsnovicaTbr7.Toc7MedunarodnaArbitraza:
                return new OsnovicaRezultat(
                    Tbr71Bodovi(vps) * 2m,
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 st. 2 — Tbr. 7. toč. 1 s povišenjem od 100%",
                    false);

            // Tbr. 7. toč. 8: "Za zastupanje u postupcima mirnog rješavanja sporova (...) po Tbr. 7. toč. 1."
            case OsnovicaTbr7.Toc8Mirenje:
                return new OsnovicaRezultat(
                    Tbr71Bodovi(vps),
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 8 — nagrada po Tbr. 7. toč. 1",
                    false);

            // Tbr. 45. toč. 1–3 — neprocjenjivi predmeti, bodovi po razini suda.
            case OsnovicaTbr7.Tbr45Neprocjenjiv:
            {
                if (request.RazinaSuda is null ||
                    !Models.RazinaSuda.Bodovi.TryGetValue(request.RazinaSuda, out var bodovi))
                {
                    // Ne postoji "razumna zadana" razina suda — razlika je do peterostruke.
                    throw new TroskovnikValidationException(
                        "Tbr. 45 zahtijeva odabranu razinu suda (općinski / županijski i trgovački / Vrhovni). " +
                        "Bez nje se bodovi ne mogu odrediti — razlika je do peterostruke.");
                }

                if (request.Tbr45Nacin is null || !Models.Tbr45Nacin.Sve.Contains(request.Tbr45Nacin))
                {
                    throw new TroskovnikValidationException(
                        "Tbr. 45 zahtijeva odabrani način obračuna. Tarifa ne kaže primjenjuju li se " +
                        "postoci iz Tbr. 8 i Tbr. 9 na iznos iz Tbr. 45, pa sustav ne smije pretpostaviti.");
                }

                var tocka = request.RazinaSuda switch
                {
                    Models.RazinaSuda.Opcinski            => "Tbr. 45. toč. 1 — pred općinskim sudom i tijelima iste razine 100 bodova",
                    Models.RazinaSuda.ZupanijskiTrgovacki => "Tbr. 45. toč. 2 — pred županijskim i trgovačkim sudovima te tijelima iste razine 200 bodova",
                    _                                     => "Tbr. 45. toč. 3 — pred Vrhovnim sudom RH i tijelima iste razine 500 bodova"
                };

                return new OsnovicaRezultat(
                    bodovi,
                    $"{TarifaConstants.NazivTarife}, {tocka}",
                    request.Tbr45Nacin == Models.Tbr45Nacin.FiksnoPoRadnji);
            }

            case OsnovicaTbr7.Toc1:
                return new OsnovicaRezultat(
                    Tbr71Bodovi(vps),
                    $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 1 — nagrada prema vrijednosti predmeta spora",
                    false);

            default:
                throw new TroskovnikValidationException(
                    $"Nepoznata osnovica \"{request.OsnovicaTbr7}\". " +
                    $"Dopušteno: {string.Join(", ", OsnovicaTbr7.Sve)}.");
        }
    }

    /// <summary>
    /// Vrijednost predmeta spora koja se stvarno koristi za Tbr. 7. toč. 1 lookup.
    ///
    /// Tbr. 7. toč. 4 al. 2: „za utvrđivanje rente, zakupa, najma i u drugim sličnim
    /// postupcima kao vrijednost spora uzima se petogodišnji iznos rente, zakupa, najma
    /// i drugih sličnih davanja."
    ///
    /// Preračun se radi SAMO uz osnovicu `toc4_renta` i uz izričito odabrano razdoblje —
    /// ništa se ne izvodi tiho.
    /// </summary>
    public static decimal EfektivniVps(TroskovnikRequest request)
    {
        if (request.OsnovicaTbr7 != OsnovicaTbr7.Toc4Renta || request.RentaIznos <= 0)
            return request.VrijednostSpora;

        return request.RentaRazdoblje switch
        {
            RentaRazdoblje.Mjesecno => request.RentaIznos * 60m,  // 5 godina × 12 mjeseci
            RentaRazdoblje.Godisnje => request.RentaIznos * 5m,   // 5 godina
            _                       => request.VrijednostSpora    // razdoblje nije odabrano → bez preračuna
        };
    }

    /// <summary>
    /// Tbr. 41. toč. 1 — povišenje do 100 % za posebno stručno i specijalističko znanje.
    /// Tbr. 41. toč. 2 — sniženje do 50 %.
    /// Vraća neto udio (0,25 = +25 %; −0,10 = −10 %). Oba su zadano 0.
    /// </summary>
    public static decimal Tbr41Udio(decimal povisenjePostotak, decimal snizenjePostotak)
        => (povisenjePostotak / 100m) - (snizenjePostotak / 100m);

    /// <summary>
    /// Razrješava postotke Tbr. 41 za jednu stavku i provjerava ih.
    ///
    /// Stavka koja nema vlastiti postotak nasljeđuje onaj s razine zahtjeva — to je
    /// prečac „primijeni na sve stavke".
    ///
    /// Povišenje i sniženje ne mogu stajati na istoj stavci: toč. 1 i toč. 2 polaze od
    /// suprotnih razloga (posebna stručnost naspram koristi za stranku), pa bi njihovo
    /// istovremeno navođenje značilo da nijedan razlog nije stvaran.
    /// </summary>
    private static (decimal Povisenje, decimal Snizenje) RazrijesiTbr41(
        TroskovnikItemRequest stavka, TroskovnikRequest request, TarifnaStavka def)
    {
        var povisenje = stavka.Tbr41PovisenjePostotak ?? request.Tbr41PovisenjePostotak;
        var snizenje = stavka.Tbr41SnizenjePostotak ?? request.Tbr41SnizenjePostotak;

        if (povisenje > 0m && snizenje > 0m)
            throw new TroskovnikValidationException(
                $"Stavka \"{def.Opis}\" ima istovremeno povišenje ({povisenje} %) i sniženje " +
                $"({snizenje} %) po Tbr. 41. Toč. 1 i toč. 2 polaze od suprotnih razloga, pa se " +
                "na istu stavku ne mogu primijeniti istovremeno.");

        if (povisenje < 0m || povisenje > Tbr41MaxPovisenje)
            throw new TroskovnikValidationException(
                $"Povišenje po Tbr. 41. toč. 1 za stavku \"{def.Opis}\" iznosi {povisenje} %. " +
                $"Tarifa dopušta najviše {Tbr41MaxPovisenje} %.");

        if (snizenje < 0m || snizenje > Tbr41MaxSnizenje)
            throw new TroskovnikValidationException(
                $"Sniženje po Tbr. 41. toč. 2 za stavku \"{def.Opis}\" iznosi {snizenje} %. " +
                $"Tarifa dopušta najviše {Tbr41MaxSnizenje} %.");

        return (povisenje, snizenje);
    }

    /// <summary>Tbr. 41. toč. 1 — "tarifne stavke mogu se povisiti za 100%".</summary>
    public const decimal Tbr41MaxPovisenje = 100m;

    /// <summary>Tbr. 41. toč. 2 — "pojedine tarifne stavke odvjetnik može sniziti za 50%".</summary>
    public const decimal Tbr41MaxSnizenje = 50m;

    /// <summary>Citat odredbe uz prilagođenu stavku; prazan kad Tbr. 41 nije primijenjen.</summary>
    private static string Tbr41Izvor(decimal povisenje, decimal snizenje)
    {
        if (povisenje > 0m)
            return $"{TarifaConstants.NazivTarife}, Tbr. 41. toč. 1 — povišenje od {povisenje} % " +
                   "za posebno stručno i specijalističko znanje, osobitu težinu predmeta ili " +
                   "posebnu odgovornost za obavljeni rad";

        if (snizenje > 0m)
            return $"{TarifaConstants.NazivTarife}, Tbr. 41. toč. 2 — sniženje od {snizenje} %; " +
                   "\"pojedine tarifne stavke odvjetnik može sniziti za 50%\"";

        return "";
    }

    /// <summary>
    /// Izračunava broj bodova prema Tbr. 7. toč. 1. na temelju
    /// vrijednosti predmeta spora u EUR.
    /// </summary>
    public static int Tbr71Bodovi(decimal vrijednost)
    {
        if (vrijednost <= 0) return 25;

        // Fixed bands
        if (vrijednost <= 332.00m) return 25;
        if (vrijednost <= 664.00m) return 50;
        if (vrijednost <= 1_327.00m) return 75;
        if (vrijednost <= 13_272.00m) return 100;
        if (vrijednost <= 33_181.00m) return 250;
        if (vrijednost <= 66_361.00m) return 500;

        // Progressive bands
        if (vrijednost <= 663_614.00m)
        {
            var extra = (int)Math.Ceiling((vrijednost - 66_361.00m) / 133.00m);
            return 500 + extra;
        }

        if (vrijednost <= 1_327_228.00m)
        {
            var extra = (int)Math.Ceiling((vrijednost - 663_614.00m) / 265.00m);
            return 4991 + extra;
        }

        // Over 1,327,228 EUR
        var extra3 = (int)Math.Ceiling((vrijednost - 1_327_228.00m) / 664.00m);
        return Math.Min(7496 + extra3, 10_000);
    }

    // ── Catalog of all tariff items ───────────────────────────────

    public static IReadOnlyList<TarifnaStavka> GetKatalog() => _katalog;

    private static readonly List<TarifnaStavka> _katalog =
    [
        // ═══════════════════════════════════════════════════════════
        // I.  KAZNENI I PREKRŠAJNI POSTUPAK
        // ═══════════════════════════════════════════════════════════

        // ── Tbr. 1 — Sastavljanje podnesaka ──
        // Toč. 1, 2, 3 → "nagrada po Tbr. 4. toč. 1." = bodovi ovise o zapriječenoj kazni
        // Tbr. 1. toč. 1 glasi samo "odvjetniku pripada nagrada po Tbr. 4. toč. 1." —
        // nikakav minimum se ne spominje. Zatečeni MinBodova = 100 nije imao izvor i
        // poništavao je umanjenje od 50% za branitelja po službenoj dužnosti.
        new() { Id = "kaz_privatna_tuzba", Kategorija = "Kazneni postupak", Opis = "Privatna tužba / kaznena prijava / prijedlog za kazneni progon / optužni prijedlog (Tbr. 1.1)", TarifniBroj = "Tbr. 1.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 1 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 100, Jedinica = "komad" },
        new() { Id = "kaz_podnesak_obrana", Kategorija = "Kazneni postupak", Opis = "Podnesci s navođenjem činjenica / pisana obrana (Tbr. 1.2)", TarifniBroj = "Tbr. 1.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 2 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 101, Jedinica = "komad" },
        new() { Id = "kaz_zahtjev_ostecenik", Kategorija = "Kazneni postupak", Opis = "Zahtjev oštećenika za dokazne radnje / preuzimanje progona (Tbr. 1.3)", TarifniBroj = "Tbr. 1.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 1. toč. 3 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 102, Jedinica = "komad" },
        new() { Id = "kaz_imovinskopravni", Kategorija = "Kazneni postupak", Opis = "Podnesak za imovinskopravni zahtjev oštećenika (Tbr. 1.4)", TarifniBroj = "Tbr. 1.4", FiksniBody = 75, SortOrder = 103, Jedinica = "komad" },
        new() { Id = "kaz_ostali_podnesci", Kategorija = "Kazneni postupak", Opis = "Ostali podnesci u kaznenom postupku (Tbr. 1.5)", TarifniBroj = "Tbr. 1.5", FiksniBody = 50, SortOrder = 104, Jedinica = "komad" },

        // ── Tbr. 2 — Prijedlozi izvan rasprave ──
        // Toč. 1, 2, 4 → fiksni; Toč. 3 → "nagrada po Tbr. 4. toč. 1."
        new() { Id = "kaz_odgoda_izvrsenja", Kategorija = "Kazneni postupak", Opis = "Prijedlog za odgodu izvršenja kazne / uvjetni otpust (Tbr. 2.1)", TarifniBroj = "Tbr. 2.1", FiksniBody = 100, SortOrder = 110, Jedinica = "komad" },
        new() { Id = "kaz_rehabilitacija", Kategorija = "Kazneni postupak", Opis = "Prijedlog za rehabilitaciju (Tbr. 2.2)", TarifniBroj = "Tbr. 2.2", FiksniBody = 50, SortOrder = 111, Jedinica = "komad" },
        new() { Id = "kaz_pomilovanje", Kategorija = "Kazneni postupak", Opis = "Molba za pomilovanje (Tbr. 2.3)", TarifniBroj = "Tbr. 2.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 2. toč. 3 — nagrada po Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 112, Jedinica = "komad" },
        new() { Id = "kaz_opoziv_uvjetne", Kategorija = "Kazneni postupak", Opis = "Prijedlog za opoziv uvjetne osude (Tbr. 2.4)", TarifniBroj = "Tbr. 2.4", FiksniBody = 50, SortOrder = 113, Jedinica = "komad" },

        // ── Tbr. 3 — Zastupanje i obrana pred policijom / DO / sucem istrage ──
        // Toč. 1: prvi sat = Tbr. 4.1, svaki sljedeći sat = 50 bodova
        // Toč. 3: punomoćnik oštećenika = 50% nagrade iz Tbr. 3 (= 50% Tbr. 4.1)
        // Toč. 4: odgoda radnje = 50% nagrade iz toč. 1 (= 50% Tbr. 4.1)
        new() { Id = "kaz_sudjelovanje_istraga", Kategorija = "Kazneni postupak", Opis = "Sudjelovanje pred policijom / DO / sucem istrage – prvi sat (Tbr. 3.1)", TarifniBroj = "Tbr. 3.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 1 — nagrada iz Tbr. 4. toč. 1 za prvi sat", KazneniBase = true, SortOrder = 120, Jedinica = "sat" },
        new() { Id = "kaz_istraga_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Svaki sljedeći započeti sat u istrazi (Tbr. 3.1)", TarifniBroj = "Tbr. 3.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 1 — 50 bodova za svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 121, Jedinica = "sat" },
        // Tbr. 3. toč. 2 — "Za radnje iz toč. 1 ovog tarifnog broja koje policija, državno
        // odvjetništvo, Ured europskog javnog tužitelja ili sud, provode izvan zgrade svog
        // sjedišta odvjetniku pripada, osim nagrade iz toč. 1. ovog tarifnog broja i
        // naknada po Tbr. 49."
        // Tbr. 49 su stvarni putni troškovi — nisu bodovi i obračunavaju se zasebno.
        new() { Id = "kaz_istraga_izvan_sjedista", Kategorija = "Kazneni postupak", Opis = "Radnje pred policijom / DO / EPPO / sucem istrage izvan zgrade sjedišta (Tbr. 3.2) – nagrada kao po Tbr. 3.1, uz naknadu putnih troškova po Tbr. 49", TarifniBroj = "Tbr. 3.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 2 — nagrada iz Tbr. 3. toč. 1 (Tbr. 4. toč. 1) uz naknadu po Tbr. 49 (stvarni putni troškovi, izvan bodovnog sustava)", KazneniBase = true, SortOrder = 124, Jedinica = "sat" },

        new() { Id = "kaz_istraga_ostecenik", Kategorija = "Kazneni postupak", Opis = "Punomoćnik oštećenika / žrtve u istrazi – 50% Tbr. 4.1 (Tbr. 3.3)", TarifniBroj = "Tbr. 3.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 3 — 50% nagrade iz Tbr. 3 (Tbr. 4. toč. 1)", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 122, Jedinica = "sat" },
        new() { Id = "kaz_istraga_odgoda", Kategorija = "Kazneni postupak", Opis = "Pristup odgođenoj radnji u istrazi – 50% Tbr. 4.1, uz naknadu po Tbr. 49 (Tbr. 3.4)", TarifniBroj = "Tbr. 3.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 3. toč. 4 — 50% nagrade iz Tbr. 3. toč. 1 uz naknadu po Tbr. 49", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 123, Jedinica = "komad" },

        // ── Tbr. 4 — Obrana na raspravi ──
        new() { Id = "kaz_obrana", Kategorija = "Kazneni postupak", Opis = "Obrana okrivljenika na raspravi – prvi sat (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", KazneniBase = true, SortOrder = 130, Jedinica = "sat" },
        new() { Id = "kaz_obrana_prekrsaj", Kategorija = "Kazneni postupak", Opis = "Obrana u prekršajnom postupku (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 7 — obrana u prekršajnom postupku, 100 bodova", FiksniBody = 100, SortOrder = 131, Jedinica = "sat" },
        new() { Id = "kaz_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Svaki sljedeći započeti sat rasprave (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 — 50 bodova za svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 132, Jedinica = "sat" },
        new() { Id = "kaz_objava_presude", Kategorija = "Kazneni postupak", Opis = "Ročište za objavu presude (Tbr. 4.2)", TarifniBroj = "Tbr. 4.2", FiksniBody = 50, SortOrder = 133, Jedinica = "komad" },
        new() { Id = "kaz_sjednica_drugostupanjska", Kategorija = "Kazneni postupak", Opis = "Sjednica vijeća drugog stupnja (Tbr. 4.2) – po Tbr. 4.1", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 2 — kao za obranu pred sudom prvog stupnja", KazneniBase = true, SortOrder = 134, Jedinica = "komad" },
        // Tbr. 4. toč. 2 al. 3: "— na raspravi pred sudom drugog stupnja odvjetniku pripada
        // nagrada kao za obranu pred sudom prvog stupnja s povišenjem od 25%, a na raspravi
        // pred sudom drugog i trećeg stupnja za kaznena djela za koja je predviđena kazna
        // dugotrajnog zatvora s povišenjem od 50%."
        new() { Id = "kaz_rasprava_drugostupanjska", Kategorija = "Kazneni postupak", Opis = "Rasprava pred sudom drugog (i trećeg) stupnja (Tbr. 4.2) – Tbr. 4.1 + 25%, a kod dugotrajnog zatvora + 50%", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 3 — prvi stupanj + 25%, a kod dugotrajnog zatvora + 50%", KazneniBase = true, KazneniPovecanje = 0.25m, KazneniPovecanjeDugotrajni = 0.50m, SortOrder = 135, Jedinica = "komad" },
        // Tbr. 4. toč. 2 al. 4 — "na sjednici povodom izvanrednih pravnih lijekova odvjetniku
        // pripada nagrada kao za obranu pred sudom drugog stupnja."
        // "Obrana pred sudom drugog stupnja" definirana je u al. 3 — nagrada iz toč. 1
        // uvećana za 25%, odnosno za 50% kod dugotrajnog zatvora.
        new() { Id = "kaz_sjednica_izvanredni", Kategorija = "Kazneni postupak", Opis = "Sjednica povodom izvanrednih pravnih lijekova (Tbr. 4.2) – kao obrana pred sudom drugog stupnja", TarifniBroj = "Tbr. 4.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 2 al. 4 — nagrada kao za obranu pred sudom drugog stupnja (al. 3: toč. 1 + 25%, kod dugotrajnog zatvora + 50%)", KazneniBase = true, KazneniPovecanje = 0.25m, KazneniPovecanjeDugotrajni = 0.50m, SortOrder = 136, Jedinica = "komad" },

        // Tbr. 4. toč. 3 — "U postupku prema maloljetniku i u postupku prema osobama s
        // duševnim smetnjama, odvjetniku kao branitelju pripada nagrada kao za obranu
        // po toč. 1. ovog tarifnog broja."
        new() { Id = "kaz_maloljetnik", Kategorija = "Kazneni postupak", Opis = "Obrana u postupku prema maloljetniku ili osobi s duševnim smetnjama (Tbr. 4.3) – po Tbr. 4.1", TarifniBroj = "Tbr. 4.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 3 — nagrada kao za obranu po toč. 1", KazneniBase = true, SortOrder = 137, Jedinica = "sat" },

        // Tbr. 4. toč. 4 — "Ako je poslije podignute optužnice, optužnog prijedloga ili
        // privatne tužbe, ukidanja obaveznog prekršajnog naloga ili prekršajnog naloga, a
        // prije izvođenja dokaza na raspravi, došlo do obustave postupka, ili do odgode
        // rasprave, odvjetniku pripada 50% nagrade iz toč. 1. ovog tarifnog broja, osim u
        // slučaju ako sud nakon odgode rasprave nastavi s izvanraspravnim izvođenjem dokaza,
        // u kojem slučaju odvjetniku pripada nagrada iz toč. 1."
        new() { Id = "kaz_obustava_odgoda", Kategorija = "Kazneni postupak", Opis = "Obustava postupka ili odgoda rasprave prije izvođenja dokaza (Tbr. 4.4) – 50% Tbr. 4.1; ako sud nastavi s izvanraspravnim izvođenjem dokaza, obračunaj punu obranu po Tbr. 4.1", TarifniBroj = "Tbr. 4.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 4 — 50% nagrade iz toč. 1", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 138, Jedinica = "komad" },

        // Tbr. 4. toč. 5 — "Za svaki obavljeni razgovor s okrivljenikom koji se nalazi u
        // pritvoru ili istražnom zatvoru odvjetniku pripada nagrada od 50 bodova, te za
        // drugi i svaki sljedeći započeti sat nagrada od 50 bodova."
        new() { Id = "kaz_razgovor_pritvor", Kategorija = "Kazneni postupak", Opis = "Razgovor s okrivljenikom u pritvoru ili istražnom zatvoru – prvi (Tbr. 4.5)", TarifniBroj = "Tbr. 4.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 5 — 50 bodova po obavljenom razgovoru", FiksniBody = 50, SortOrder = 139, Jedinica = "komad" },
        new() { Id = "kaz_pritvor_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Razgovor u pritvoru – svaki sljedeći započeti sat (Tbr. 4.5)", TarifniBroj = "Tbr. 4.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 5 — 50 bodova za drugi i svaki sljedeći započeti sat", FiksniBody = 50, SortOrder = 140, Jedinica = "sat" },

        // Tbr. 4. toč. 6 — "Za prisustvovanje očevidu, rekonstrukciji, kao i ostalim radnjama,
        // koje policija, državno odvjetništvo, Ured europskog javnog tužitelja ili sud provode
        // izvan zgrade svog sjedišta, odvjetniku pripada nagrada iz toč. 1. ovog tarifnog broja
        // kao i naknada po Tbr. 49."
        new() { Id = "kaz_ocevid_izvan_zgrade", Kategorija = "Kazneni postupak", Opis = "Očevid / rekonstrukcija / ostale radnje izvan zgrade suda (Tbr. 4.6) – po Tbr. 4.1, uz naknadu putnih troškova po Tbr. 49", TarifniBroj = "Tbr. 4.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 6 — nagrada iz toč. 1 uz naknadu po Tbr. 49 (stvarni putni troškovi, izvan bodovnog sustava)", KazneniBase = true, SortOrder = 141, Jedinica = "komad" },

        // Tbr. 4. toč. 8 — "Odvjetniku koji zastupa oštećenika u postupku koji se vodi pred
        // policijom, državnim odvjetništvom, Uredom europskog javnog tužitelja te sucem istrage,
        // u postupku donošenja odluke o davanju suglasnosti za uvjetni odustanak od kaznenog
        // progona po načelu svrhovitosti, postupku provođenja pojedinačne procjene žrtve te na
        // raspravi pred sudom, pripada 50% nagrade iz ovog tarifnog broja."
        new() { Id = "kaz_zastupanje_ostecenika", Kategorija = "Kazneni postupak", Opis = "Zastupanje oštećenika (uvjetni odustanak, pojedinačna procjena žrtve, rasprava) – 50% Tbr. 4 (Tbr. 4.8)", TarifniBroj = "Tbr. 4.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 8 — 50% nagrade iz Tbr. 4", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 142, Jedinica = "komad" },

        // ── Tbr. 4.1 — Prekršaj s oduzimanjem predmeta ──
        // Ako se uz novčanu kaznu / kaznu zatvora može izreći i kazna oduzimanja predmeta,
        // odvjetniku pripada nagrada po Tbr. 7. toč. 1. ovisno o vrijednosti oduzetog predmeta.
        new() { Id = "kaz_prekrsaj_oduzimanje", Kategorija = "Kazneni postupak", Opis = "Prekršaj s oduzimanjem – nagrada po Tbr. 7.1 prema vrijednosti (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 8 — nagrada po Tbr. 7. toč. 1 prema vrijednosti oduzetog predmeta", PrekrsajOduzimanje = true, SortOrder = 137, Jedinica = "sat" },

        // ── Tbr. 4.1, al. 9 — Obrana pravne osobe ──
        // Za obranu pravne osobe u kaznenom ili prekršajnom postupku,
        // prema visini predviđene kazne za odgovornu osobu = KazneniBase (tier).
        new() { Id = "kaz_obrana_pravna_osoba", Kategorija = "Kazneni postupak", Opis = "Obrana pravne osobe u kazn./prekrš. postupku – po kazni za odg. osobu (Tbr. 4.1 al. 9)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 9 — prema visini kazne za odgovornu osobu", KazneniBase = true, SortOrder = 138, Jedinica = "sat" },

        // ── Tbr. 4.1, al. 10 — Predstavnik pravne osobe (postavi sud) ──
        // Kad odvjetnika u kaznenom postupku za predstavnika pravne osobe postavi sud = 100 bodova.
        new() { Id = "kaz_predstavnik_sud", Kategorija = "Kazneni postupak", Opis = "Predstavnik pravne osobe – postavi sud, 100 bodova (Tbr. 4.1 al. 10)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 10 — 100 bodova kad predstavnika postavi sud", FiksniBody = 100, SortOrder = 139, Jedinica = "sat" },

        // ── Tbr. 4.1, al. 10 — Predstavnik pravne osobe (postavi pravna osoba) ──
        // Kad ga postavi pravna osoba, nagrada prema al. 9 (= tier za odgovornu osobu).
        new() { Id = "kaz_predstavnik_pravna_osoba", Kategorija = "Kazneni postupak", Opis = "Predstavnik pravne osobe – postavi pravna osoba, po kazni za odg. osobu (Tbr. 4.1 al. 10→9)", TarifniBroj = "Tbr. 4.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 4. toč. 1 al. 10 — nagrada po al. 9 kad predstavnika postavi pravna osoba", KazneniBase = true, SortOrder = 140, Jedinica = "sat" },

        // ── Tbr. 5 — Pravni lijekovi ──
        new() { Id = "kaz_zalba_presuda", Kategorija = "Kazneni postupak", Opis = "Žalba protiv presude (Tbr. 5.1) – Tbr. 4.1 + 50%", TarifniBroj = "Tbr. 5.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 1 — nagrada iz Tbr. 4. toč. 1 uvećana za 50%", KazneniBase = true, KazneniPovecanje = 0.5m, SortOrder = 150, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_zalba", Kategorija = "Kazneni postupak", Opis = "Odgovor na žalbu protiv presude (Tbr. 5.2) – 50% od Tbr. 5.1", TarifniBroj = "Tbr. 5.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 2 — 50% nagrade iz Tbr. 5. toč. 1", KazneniBase = true, KazneniPovecanje = -0.25m, SortOrder = 151, Jedinica = "komad" },
        new() { Id = "kaz_zalba_istrazni_zatvor", Kategorija = "Kazneni postupak", Opis = "Žalba protiv rješenja o istražnom zatvoru (Tbr. 5.3) – po Tbr. 4.1", TarifniBroj = "Tbr. 5.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 3 — nagrada iz Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 152, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_zalba_rjesenje", Kategorija = "Kazneni postupak", Opis = "Odgovor na žalbu iz Tbr. 5.3 (Tbr. 5.4) – 50% Tbr. 4.1", TarifniBroj = "Tbr. 5.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 4 — 50% nagrade iz Tbr. 5. toč. 3", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 153, Jedinica = "komad" },
        new() { Id = "kaz_zalba_troskovi", Kategorija = "Kazneni postupak", Opis = "Žalba protiv odluke o trošku (Tbr. 5.5)", TarifniBroj = "Tbr. 5.5", FiksniBody = 50, SortOrder = 154, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_optuznica", Kategorija = "Kazneni postupak", Opis = "Odgovor na optužnicu (Tbr. 5.6) – po Tbr. 4.1", TarifniBroj = "Tbr. 5.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 5. toč. 6 — nagrada iz Tbr. 4. toč. 1", KazneniBase = true, SortOrder = 155, Jedinica = "komad" },

        // ── Tbr. 6 — Izvanredni pravni lijekovi ──
        new() { Id = "kaz_izvanredni_pravni_lijek", Kategorija = "Kazneni postupak", Opis = "Izvanredni pravni lijek (Tbr. 6.1) – po Tbr. 5.1 (Tbr. 4.1 + 50%)", TarifniBroj = "Tbr. 6.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 6. toč. 1 — nagrada po Tbr. 5. toč. 1", KazneniBase = true, KazneniPovecanje = 0.5m, SortOrder = 170, Jedinica = "komad" },
        new() { Id = "kaz_odgovor_izvanredni", Kategorija = "Kazneni postupak", Opis = "Odgovor na izvanredni pravni lijek (Tbr. 6.2) – 50% Tbr. 4.1", TarifniBroj = "Tbr. 6.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 6. toč. 2 — 50% nagrade iz Tbr. 4. toč. 1", KazneniBase = true, KazneniPovecanje = -0.5m, SortOrder = 171, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // II. PARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "par_tuzba", Kategorija = "Parnični postupak", Opis = "Tužba / protutužba / prijedlog (Tbr. 7.1)", TarifniBroj = "Tbr. 7.1", Tbr71Postotak = 1.0m, SortOrder = 200, Jedinica = "komad" },
        // ── Tbr. 7. toč. 2 — jednokratna nagrada od 250 bodova ──
        // "Odvjetniku pripada jednokratna nagrada u visini od 250 bodova, koja obuhvaća
        //  najviše do četiri radnje za svaki prvostupanjski postupak, i to u sljedećim
        //  postupcima: - radi smetanja posjeda, - radi služnosti, - iz stambenih odnosa
        //  (otkazi, iseljenja, utvrđenja), - iz radnih odnosa (poništenje otkaza,
        //  utvrđivanje prava na rad i sl.), OSIM u procjenjivim predmetima na koje se
        //  primjenjuje Tbr. 7. toč. 1.
        //  Za svaku daljnju radnju u navedenim postupcima odvjetniku pripada nagrada
        //  u visini od 50 bodova."
        new() { Id = "par_tuzba_smetanje", Kategorija = "Parnični postupak", Opis = "Tužba – smetanje posjeda / služnost / stambeni / radni spor (Tbr. 7.2) – jednokratno 250 bod. za do 4 radnje; radni sporovi s procjenjivom vrijednošću idu po Tbr. 7. toč. 1", TarifniBroj = "Tbr. 7.2", FiksniBody = 250, JednokratnaNagrada = true, UkljuceneRadnje = 4, DaljnjaRadnjaBodovi = 50, SortOrder = 201, Jedinica = "radnja" },

        // ── Tbr. 7. toč. 3 — jednokratna nagrada od 200 bodova ──
        // "Odvjetniku pripada jednokratna nagrada u visini od 200 bodova, koja obuhvaća
        //  najviše do četiri radnje za svaki prvostupanjski postupak, i to u sljedećim
        //  postupcima: - radi razvoda ili poništenja braka, o postojanju ili nepostojanju
        //  braka, - za utvrđivanje ili osporavanje očinstva, te o tome s kime će živjeti
        //  maloljetna djeca, - za zakonsko uzdržavanje ili dokidanje obveze uzdržavanja.
        //  Za svaku daljnju radnju (...) nagrada u visini od 50 bodova."
        new() { Id = "par_tuzba_obiteljski", Kategorija = "Parnični postupak", Opis = "Tužba – razvod / poništenje braka / očinstvo / s kime žive djeca / uzdržavanje (Tbr. 7.3) – jednokratno 200 bod. za do 4 radnje", TarifniBroj = "Tbr. 7.3", FiksniBody = 200, JednokratnaNagrada = true, UkljuceneRadnje = 4, DaljnjaRadnjaBodovi = 50, SortOrder = 202, Jedinica = "radnja" },
        new() { Id = "par_tuzba_platni_nalog", Kategorija = "Parnični postupak", Opis = "Tužba za platni nalog (Tbr. 7.5) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 7.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 5 — 50% nagrade iz Tbr. 7. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 205, Jedinica = "komad" },

        // ── Tbr. 7. toč. 6 — mjenični, čekovni i drugi vrijednosni papiri ──
        // "Za tužbu na izdavanje mjeničnih, čekovnih i platnih naloga na osnovi drugih
        //  vrijednosnih papira, javnih i privatnih isprava, osim onih koji su navedeni u
        //  toč. 5. ovog tarifnog broja, odvjetniku pripada nagrada iz toč. 1. ovog
        //  tarifnog broja."
        // PUNA nagrada — bez stropa od 100 bodova koji vrijedi za toč. 5.
        new() { Id = "par_tuzba_vrijednosni_papiri", Kategorija = "Parnični postupak", Opis = "Tužba za mjenični / čekovni platni nalog i naloge na osnovi drugih vrijednosnih papira i isprava (Tbr. 7.6) – puna nagrada iz Tbr. 7.1", TarifniBroj = "Tbr. 7.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 6 — nagrada iz Tbr. 7. toč. 1, bez ograničenja iz toč. 5", Tbr71Postotak = 1.0m, SortOrder = 206, Jedinica = "komad" },

        new() { Id = "par_tuzba_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred domaćom arbitražom (Tbr. 7.7)", TarifniBroj = "Tbr. 7.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 207, Jedinica = "komad" },
        new() { Id = "par_tuzba_med_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred međunarodnom arbitražom (Tbr. 7.7) – +100%", TarifniBroj = "Tbr. 7.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 7 — Tbr. 7. toč. 1 s povišenjem od 100%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 1.0m, SortOrder = 208, Jedinica = "komad" },

        // ── Tbr. 7. toč. 9 — parnica vrijednosti manje od 100,00 EUR ──
        // "Za zastupanje u parničnom postupku u kojem je vrijednost predmeta spora manja
        //  od 100,00 eura, neovisno o odredbi Tbr. 7. toč. 1., ukoliko točkama od 2. do 8.
        //  ovog Tbr. za pojedine postupke i sporove nije drugačije određeno, odvjetniku
        //  pripada jednokratna nagrada za cijeli prvostupanjski postupak u visini od 50 bodova."
        // Jednokratno za CIJELI prvostupanjski postupak — ne po radnji.
        new() { Id = "par_mali_spor", Kategorija = "Parnični postupak", Opis = "Zastupanje u prvostupanjskom postupku – VPS manji od 100,00 EUR (Tbr. 7.9) – jednokratno 50 bodova za cijeli postupak", TarifniBroj = "Tbr. 7.9", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 9 — jednokratna nagrada od 50 bodova za cijeli prvostupanjski postupak", FiksniBody = 50, JednokratnaNagrada = true, SortOrder = 209, Jedinica = "postupak" },

        // ── Tbr. 7. toč. 10 — obiteljski sporovi s međunarodnim elementom ──
        // "Zahtjevi kojima se pokreće postupak po Haškoj konvenciji (...) kao i svim drugim
        //  parničnim i izvanparničnim postupcima koji se odnose na obiteljske sporove s
        //  međunarodnim elementom odvjetniku pripada nagrada od 250 bodova."
        new() { Id = "par_zahtjev_haska", Kategorija = "Parnični postupak", Opis = "Zahtjev kojim se pokreće postupak po Haškoj konvenciji / Uredbama Vijeća – obiteljski spor s međunarodnim elementom (Tbr. 7.10) – 250 bodova", TarifniBroj = "Tbr. 7.10", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 7. toč. 10 — nagrada od 250 bodova", FiksniBody = 250, SortOrder = 210, Jedinica = "komad" },
        // ── Tbr. 8. toč. 1 — odgovor na tužbu + prigovor + četiri podneska ──
        // "Za sastavljanje odgovora na tužbu, prigovora protiv platnog naloga i četiri
        //  obrazložena podneska u prvostupanjskom postupku odvjetniku pripada nagrada
        //  kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8."
        // Jedna nagrada pokriva sve navedeno — bez naknade za daljnje radnje, jer je
        // toč. 1 ne propisuje; podnesci izvan toga idu po Tbr. 8. toč. 3 (niže).
        new() { Id = "par_odgovor_tuzba", Kategorija = "Parnični postupak", Opis = "Odgovor na tužbu / prigovor protiv platnog naloga + do četiri obrazložena podneska (Tbr. 8.1) – jedna nagrada kao za tužbu po Tbr. 7.1", TarifniBroj = "Tbr. 8.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 1 — nagrada kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8.", Tbr71Postotak = 1.0m, JednokratnaNagrada = true, SortOrder = 209, Jedinica = "postupak" },

        // ── Tbr. 8. toč. 3 — obrazloženi podnesci IZVAN Tbr. 8. toč. 1 ──
        // "Za sastavljanje obrazloženih podnesaka izvan Tbr. 8. toč. 1. odvjetniku
        //  pripada 50 % nagrade iz Tbr. 7. toč. 1., 4., 7. i 8., ali ne više od 100 bodova."
        new() { Id = "par_obrazlozeni_podnesak", Kategorija = "Parnični postupak", Opis = "Obrazloženi podnesci izvan Tbr. 8.1 (Tbr. 8.3) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 8.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 210, Jedinica = "komad" },
        new() { Id = "par_ostali_podnesci", Kategorija = "Parnični postupak", Opis = "Ostali podnesci (Tbr. 8.4) – 25% Tbr. 7.1, max 25", TarifniBroj = "Tbr. 8.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 4 — 25% nagrade iz Tbr. 7. toč. 1, najviše 25 bodova", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 211, Jedinica = "komad" },

        // ── Tbr. 8. toč. 2 — povrat u prijašnje stanje ──
        // "Za sastavljanje prijedloga za povrat u prijašnje stanje odvjetniku pripada 50%
        //  nagrade iz toč. 1. ovog tarifnog broja, ali ne više od 100 bodova."
        new() { Id = "par_povrat_u_prijasnje_stanje", Kategorija = "Parnični postupak", Opis = "Prijedlog za povrat u prijašnje stanje (Tbr. 8.2) – 50% nagrade iz Tbr. 8.1, max 100", TarifniBroj = "Tbr. 8.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 2 — 50% nagrade iz Tbr. 8. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 212, Jedinica = "komad" },

        // ── Tbr. 8. toč. 5 — obiteljski sporovi s međunarodnim elementom ──
        // "(...) odvjetniku pripada nagrada od 250 bodova, a za ostale podneske tijekom
        //  postupka nagrada od 125 bodova."
        // Apsolutni iznosi — ne ovise o vrijednosti predmeta spora.
        new() { Id = "par_podnesak_haska", Kategorija = "Parnični postupak", Opis = "Obrazloženi podnesak u postupku po Haškoj konvenciji / Uredbama Vijeća (Tbr. 8.5) – 250 bodova", TarifniBroj = "Tbr. 8.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 5 — 250 bodova za obrazloženi podnesak", FiksniBody = 250, SortOrder = 213, Jedinica = "komad" },
        new() { Id = "par_ostali_podnesak_haska", Kategorija = "Parnični postupak", Opis = "Ostali podnesak tijekom postupka po Haškoj konvenciji / Uredbama Vijeća (Tbr. 8.5) – 125 bodova", TarifniBroj = "Tbr. 8.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 8. toč. 5 — 125 bodova za ostale podneske tijekom postupka", FiksniBody = 125, SortOrder = 214, Jedinica = "komad" },

        new() { Id = "par_rociste_glavno", Kategorija = "Parnični postupak", Opis = "Ročište – glavna stvar / dokazi / nagodba (Tbr. 9.1)", TarifniBroj = "Tbr. 9.1", Tbr71Postotak = 1.0m, SortOrder = 220, Jedinica = "komad" },
        new() { Id = "par_rociste_procesno", Kategorija = "Parnični postupak", Opis = "Ročište – procesna pitanja (Tbr. 9.2) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 9.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 2 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 221, Jedinica = "komad" },
        new() { Id = "par_rociste_presuda", Kategorija = "Parnični postupak", Opis = "Ročište – objava presude (Tbr. 9.3) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 222, Jedinica = "komad" },
        new() { Id = "par_pristup_odgodjen", Kategorija = "Parnični postupak", Opis = "Pristup odgođenom ročištu (Tbr. 9.5) – 25% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 5 — 25% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.25m, MaxBodova = 50, SortOrder = 223, Jedinica = "komad" },
        // ── Tbr. 9. toč. 4 — ročišta pred arbitražom ──
        // "Za svako ročište pred arbitražom na kojem se raspravljalo o glavnoj stvari ili
        //  su se izvodili dokazi odvjetniku pripada nagrada iz Tbr. 7. toč. 7., a sva
        //  ostala ročišta 50% nagrade iz Tbr. 7. toč. 7."
        new() { Id = "par_rociste_arbitraza", Kategorija = "Parnični postupak", Opis = "Ročište pred arbitražom – glavna stvar / izvođenje dokaza (Tbr. 9.4)", TarifniBroj = "Tbr. 9.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 4 — nagrada iz Tbr. 7. toč. 7", Tbr71Postotak = 1.0m, SortOrder = 225, Jedinica = "komad" },
        new() { Id = "par_rociste_arbitraza_ostalo", Kategorija = "Parnični postupak", Opis = "Ostala ročišta pred arbitražom (Tbr. 9.4) – 50% nagrade iz Tbr. 7.7", TarifniBroj = "Tbr. 9.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 4 — 50% nagrade iz Tbr. 7. toč. 7", Tbr71Postotak = 0.5m, SortOrder = 226, Jedinica = "komad" },

        // ── Tbr. 9. toč. 6 — rasprava pred sudom drugog stupnja ──
        // "Za raspravu pred sudom drugog stupnja odvjetniku pripada nagrada po
        //  Tbr. 7. toč. 1., 4., 7. i 8."
        new() { Id = "par_rasprava_drugostupanjska", Kategorija = "Parnični postupak", Opis = "Rasprava pred sudom drugog stupnja (Tbr. 9.6) – puna nagrada iz Tbr. 7.1", TarifniBroj = "Tbr. 9.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 6 — nagrada po Tbr. 7. toč. 1., 4., 7. i 8.", Tbr71Postotak = 1.0m, SortOrder = 227, Jedinica = "komad" },

        new() { Id = "par_ocevid", Kategorija = "Parnični postupak", Opis = "Nazočnost očevidu (Tbr. 9.7)", TarifniBroj = "Tbr. 9.7", Tbr71Postotak = 1.0m, SortOrder = 228, Jedinica = "komad" },

        // ── Tbr. 9. toč. 8 — zastupanje u postupcima s međunarodnim elementom ──
        // "(...) odvjetniku pripada nagrada od 250 bodova." Apsolutni iznos.
        new() { Id = "par_zastupanje_haska", Kategorija = "Parnični postupak", Opis = "Zastupanje u postupku po Haškoj konvenciji / Uredbama Vijeća (Tbr. 9.8) – 250 bodova", TarifniBroj = "Tbr. 9.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 9. toč. 8 — 250 bodova", FiksniBody = 250, SortOrder = 229, Jedinica = "komad" },
        new() { Id = "par_zalba_presuda", Kategorija = "Parnični postupak", Opis = "Žalba protiv presude (Tbr. 10.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 10.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 1 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 230, Jedinica = "komad" },
        // ── Tbr. 10. toč. 2 i 3 — pravni lijekovi u parnicama iz Tbr. 7. toč. 2, 3 i 9 ──
        // Toč. 2: "(...) u parnicama iz Tbr. 7. toč. 2. i 3. odvjetniku pripada jednokratna
        //          nagrada u visini od 100 bodova."
        // Toč. 3: "(...) u parnicama iz Tbr. 7. toč. 9. odvjetniku pripada jednokratna
        //          nagrada u visini od 30 bodova."
        new() { Id = "par_zalba_jednokratna", Kategorija = "Parnični postupak", Opis = "Redovni pravni lijek u parnicama iz Tbr. 7.2 i 7.3 (Tbr. 10.2) – jednokratno 100 bodova", TarifniBroj = "Tbr. 10.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 2 — jednokratna nagrada od 100 bodova", FiksniBody = 100, JednokratnaNagrada = true, SortOrder = 231, Jedinica = "postupak" },
        new() { Id = "par_zalba_mali_spor", Kategorija = "Parnični postupak", Opis = "Redovni pravni lijek u parnicama iz Tbr. 7.9 – VPS ispod 100 EUR (Tbr. 10.3) – jednokratno 30 bodova", TarifniBroj = "Tbr. 10.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 3 — jednokratna nagrada od 30 bodova", FiksniBody = 30, JednokratnaNagrada = true, SortOrder = 232, Jedinica = "postupak" },

        new() { Id = "par_zalba_rjesenje", Kategorija = "Parnični postupak", Opis = "Žalba protiv rješenja (Tbr. 10.4) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 10.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 4 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 233, Jedinica = "komad" },
        new() { Id = "par_revizija", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije / izvanredni pravni lijek (Tbr. 10.5) – Tbr. 7.1 + 50%", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 — Tbr. 7. toč. 1 s povišenjem od 50%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.5m, SortOrder = 234, Jedinica = "komad" },

        // Tbr. 10. toč. 5 st. 2 — u predmetima iz Tbr. 7. toč. 2 i 3 nagrada je 150 bodova,
        // a u predmetima iz Tbr. 7. toč. 9 nagrada je 100 bodova.
        new() { Id = "par_revizija_jednokratna", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije u predmetima iz Tbr. 7.2 i 7.3 (Tbr. 10.5 st. 2) – 150 bodova", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 st. 2 — 150 bodova u predmetima iz Tbr. 7. toč. 2 i 3", FiksniBody = 150, SortOrder = 235, Jedinica = "komad" },
        new() { Id = "par_revizija_mali_spor", Kategorija = "Parnični postupak", Opis = "Prijedlog za dopuštenje revizije u predmetima iz Tbr. 7.9 (Tbr. 10.5 st. 2) – 100 bodova", TarifniBroj = "Tbr. 10.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 5 st. 2 — 100 bodova u predmetima iz Tbr. 7. toč. 9", FiksniBody = 100, SortOrder = 236, Jedinica = "komad" },

        new() { Id = "par_odgovor_zalba", Kategorija = "Parnični postupak", Opis = "Odgovor na žalbu / prijedlog za dopuštenje revizije / izvanredni pravni lijek (Tbr. 10.6)", TarifniBroj = "Tbr. 10.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 6 — nagrada po Tbr. 7. toč. 1., 4. i 7.", Tbr71Postotak = 1.0m, SortOrder = 237, Jedinica = "komad" },

        // Tbr. 10. toč. 6 st. 2 — u predmetima iz Tbr. 7. toč. 2, 3 i 9 nagrada je 30 bodova.
        new() { Id = "par_odgovor_zalba_jednokratno", Kategorija = "Parnični postupak", Opis = "Odgovor na žalbu u predmetima iz Tbr. 7.2, 7.3 i 7.9 (Tbr. 10.6 st. 2) – 30 bodova", TarifniBroj = "Tbr. 10.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 10. toč. 6 st. 2 — 30 bodova u predmetima iz Tbr. 7. toč. 2, 3 i 9", FiksniBody = 30, SortOrder = 238, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // III. OVRŠNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ovr_prijedlog", Kategorija = "Ovršni postupak", Opis = "Prijedlog za ovrhu / protuovrhu / osiguranje (Tbr. 11.1)", TarifniBroj = "Tbr. 11.1", Tbr71Postotak = 1.0m, SortOrder = 300, Jedinica = "komad" },
        new() { Id = "ovr_prokazna_izjava", Kategorija = "Ovršni postupak", Opis = "Prijedlog za prokaznu izjavu (Tbr. 11.3) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 3 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 303, Jedinica = "komad" },
        new() { Id = "ovr_odgoda", Kategorija = "Ovršni postupak", Opis = "Prijedlog za odgodu ovrhe (Tbr. 11.4) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 4 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 304, Jedinica = "komad" },
        new() { Id = "ovr_sudska_ovrha", Kategorija = "Ovršni postupak", Opis = "Prijedlog sudu za provedbu sudske ovrhe (Tbr. 11.5) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.5", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 5 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 305, Jedinica = "komad" },
        new() { Id = "ovr_dostava", Kategorija = "Ovršni postupak", Opis = "Dostava ovršne isprave (Tbr. 11.6) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.6", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 6 — 50% nagrade iz Tbr. 11. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 306, Jedinica = "komad" },
        new() { Id = "ovr_prituzba", Kategorija = "Ovršni postupak", Opis = "Pritužba ovršenika (Tbr. 11.7) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.7", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 7 — 50% nagrade iz Tbr. 11. toč. 1, najviše 100 bodova", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 307, Jedinica = "komad" },
        new() { Id = "ovr_zaduznica", Kategorija = "Ovršni postupak", Opis = "Zahtjev za naplatu po zadužnici (Tbr. 11.8) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.8", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 8 — 50% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 308, Jedinica = "komad" },
        new() { Id = "ovr_drazbeni_uvjeti", Kategorija = "Ovršni postupak", Opis = "Dražbeni uvjeti (Tbr. 11.9)", TarifniBroj = "Tbr. 11.9", Tbr71Postotak = 1.0m, SortOrder = 309, Jedinica = "komad" },
        new() { Id = "ovr_ostali_podnesci", Kategorija = "Ovršni postupak", Opis = "Ostali podnesci u ovrsi (Tbr. 11.10) – 25% Tbr. 7.1, max 250", TarifniBroj = "Tbr. 11.10", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 11. toč. 10 — 25% nagrade iz Tbr. 7. toč. 1, najviše 250 bodova", Tbr71Postotak = 0.25m, MaxBodova = 250, SortOrder = 310, Jedinica = "komad" },
        new() { Id = "ovr_rociste", Kategorija = "Ovršni postupak", Opis = "Ročište u ovrsi (Tbr. 12.1)", TarifniBroj = "Tbr. 12.1", Tbr71Postotak = 1.0m, SortOrder = 312, Jedinica = "komad" },
        new() { Id = "ovr_pravni_lijek", Kategorija = "Ovršni postupak", Opis = "Pravni lijek u ovrsi (Tbr. 13.1)", TarifniBroj = "Tbr. 13.1", Tbr71Postotak = 1.0m, SortOrder = 315, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IV. STEČAJNI I PREDSTEČAJNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ste_prijedlog", Kategorija = "Stečajni postupak", Opis = "Prijedlog za pokretanje stečaja/predstečaja (Tbr. 14.1) – Tbr. 7.1, max 1000", TarifniBroj = "Tbr. 14.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 1 — nagrada po Tbr. 7. toč. 1, najviše 1000 bodova", Tbr71Postotak = 1.0m, MaxBodova = 1000, SortOrder = 400, Jedinica = "komad" },
        new() { Id = "ste_prijava_potrazivanja", Kategorija = "Stečajni postupak", Opis = "Prijava potraživanja / obrazloženi podnesak (Tbr. 14.2) – Tbr. 7.1, max 2000", TarifniBroj = "Tbr. 14.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 2 — nagrada po Tbr. 7. toč. 1, najviše 2000 bodova", Tbr71Postotak = 1.0m, MaxBodova = 2000, SortOrder = 401, Jedinica = "komad" },
        new() { Id = "ste_pravni_lijek", Kategorija = "Stečajni postupak", Opis = "Pravni lijek u stečaju (Tbr. 14.3) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 14.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 14. toč. 3 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 402, Jedinica = "komad" },
        new() { Id = "ste_rociste", Kategorija = "Stečajni postupak", Opis = "Ročište / sjednica vjerovnika (Tbr. 15)", TarifniBroj = "Tbr. 15", Tbr71Postotak = 1.0m, SortOrder = 405, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // V. IZVANPARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "izv_dioba", Kategorija = "Izvanparnični postupak", Opis = "Pokretanje postupka – dioba / ostavina / procjenjivi (Tbr. 16.1) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 16.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 16. toč. 1 — 50% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 500, Jedinica = "komad" },
        new() { Id = "izv_meda", Kategorija = "Izvanparnični postupak", Opis = "Uređenje međe (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 501, Jedinica = "komad" },
        new() { Id = "izv_neprocjenjivi", Kategorija = "Izvanparnični postupak", Opis = "Ostali neprocjenjivi izvanparnični (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 502, Jedinica = "komad" },
        new() { Id = "izv_amortizacija", Kategorija = "Izvanparnični postupak", Opis = "Amortizacija isprava (Tbr. 16.1) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 16.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 16. toč. 1 — 50% nagrade iz Tbr. 7. toč. 1, najviše 50 bodova", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 503, Jedinica = "komad" },
        new() { Id = "izv_rociste", Kategorija = "Izvanparnični postupak", Opis = "Ročište u izvanparnici – procjenjivo (Tbr. 17.1) – 75% Tbr. 7.1", TarifniBroj = "Tbr. 17.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 17. toč. 1 — 75% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.75m, SortOrder = 510, Jedinica = "komad" },
        new() { Id = "izv_pravni_lijek", Kategorija = "Izvanparnični postupak", Opis = "Pravni lijek u izvanparnici (Tbr. 18.1) – Tbr. 16 + 25%", TarifniBroj = "Tbr. 18.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 18. toč. 1 — nagrada iz Tbr. 16 s povišenjem od 25%", Tbr71Postotak = 0.5m, Tbr71Povecanje = 0.25m, SortOrder = 515, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VI. ZEMLJIŠNOKNJIŽNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "zk_redoviti", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – redoviti postupak (Tbr. 19.1) – 25% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 19.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 19. toč. 1 — 25% nagrade iz Tbr. 7. toč. 1, najviše 500 bodova", Tbr71Postotak = 0.25m, MaxBodova = 500, SortOrder = 600, Jedinica = "komad" },
        new() { Id = "zk_redoviti_neproc", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – neprocjenjiv (Tbr. 19.2)", TarifniBroj = "Tbr. 19.2", FiksniBody = 50, SortOrder = 601, Jedinica = "komad" },
        new() { Id = "zk_ostali", Kategorija = "Zemljišnoknjižni postupak", Opis = "Ostali podnesci u ZK postupku (Tbr. 19.3)", TarifniBroj = "Tbr. 19.3", FiksniBody = 50, SortOrder = 602, Jedinica = "komad" },
        new() { Id = "zk_posebni", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – posebni postupak (Tbr. 20.1) – 25% Tbr. 7.1", TarifniBroj = "Tbr. 20.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 20. toč. 1 — 25% nagrade iz Tbr. 7. toč. 1", Tbr71Postotak = 0.25m, SortOrder = 605, Jedinica = "komad" },
        new() { Id = "zk_pravni_lijek", Kategorija = "Zemljišnoknjižni postupak", Opis = "Pravni lijek u ZK postupku (Tbr. 21) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 21", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 21 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 610, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VII. UPRAVNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "upr_neprocjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – neprocjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", FiksniBody = 50, SortOrder = 700, Jedinica = "komad" },
        new() { Id = "upr_procjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – procjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 22. toč. 1 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 701, Jedinica = "komad" },
        new() { Id = "upr_ostali", Kategorija = "Upravni postupak", Opis = "Ostali podnesci u upravnom postupku (Tbr. 22.2) – 25%, max 25", TarifniBroj = "Tbr. 22.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 22. toč. 2 — 25% nagrade iz Tbr. 7. toč. 1, najviše 25 bodova", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 702, Jedinica = "komad" },
        new() { Id = "upr_zalba", Kategorija = "Upravni postupak", Opis = "Žalba / prigovor / drug. instanca (Tbr. 24) – Tbr. 22 + 25%", TarifniBroj = "Tbr. 24", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 24 — nagrada iz Tbr. 22 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 710, Jedinica = "komad" },

        // Tbr. 23. toč. 1: "Za zastupanje u ispitnom postupku, u postupku osiguranja
        // dokaza, na usmenoj raspravi te očevidu u prvostupanjskom upravnom postupku
        // odvjetniku pripada nagrada iz Tbr. 22. te naknada po Tbr. 39."
        // Toč. 2: "Za obranu u postupcima radi poreznih, carinskih ili deviznih prekršaja
        // odvjetniku pripada nagrada po Tbr. 22., te naknada po Tbr. 39."
        //
        // Tbr. 22. toč. 1 ima dvije alineje (neprocjenjivo 50 bodova, procjenjivo po
        // Tbr. 7. toč. 1), pa svaka točka Tbr. 23 daje dvije stavke — isti obrazac kao
        // upr_neprocjenjiv / upr_procjenjiv gore.
        //
        // "naknada po Tbr. 39" se NE dodaje ovdje: Tbr. 39 je već zasebna stavka
        // zaj_cekanje i obračunava se odvojeno, po započetom satu.
        //
        // Toč. 2 govori o prekršajima, ali Tarifa je smješta u glavu VII. UPRAVNI POSTUPAK
        // i nagradu veže uz Tbr. 22, ne uz Tbr. 4. Kategorija mora ostati "Upravni
        // postupak" — u kategoriji "Kazneni postupak" tiho bi ugasila sudsku pristojbu.
        new() { Id = "upr_zastupanje_neproc", Kategorija = "Upravni postupak", Opis = "Zastupanje u ispitnom postupku / na raspravi / očevidu – neprocjenjiv (Tbr. 23.1); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 1 — nagrada iz Tbr. 22. toč. 1 al. 1, 50 bodova", FiksniBody = 50, SortOrder = 720, Jedinica = "komad" },
        new() { Id = "upr_zastupanje_proc", Kategorija = "Upravni postupak", Opis = "Zastupanje u ispitnom postupku / na raspravi / očevidu – procjenjiv (Tbr. 23.1); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 1 — nagrada iz Tbr. 22. toč. 1 al. 2 (Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 721, Jedinica = "komad" },
        new() { Id = "upr_obrana_prekrsaj_neproc", Kategorija = "Upravni postupak", Opis = "Obrana u poreznom / carinskom / deviznom prekršaju – neprocjenjiv (Tbr. 23.2); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 2 — nagrada po Tbr. 22. toč. 1 al. 1, 50 bodova", FiksniBody = 50, SortOrder = 722, Jedinica = "komad" },
        new() { Id = "upr_obrana_prekrsaj_proc", Kategorija = "Upravni postupak", Opis = "Obrana u poreznom / carinskom / deviznom prekršaju – procjenjiv (Tbr. 23.2); Tbr. 39 se obračunava zasebno", TarifniBroj = "Tbr. 23.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 23. toč. 2 — nagrada po Tbr. 22. toč. 1 al. 2 (Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 723, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VIII. UPRAVNI SPOR
        // ═══════════════════════════════════════════════════════════
        // Tbr. 27. toč. 1: "— u procjenjivim predmetima odvjetniku pripada nagrada iz
        // Tbr. 7. toč. 1., — u neprocjenjivim predmetima odvjetniku pripada nagrada od
        // 500 bodova." Toč. 2, 3 i 4 sve upućuju na toč. 1, pa svaka od njih ima
        // procjenjivu i neprocjenjivu varijantu.
        new() { Id = "usp_tuzba_proc", Kategorija = "Upravni spor", Opis = "Tužba / odgovor na tužbu / obrazloženi podnesak – procjenjiv (Tbr. 27.1)", TarifniBroj = "Tbr. 27.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 1 al. 1 — nagrada iz Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 800, Jedinica = "komad" },
        new() { Id = "usp_tuzba_neproc", Kategorija = "Upravni spor", Opis = "Tužba / odgovor na tužbu / obrazloženi podnesak – neprocjenjiv (Tbr. 27.1) – 500 bodova", TarifniBroj = "Tbr. 27.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 1 al. 2 — 500 bodova u neprocjenjivim predmetima", FiksniBody = 500, SortOrder = 801, Jedinica = "komad" },

        // Tbr. 27. toč. 2: "Za zastupanje na raspravi odvjetniku pripada nagrada iz
        // toč. 1. ovog tarifnog broja."
        new() { Id = "usp_rasprava", Kategorija = "Upravni spor", Opis = "Zastupanje na raspravi – procjenjiv (Tbr. 27.2)", TarifniBroj = "Tbr. 27.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 2 — nagrada iz Tbr. 27. toč. 1 (procjenjivo: Tbr. 7. toč. 1)", Tbr71Postotak = 1.0m, SortOrder = 802, Jedinica = "komad" },
        new() { Id = "usp_rasprava_neproc", Kategorija = "Upravni spor", Opis = "Zastupanje na raspravi – neprocjenjiv (Tbr. 27.2 → 27.1) – 500 bodova", TarifniBroj = "Tbr. 27.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 2 — nagrada iz Tbr. 27. toč. 1 al. 2, 500 bodova", FiksniBody = 500, SortOrder = 803, Jedinica = "komad" },

        // Tbr. 27. toč. 3: "(...) pripada nagrada iz toč. 1. ovog tarifnog broja
        // uvećana za 25%."  Neprocjenjivo: 500 × 1,25 = 625 bodova.
        new() { Id = "usp_zalba", Kategorija = "Upravni spor", Opis = "Žalba / zahtjev VUS – procjenjiv (Tbr. 27.3) – Tbr. 27.1 + 25%", TarifniBroj = "Tbr. 27.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 3 — nagrada iz Tbr. 27. toč. 1 uvećana za 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 805, Jedinica = "komad" },
        new() { Id = "usp_zalba_neproc", Kategorija = "Upravni spor", Opis = "Žalba / zahtjev VUS – neprocjenjiv (Tbr. 27.3) – 500 + 25% = 625 bodova", TarifniBroj = "Tbr. 27.3", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 3 — 500 bodova iz Tbr. 27. toč. 1 al. 2 uvećano za 25% = 625", FiksniBody = 625, SortOrder = 806, Jedinica = "komad" },

        // Tbr. 27. toč. 4: "(...) pripada 50% nagrade iz toč. 1. ovog tarifnog broja."
        // Neprocjenjivo: 50% od 500 = 250 bodova.
        new() { Id = "usp_izvrsenje", Kategorija = "Upravni spor", Opis = "Zahtjev za izvršenje presude – procjenjiv (Tbr. 27.4) – 50% Tbr. 27.1", TarifniBroj = "Tbr. 27.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 4 — 50% nagrade iz Tbr. 27. toč. 1", Tbr71Postotak = 0.5m, SortOrder = 807, Jedinica = "komad" },
        new() { Id = "usp_izvrsenje_neproc", Kategorija = "Upravni spor", Opis = "Zahtjev za izvršenje presude – neprocjenjiv (Tbr. 27.4) – 50% od 500 = 250 bodova", TarifniBroj = "Tbr. 27.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 27. toč. 4 — 50% od 500 bodova iz Tbr. 27. toč. 1 al. 2 = 250", FiksniBody = 250, SortOrder = 808, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IX. REGISTARSKI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "reg_upis", Kategorija = "Registarski postupak", Opis = "Podnesak za upis / promjenu u registru (Tbr. 28.1)", TarifniBroj = "Tbr. 28.1", FiksniBody = 50, SortOrder = 900, Jedinica = "komad" },

        // Tbr. 28. toč. 2: "Za sastavljanje isprava (izjava o osnivanju društva, društveni
        // ugovor, statut, te ostale isprave) potrebnih za upis osnivanja ili promjena u
        // registru, odvjetniku pripada nagrada prema Tbr. 33. toč. 1. i 2."
        //
        // Tbr. 33. toč. 1: "nagrada iz Tbr. 7. toč. 1. uvećana za 25%."
        // Tbr. 33. toč. 2 al. 3: "ako u ugovoru nije naznačena vrijednost ugovora
        // odvjetniku pripada nagrada od 200 bodova."
        // Isti par kakav već postoji za raz_ugovor / raz_ugovor_neproc.
        new() { Id = "reg_isprave_proc", Kategorija = "Registarski postupak", Opis = "Osnivačke isprave za upis u registar – naznačena vrijednost (Tbr. 28.2) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 28.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 2 — nagrada prema Tbr. 33. toč. 1 (Tbr. 7. toč. 1 uvećan za 25%)", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 901, Jedinica = "komad" },
        new() { Id = "reg_isprave_neproc", Kategorija = "Registarski postupak", Opis = "Osnivačke isprave za upis u registar – bez naznačene vrijednosti (Tbr. 28.2) – 200 bodova", TarifniBroj = "Tbr. 28.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 2 — nagrada prema Tbr. 33. toč. 2 al. 3, 200 bodova", FiksniBody = 200, SortOrder = 902, Jedinica = "komad" },

        new() { Id = "reg_ostali", Kategorija = "Registarski postupak", Opis = "Ostali podnesci za upis (Tbr. 28.3)", TarifniBroj = "Tbr. 28.3", FiksniBody = 50, SortOrder = 903, Jedinica = "komad" },
        new() { Id = "reg_pravni_lijek", Kategorija = "Registarski postupak", Opis = "Pravni lijek u registarskom postupku (Tbr. 28.4) – 50 + 50%", TarifniBroj = "Tbr. 28.4", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 28. toč. 4 — nagrada iz Tbr. 28. toč. 1 uvećana za 50%", FiksniBody = 75, SortOrder = 904, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // X. ZAŠTITA PRAVA PRED POSLODAVCEM
        // ═══════════════════════════════════════════════════════════
        new() { Id = "pos_obrazlozeni", Kategorija = "Zaštita pred poslodavcem", Opis = "Obrazloženi podnesci (Tbr. 30.1)", TarifniBroj = "Tbr. 30.1", FiksniBody = 50, SortOrder = 1000, Jedinica = "komad" },
        new() { Id = "pos_zahtjev", Kategorija = "Zaštita pred poslodavcem", Opis = "Zahtjev za zaštitu prava (Tbr. 30.2)", TarifniBroj = "Tbr. 30.2", FiksniBody = 50, SortOrder = 1001, Jedinica = "komad" },
        new() { Id = "pos_nacrt_odluke", Kategorija = "Zaštita pred poslodavcem", Opis = "Nacrt odluke za poslodavca (Tbr. 30.3)", TarifniBroj = "Tbr. 30.3", FiksniBody = 100, SortOrder = 1002, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XI. USTAVNI SUD / ECHR / SUD EU
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ust_pokretanje", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Pokretanje postupka pred Ustavnim sudom / ECHR / Sud EU (Tbr. 31.1)", TarifniBroj = "Tbr. 31.1", FiksniBody = 1000, SortOrder = 1100, Jedinica = "komad" },
        new() { Id = "ust_ostali_podnesci", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Ostali podnesci (Tbr. 31.2)", TarifniBroj = "Tbr. 31.2", FiksniBody = 50, SortOrder = 1101, Jedinica = "komad" },
        new() { Id = "ust_rasprava", Kategorija = "Ustavni sud / ECHR / Sud EU", Opis = "Zastupanje na raspravi (Tbr. 31.3)", TarifniBroj = "Tbr. 31.3", FiksniBody = 1000, SortOrder = 1102, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XII. OSTALI POSTUPCI
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ost_naknada_stete", Kategorija = "Ostali postupci", Opis = "Zahtjev za naknadu štete prema državi / osiguranju (Tbr. 32.1)", TarifniBroj = "Tbr. 32.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 32. toč. 1 — nagrada po Tbr. 7. toč. 1", Tbr71Postotak = 1.0m, SortOrder = 1200, Jedinica = "komad" },
        new() { Id = "ost_stegovni", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – podnesci / pravni lijekovi (Tbr. 32.2)", TarifniBroj = "Tbr. 32.2", FiksniBody = 100, SortOrder = 1201, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XIII. RAZNO – ISPRAVE, SAVJETI, OPOMENE
        // ═══════════════════════════════════════════════════════════
        new() { Id = "raz_ugovor", Kategorija = "Razno", Opis = "Sastavljanje ugovora (Tbr. 33.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 33.1", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 33. toč. 1 — Tbr. 7. toč. 1 s povišenjem od 25%", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 1300, Jedinica = "komad" },
        new() { Id = "raz_ugovor_neproc", Kategorija = "Razno", Opis = "Ugovor – neprocjenjiv (Tbr. 33.2)", TarifniBroj = "Tbr. 33.2", FiksniBody = 200, SortOrder = 1301, Jedinica = "komad" },
        new() { Id = "raz_izjava", Kategorija = "Razno", Opis = "Jednostavna izjava volje (Tbr. 33.3)", TarifniBroj = "Tbr. 33.3", FiksniBody = 50, SortOrder = 1302, Jedinica = "komad" },
        new() { Id = "raz_oporuka", Kategorija = "Razno", Opis = "Sastavljanje oporuke (Tbr. 33.4)", TarifniBroj = "Tbr. 33.4", FiksniBody = 100, SortOrder = 1303, Jedinica = "komad" },
        new() { Id = "raz_usmeni_savjet", Kategorija = "Razno", Opis = "Usmeni pravni savjet (Tbr. 34.1) – 50 bodova/sat", TarifniBroj = "Tbr. 34.1", FiksniBody = 50, SortOrder = 1310, Jedinica = "sat" },
        // Tbr. 34. toč. 2, odlomak 1: "Za sastavljanje pisanih pravnih savjeta i pisanih
        // pravnih mišljenja s činjeničnim i pravnim obrazloženjima odvjetniku pripada
        // nagrada iz Tbr. 7. toč. 1. i 4." — bez minimuma.
        new() { Id = "raz_pisani_savjet", Kategorija = "Razno", Opis = "Pisani pravni savjet / mišljenje – procjenjiv predmet (Tbr. 34.2)", TarifniBroj = "Tbr. 34.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 34. toč. 2 odl. 1 — nagrada iz Tbr. 7. toč. 1. i 4.", Tbr71Postotak = 1.0m, SortOrder = 1311, Jedinica = "komad" },

        // Tbr. 34. toč. 2, odlomak 2: "(...) u neprocjenjivim predmetima odvjetniku
        // pripada nagrada ne manja od 200 bodova." Minimum vrijedi SAMO ovdje.
        new() { Id = "raz_pisani_savjet_neproc", Kategorija = "Razno", Opis = "Pisani pravni savjet / mišljenje – neprocjenjiv predmet (Tbr. 34.2) – najmanje 200 bodova", TarifniBroj = "Tbr. 34.2", PravniIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 34. toč. 2 odl. 2 — u neprocjenjivim predmetima ne manje od 200 bodova", FiksniBody = 200, MinBodova = 200, SortOrder = 1312, Jedinica = "komad" },
        new() { Id = "raz_konferencija", Kategorija = "Razno", Opis = "Konferencija / sastanak (Tbr. 35) – 50 bodova/sat", TarifniBroj = "Tbr. 35", FiksniBody = 50, SortOrder = 1320, Jedinica = "sat" },
        new() { Id = "raz_razmatranje_spisa", Kategorija = "Razno", Opis = "Razmatranje spisa / uvid u ZK / registre (Tbr. 36) – 25 bodova/sat", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1330, Jedinica = "sat" },
        new() { Id = "raz_izvid_pravomocnost", Kategorija = "Razno", Opis = "Izvid pravomoćnosti / izvršnosti (Tbr. 36)", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1331, Jedinica = "komad" },
        new() { Id = "raz_pribava_izvadaka", Kategorija = "Razno", Opis = "Pribava izvadaka iz ZK / registara (Tbr. 36)", TarifniBroj = "Tbr. 36", FiksniBody = 25, SortOrder = 1332, Jedinica = "komad" },
        new() { Id = "raz_opomena", Kategorija = "Razno", Opis = "Obavijest / opomena s činjeničnim stanjem (Tbr. 37.1) – 25 bod/str.", TarifniBroj = "Tbr. 37.1", FiksniBody = 25, SortOrder = 1340, Jedinica = "stranica" },
        new() { Id = "raz_tipizirana_opomena", Kategorija = "Razno", Opis = "Tipizirana opomena (Tbr. 37.2)", TarifniBroj = "Tbr. 37.2", FiksniBody = 10, SortOrder = 1341, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XIV. ZAJEDNIČKE ODREDBE
        // ═══════════════════════════════════════════════════════════
        new() { Id = "zaj_cekanje", Kategorija = "Zajedničke odredbe", Opis = "Čekanje na raspravu / putovanje (Tbr. 39) – 25 bod/sat, max 7h", TarifniBroj = "Tbr. 39", FiksniBody = 25, SortOrder = 1400, Jedinica = "sat" },
        // Tbr. 40 (zastupanje više osoba) NIJE stavka kataloga nego povišenje cijele
        // osnovice — vidi IzracunajPovisenjeViseOsoba i polje broj_zastupanih_osoba.
    ];

    // ── Calculate a troškovnik ────────────────────────────────────

    /// <summary>Naziv kategorije kaznenog i prekršajnog postupka u katalogu.</summary>
    public const string KategorijaKazneni = "Kazneni postupak";

    /// <summary>Naziv kategorije parničnog postupka u katalogu.</summary>
    public const string KategorijaParnicni = "Parnični postupak";

    /// <summary>Naziv kategorije ovršnog postupka u katalogu.</summary>
    public const string KategorijaOvrsni = "Ovršni postupak";

    /// <summary>
    /// Određuje osnovu za obračun sudske pristojbe iz kategorija odabranih stavki.
    ///
    /// Uredba o tarifi sudskih pristojbi (NN 37/2023), Prilog, POGLAVLJE I.
    /// "PARNIČNI I OVRŠNI POSTUPAK":
    ///  - Tar. br. 1 st. 1 — puna pristojba na tužbu,
    ///  - Tar. br. 1 st. 2 — "prijedlog za ovrhu, protuovrhu i osiguranje (...) žalbu,
    ///    prijedlog za dopuštenje revizije i reviziju plaća se polovica pristojbe iz
    ///    stavka 1. ovoga Tar. br.",
    ///  - kazneni postupak u Uredbi ne postoji → nema pristojbe.
    /// </summary>
    private static PristojbaOsnova OdrediPristojbuOsnovu(List<TarifnaStavka> odabrane)
    {
        if (odabrane.Count == 0)
            return PristojbaOsnova.Puna;

        if (odabrane.TrueForAll(d => d.Kategorija == KategorijaKazneni))
            return PristojbaOsnova.Nema;

        // Čisti ovršni postupak: ima ovršnih stavki, a nijedne parnične.
        var imaOvrsnih = odabrane.Exists(d => d.Kategorija == KategorijaOvrsni);
        var imaParnicnih = odabrane.Exists(d => d.Kategorija == KategorijaParnicni);

        if (imaOvrsnih && !imaParnicnih)
            return PristojbaOsnova.Polovica;

        return PristojbaOsnova.Puna;
    }

    public TroskovnikResponse Izracunaj(TroskovnikRequest request)
    {
        var catalogMap = _katalog.ToDictionary(s => s.Id);
        var lines = new List<TroskovnikLineItem>();
        var rbr = 0;

        // ── Vrsta postupka ──────────────────────────────────────────
        // U kaznenom se postupku sve veže na zapriječenu kaznu (Tbr. 4. toč. 1),
        // nikad na vrijednost predmeta spora. VPS je institut parničnog postupka,
        // pa se u kaznenom toku ne koristi ni za bodove ni za sudsku pristojbu.
        // Nepoznata stavka i nevaljana količina odbijaju se odmah — prije se takva
        // stavka tiho preskakala i nestajala iz troškovnika bez ijedne poruke.
        var odabraneDefinicije = new List<TarifnaStavka>();
        foreach (var stavka in request.Stavke)
        {
            if (!catalogMap.TryGetValue(stavka.ItemId, out var def))
                throw new TroskovnikValidationException(
                    $"Nepoznata tarifna stavka: \"{stavka.ItemId}\". Nije pronađena u katalogu.");

            if (stavka.Kolicina <= 0)
                throw new TroskovnikValidationException(
                    $"Stavka \"{def.Opis}\" ima količinu {stavka.Kolicina}. Količina mora biti veća od 0.");

            odabraneDefinicije.Add(def);
        }

        var isSamoKazneni = odabraneDefinicije.Count > 0
            && odabraneDefinicije.TrueForAll(d => d.Kategorija == KategorijaKazneni);

        // Jedina iznimka je Tbr. 4. toč. 1 al. 8 (prekršaj s oduzimanjem), koji ide po
        // Tbr. 7. toč. 1 prema vrijednosti ODUZETOG PREDMETA — to se računa zasebno niže.
        var vrijednostSpora = isSamoKazneni ? 0m : EfektivniVps(request);

        // Osnovica na koju upućuju Tbr. 8. toč. 1, 3, 4 i Tbr. 9. toč. 1–6:
        // "Tbr. 7. toč. 1., 4., 7. i 8." — četiri varijante iste osnovice, ne četiri iznosa.
        // U kaznenom postupku vrijednost predmeta spora ne postoji, pa se osnovica ne razrješava.
        var osnovica = isSamoKazneni
            ? new OsnovicaRezultat(Tbr71Bodovi(0m), "", false)
            : IzracunajOsnovicu(request);

        // ── Zapriječena kazna (Tbr. 4. toč. 1) ──
        // Kaznene stavke nemaju osnovicu bez nje, pa se traži samo kad je stvarno treba.
        var trebaKazneniTier = odabraneDefinicije.Exists(d => d.KazneniBase);
        int kazneniTierBodovi = 0;

        if (trebaKazneniTier)
        {
            if (string.IsNullOrWhiteSpace(request.ZaprijecenaKazna))
                throw new TroskovnikValidationException(
                    "Odabrane su stavke iz kaznenog postupka, a nije odabrana zapriječena kazna. " +
                    "Tbr. 4. toč. 1 nagradu veže isključivo uz visinu zapriječene kazne.");

            if (!ZaprijecenaKaznaTiers.Bodovi.TryGetValue(request.ZaprijecenaKazna, out var tierPts))
                throw new TroskovnikValidationException(
                    $"Nepoznata zapriječena kazna: \"{request.ZaprijecenaKazna}\". " +
                    $"Dopuštene vrijednosti: {string.Join(", ", ZaprijecenaKaznaTiers.Bodovi.Keys)}.");

            kazneniTierBodovi = tierPts;
        }

        foreach (var stavka in request.Stavke)
        {
            var def = catalogMap[stavka.ItemId];

            decimal bodovi;
            if (def.PrekrsajOduzimanje)
            {
                if (request.IsOduzimanjePrekrsaj)
                {
                    // Tbr. 4. toč. 1 al. 8 — prekršaj s oduzimanjem ide po Tbr. 7. toč. 1
                    // prema vrijednosti oduzetog predmeta odnosno imovinske koristi.
                    if (request.VrijednostOduzimanja <= 0)
                        throw new TroskovnikValidationException(
                            "Uključeno je oduzimanje predmeta, a nije unesena vrijednost oduzetog " +
                            "predmeta odnosno imovinske koristi. Tbr. 4. toč. 1 al. 8 nagradu veže " +
                            "uz tu vrijednost po Tbr. 7. toč. 1.");

                    bodovi = Tbr71Bodovi(request.VrijednostOduzimanja);
                }
                else
                {
                    // Tbr. 4. toč. 1 al. 7 — "za obranu u prekršajnom postupku 100 bodova".
                    bodovi = 100m;
                }
            }
            else if (def.KazneniBase)
            {
                bodovi = IzracunajKazneneBodove(def, kazneniTierBodovi, request);
            }
            else
            {
                bodovi = IzracunajBodove(def, osnovica);
            }

            // Zaštitna mreža: nijedna stavka ne smije završiti na nula bodova.
            // Nula je u troškovniku neupotrebljiva — izgleda kao valjan iznos, a nije.
            if (bodovi <= 0)
                throw new TroskovnikValidationException(
                    $"Stavka \"{def.Opis}\" ({def.TarifniBroj}) daje 0 bodova. " +
                    "Provjerite unesene parametre predmeta.");

            rbr++;

            var ukupnoStavke = UkupnoBodovaZaStavku(def, bodovi, stavka.Kolicina);

            // ── Tbr. 41 po POJEDINOJ stavci ──
            // Toč. 2 kaže "POJEDINE tarifne stavke odvjetnik može sniziti za 50%" —
            // riječ "pojedine" stoji samo ondje, pa se prilagodba veže uz stavku.
            var (tbr41Povisenje, tbr41Snizenje) = RazrijesiTbr41(stavka, request, def);
            var tbr41BodoviStavke = ukupnoStavke * Tbr41Udio(tbr41Povisenje, tbr41Snizenje);

            lines.Add(new TroskovnikLineItem
            {
                Rbr = rbr,
                ItemId = def.Id,
                Opis = def.Opis,
                TarifniBroj = def.TarifniBroj,
                PravniIzvor = def.PravniIzvor,
                Kolicina = stavka.Kolicina,
                BodovaPoJedinici = bodovi,
                UkupnoBodova = ukupnoStavke,
                // Redak se prikazuje u eurima, pa se ovdje zaokružuje. Zbrojevi niže
                // računaju se iz nezaokruženih bodova, da se greška ne akumulira.
                IznosEur = ZaokruziNovac(ukupnoStavke * TarifaConstants.BodValue),
                IsJednokratna = def.JednokratnaNagrada,
                Tbr41PovisenjePostotak = tbr41Povisenje,
                Tbr41SnizenjePostotak = tbr41Snizenje,
                Tbr41Bodovi = tbr41BodoviStavke,
                Tbr41PravniIzvor = Tbr41Izvor(tbr41Povisenje, tbr41Snizenje)
            });
        }

        var osnovicaBodova = lines.Sum(l => l.UkupnoBodova);

        // ── Tbr. 40. toč. 1 — zastupanje više osoba ──
        var povisenjePostotak = PovisenjeViseOsobaPostotak(request.BrojZastupanihOsoba);
        var povisenjeBodovi = osnovicaBodova * povisenjePostotak;

        // ── Tbr. 41 — zbroj prilagodbi po stavkama ──
        // Doprinos je ADITIVAN na ukupan iznos, kao i Tbr. 40, iako se računa po stavci.
        // Da je umjesto toga multiplikativan (osnovica' = Σ stavka × (1 + udio)), Tbr. 40
        // bi se složio na već prilagođenu osnovicu i ukupan bi se iznos promijenio —
        // vidi TarifaTbr41PoStavciTests.RegresijaSaTbr40_OstajeAditivna.
        // Sudska pristojba se ne dira jer nije odvjetnička nagrada.
        var tbr41Bodovi = lines.Sum(l => l.Tbr41Bodovi);

        var ukupnoBodova = osnovicaBodova + povisenjeBodovi + tbr41Bodovi;

        // ── Novac ──
        // Bodovi su do ovdje ostali decimalni, bez ijednog zaokruživanja. Tek se
        // gotov iznos zaokružuje, jednom, na dvije decimale.
        var ukupnoEur = ZaokruziNovac(ukupnoBodova * TarifaConstants.BodValue);

        // ── Tbr. 40. toč. 1, drugi odlomak ──
        // "Iznos povišene nagrade dijeli se na jednake dijelove prema broju branjenika,
        //  odnosno zastupanih osoba."
        // Dioba ne mijenja ukupnu nagradu — pokazuje koliko od nje otpada na svaku osobu.
        // Dijeli se NAGRADA; PDV i sudska pristojba nisu odvjetnička nagrada pa ne ulaze.
        var brojOsoba = Math.Max(1, request.BrojZastupanihOsoba);
        var bodoviPoOsobi = ukupnoBodova / brojOsoba;
        var nagradaPoOsobi = ZaokruziNovac(ukupnoEur / brojOsoba);

        decimal pdv = 0;
        var useVat = request.IsPdvUkljucen ?? request.HasVat;
        if (useVat)
            pdv = ZaokruziNovac(ukupnoEur * TarifaConstants.PdvRate);

        // ── Sudska pristojba (Uredba o tarifi sudskih pristojbi, NN 37/2023) ──
        //
        var pristojbaOsnova = OdrediPristojbuOsnovu(odabraneDefinicije);

        var sudskaPristojba = request.IsOslobodjenPristojbi
            ? 0m
            : SudskePristojbeCalculator.Izracunaj(vrijednostSpora, pristojbaOsnova);
        var odvjetnickiTrosak = ukupnoEur + pdv;
        var sveukupniTrosak = odvjetnickiTrosak + sudskaPristojba;

        return new TroskovnikResponse
        {
            Stavke = lines,
            UkupnoBodova = ukupnoBodova,
            BrojZastupanihOsoba = Math.Max(1, request.BrojZastupanihOsoba),
            PovisenjeViseOsobaPostotak = povisenjePostotak,
            OsnovicaTbr7 = request.OsnovicaTbr7,
            OsnovicaBodovi = osnovica.Bodovi,
            OsnovicaPravniIzvor = osnovica.PravniIzvor,
            RazinaSuda = request.RazinaSuda,
            Tbr45Nacin = request.Tbr45Nacin,
            Tbr41PovisenjePostotak = Math.Clamp(request.Tbr41PovisenjePostotak, 0m, 100m),
            Tbr41SnizenjePostotak = Math.Clamp(request.Tbr41SnizenjePostotak, 0m, 50m),
            Tbr41Bodovi = tbr41Bodovi,
            PovisenjeViseOsobaBodovi = povisenjeBodovi,
            BodoviPoOsobi = bodoviPoOsobi,
            NagradaPoOsobi = nagradaPoOsobi,
            VrijednostSpora = vrijednostSpora,
            IsSamoKazneni = isSamoKazneni,
            UkupnoEurBezPdv = ukupnoEur,
            Pdv = pdv,
            UkupnoEurSPdv = ukupnoEur + pdv,
            Klijent = request.Klijent,
            // Map nested request Odvjetnik (naziv) into response party `ime` for compatibility
            Odvjetnik = request.Odvjetnik is null ? null : new TroskovnikParty { Ime = request.Odvjetnik.Naziv ?? "", Oib = request.Odvjetnik.Oib, Adresa = request.Odvjetnik.Adresa },
            // Map protustranka from nested stranke if present
            Protustranka = request.Stranke?.Protustranka is null ? null : new TroskovnikParty { Ime = request.Stranke.Protustranka.Naziv ?? "", Oib = request.Stranke.Protustranka.Oib, Adresa = request.Stranke.Protustranka.Adresa },
            ProtustrankaPolozaj = request.Stranke?.Protustranka?.Polozaj ?? request.ProtustrankaPolozaj,
            IsPdvUkljucen = request.IsPdvUkljucen ?? request.HasVat,
            HasVat = request.HasVat,
            OpisPredmeta = request.OpisPredmeta,
            // Enrich with predmet metadata
            Sud = request.Predmet?.Sud,
            BrojPredmeta = request.Predmet?.PoslovniBroj ?? request.CaseId,
            DatumIzrade = DateTime.UtcNow,
            // Nested objects (new schema) — pass through as-is for frontend consumption
            OdvjetnikInfo = request.Odvjetnik,
            PredmetInfo = request.Predmet,
            StrankeInfo = request.Stranke,
            // Kazneni echo fields
            ZaprijecenaKazna = request.ZaprijecenaKazna,
            IsPoSluzbenojDuznosti = request.IsPoSluzbenojDuznosti,
            IsNaTeretProracuna = request.IsNaTeretProracuna,
            IsZastupnikDjeteta = request.IsZastupnikDjeteta,
            IsStjecaj = request.IsStjecaj,
            NapomenaStjecaj = request.IsStjecaj ? NapomenaZaStjecaj : null,
            IsOduzimanjePrekrsaj = request.IsOduzimanjePrekrsaj,
            VrijednostOduzimanja = request.VrijednostOduzimanja,
            // Sudske pristojbe
            SudskaPristojba = sudskaPristojba,
            PristojbaOsnova = pristojbaOsnova,
            IsOslobodjenPristojbi = request.IsOslobodjenPristojbi,
            SveukupniTrosak = sveukupniTrosak
        };
    }

    // ── Stjecaj i produljeno kazneno djelo (Tbr. 4. toč. 1 al. 6) ─

    /// <summary>
    /// Alineja 6 ne uvodi ni množitelj ni broj radnji — ona samo određuje KOJI se od
    /// razreda iz alineja 1–5 uzima: onaj koji odgovara najtežoj kazni koja se u
    /// postupku može izreći primjenom pravila o stjecaju. Nagrada ostaje jedna.
    ///
    /// Zato zastavica ne mijenja izračun, nego se bilježi uz troškovnik kako bi se
    /// vidjelo zašto je odabran razred koji ne odgovara pojedinačnom djelu.
    /// </summary>
    public const string NapomenaZaStjecaj =
        "Kaznena djela u stjecaju odnosno produljeno kazneno djelo: razred je odabran " +
        "prema najtežoj kazni koja se u postupku može izreći primjenom odredbi o " +
        "izricanju kazne za djela u stjecaju (Tbr. 4. toč. 1 al. 6).";

    // ── Zaokruživanje ─────────────────────────────────────────────

    /// <summary>
    /// Jedino zaokruživanje u cijelom izračunu: novac, na dvije decimale, standardno.
    ///
    /// Bodovi se ne zaokružuju — Tarifa to nigdje ne propisuje, a lančano zaokruživanje
    /// prema gore sustavno je podizalo iznose (75 × 0,5 × 1,25 = 46,875, a stari je
    /// lanac davao 48).
    ///
    /// AwayFromZero, ne bankarsko: 23,4375 → 23,44, kako se novac u praksi zaokružuje.
    /// </summary>
    private static decimal ZaokruziNovac(decimal iznos)
        => Math.Round(iznos, 2, MidpointRounding.AwayFromZero);

    // ── Tbr. 40. toč. 1 — zastupanje više osoba ───────────────────

    /// <summary>10% za drugu i svaku sljedeću osobu, najviše 50%.</summary>
    public const decimal ViseOsobaPoOsobi = 0.10m;

    /// <summary>Gornja granica povišenja iz Tbr. 40. toč. 1.</summary>
    public const decimal ViseOsobaMaxPovisenje = 0.50m;

    /// <summary>
    /// Tbr. 40. toč. 1: "Kad odvjetnik u istom postupku brani ili zastupa više osoba,
    /// pripada mu pravo na povišenje tarifnih stavaka za drugu i svaku sljedeću osobu
    /// za 10% — uz uvjet da to povišenje može iznositi najviše 50%."
    ///
    /// Prva osoba ne ulazi u postotak, pa je osnova (n − 1). Povišenje se primjenjuje
    /// na cijelu osnovicu troškovnika ("tarifnih stavaka"), a ne na pojedinu stavku.
    /// </summary>
    public static decimal PovisenjeViseOsobaPostotak(int brojOsoba)
    {
        if (brojOsoba <= 1) return 0m;

        return Math.Min((brojOsoba - 1) * ViseOsobaPoOsobi, ViseOsobaMaxPovisenje);
    }

    // ── Ukupno bodova za jednu stavku troškovnika ─────────────────

    /// <summary>
    /// Za obične stavke: osnovica × količina.
    ///
    /// Za jednokratne nagrade (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) osnovica pokriva
    /// cijeli prvostupanjski postupak i NE množi se količinom. Ako tarifni broj
    /// propisuje naknadu za daljnje radnje ("Za svaku daljnju radnju... 50 bodova"),
    /// dodaje se samo za radnje preko uključenog broja.
    /// </summary>
    private static decimal UkupnoBodovaZaStavku(TarifnaStavka def, decimal bodovi, int kolicina)
    {
        if (!def.JednokratnaNagrada)
            return bodovi * kolicina;

        var ukupno = bodovi;

        if (def.UkljuceneRadnje.HasValue && def.DaljnjaRadnjaBodovi.HasValue)
        {
            var daljnjeRadnje = Math.Max(0, kolicina - def.UkljuceneRadnje.Value);
            ukupno += daljnjeRadnje * def.DaljnjaRadnjaBodovi.Value;
        }

        return ukupno;
    }

    // ── Izračun bodova za jednu stavku ────────────────────────────

    /// <summary>
    /// Lanac se računa u decimalu, bez zaokruživanja između koraka. Tarifa
    /// zaokruživanje bodova nigdje ne propisuje, a Math.Ceiling na svakom koraku
    /// sustavno je podizao iznose.
    /// </summary>
    private static decimal IzracunajBodove(TarifnaStavka def, OsnovicaRezultat osnovica)
    {
        decimal bodovi;

        if (def.FiksniBody.HasValue)
        {
            // Iznos koji Tarifa određuje apsolutno (npr. Tbr. 7. toč. 10 = 250 bodova) —
            // ne ovisi ni o vrijednosti spora ni o osnovici.
            bodovi = def.FiksniBody.Value;
        }
        else if (osnovica.FiksnoPoRadnji)
        {
            // Tbr. 45 uz način "fiksno po radnji": svaka poduzeta radnja nosi puni iznos
            // iz Tbr. 45. toč. 1–3. Postoci i stropovi iz Tbr. 8./9. se NE primjenjuju —
            // to je upravo ono po čemu se to čitanje razlikuje od "kao osnovica".
            return osnovica.Bodovi;
        }
        else if (def.Tbr71Postotak.HasValue)
        {
            bodovi = osnovica.Bodovi * def.Tbr71Postotak.Value;
            if (def.Tbr71Povecanje.HasValue)
                bodovi *= 1 + def.Tbr71Povecanje.Value;
        }
        else
        {
            bodovi = osnovica.Bodovi;
        }

        return ClampBodovi(bodovi, def);
    }

    // ── Kazneni point calculation ─────────────────────────────────

    /// <summary>
    /// Izračunava bodove za stavke s KazneniBase = true.
    /// Koristi ZaprijecenaKaznaTiers lookup i primjenjuje modifikatore:
    ///   - KazneniPovecanje (npr. +50% za žalbu)
    ///   - IsZastupnikDjeteta → 70% od Tbr. 4.8 (= 70% × 50% = 35% baze)
    ///   - IsPoSluzbenojDuznosti / IsNaTeretProracuna → 50% umanjenje
    /// </summary>
    private static decimal IzracunajKazneneBodove(TarifnaStavka def, int tierBodovi, TroskovnikRequest req)
    {
        decimal bodovi = tierBodovi;

        // Povišenje vezano uz tarifni broj (npr. +50% za žalbu protiv presude, Tbr. 5.1).
        //
        // Tbr. 4. toč. 2 al. 3 propisuje drukčiji postotak za raspravu pred drugim i
        // trećim stupnjem kad je zapriječena kazna dugotrajni zatvor — tada se koristi
        // KazneniPovecanjeDugotrajni umjesto redovnog povišenja.
        var jeDugotrajni = req.ZaprijecenaKazna == ZaprijecenaKaznaTiers.DugotrajniZatvor;
        var povecanje = jeDugotrajni && def.KazneniPovecanjeDugotrajni.HasValue
            ? def.KazneniPovecanjeDugotrajni
            : def.KazneniPovecanje;

        if (povecanje.HasValue)
            bodovi *= 1 + povecanje.Value;

        // Opunomoćenik djeteta žrtve kaznenog djela →
        //   Tbr. 4.8: zastupanje oštećenika = 50% od Tbr. 4
        //   Dijete žrtve = 70% od Tbr. 4.8  ⇒ efektivno 35%
        if (req.IsZastupnikDjeteta)
            bodovi *= TarifaConstants.Tbr4t8Rate * TarifaConstants.ZastupnikDjetetaRate;

        // Branitelj po službenoj dužnosti ili na teret proračunskih sredstava → 50%
        if (req.IsPoSluzbenojDuznosti || req.IsNaTeretProracuna)
            bodovi *= TarifaConstants.KazneniReductionRate;

        return ClampBodovi(bodovi, def);
    }

    // ── Clamp helper ──────────────────────────────────────────────

    private static decimal ClampBodovi(decimal bodovi, TarifnaStavka def)
    {
        if (def.MaxBodova.HasValue && bodovi > def.MaxBodova.Value)
            bodovi = def.MaxBodova.Value;
        if (def.MinBodova.HasValue && bodovi < def.MinBodova.Value)
            bodovi = def.MinBodova.Value;

        return bodovi;
    }
}
