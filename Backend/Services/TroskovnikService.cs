using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Odvjetnička tarifa – izračun troškovnika prema Tarifi o nagradama
/// i naknadi troškova za rad odvjetnika (NN 2023).
/// Tbr. 54: Vrijednost boda = 2,00 EUR.
/// </summary>
public sealed class TroskovnikService
{
    // ── Tbr. 7, toč. 1 — base point lookup ───────────────────────

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
        new() { Id = "kaz_privatna_tuzba", Kategorija = "Kazneni postupak", Opis = "Privatna tužba / kaznena prijava / optužni prijedlog (Tbr. 1.1)", TarifniBroj = "Tbr. 1.1", FiksniBody = null, Tbr71Postotak = null, MinBodova = 100, SortOrder = 100, Jedinica = "komad" },
        new() { Id = "kaz_ostali_podnesci", Kategorija = "Kazneni postupak", Opis = "Ostali podnesci u kaznenom postupku (Tbr. 1.5)", TarifniBroj = "Tbr. 1.5", FiksniBody = 50, SortOrder = 105, Jedinica = "komad" },
        new() { Id = "kaz_imovinskopravni", Kategorija = "Kazneni postupak", Opis = "Podnesak za imovinskopravni zahtjev oštećenika (Tbr. 1.4)", TarifniBroj = "Tbr. 1.4", FiksniBody = 75, SortOrder = 103, Jedinica = "komad" },
        new() { Id = "kaz_obrana_do3", Kategorija = "Kazneni postupak", Opis = "Obrana – novčana / do 3 god. zatvora (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 100, SortOrder = 110, Jedinica = "sat" },
        new() { Id = "kaz_obrana_do5", Kategorija = "Kazneni postupak", Opis = "Obrana – do 5 godina zatvora (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 200, SortOrder = 111, Jedinica = "sat" },
        new() { Id = "kaz_obrana_do10", Kategorija = "Kazneni postupak", Opis = "Obrana – do 10 godina zatvora (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 300, SortOrder = 112, Jedinica = "sat" },
        new() { Id = "kaz_obrana_preko10", Kategorija = "Kazneni postupak", Opis = "Obrana – preko 10 godina zatvora (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 400, SortOrder = 113, Jedinica = "sat" },
        new() { Id = "kaz_obrana_dugotrajni", Kategorija = "Kazneni postupak", Opis = "Obrana – dugotrajni zatvor (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 500, SortOrder = 114, Jedinica = "sat" },
        new() { Id = "kaz_obrana_prekrsaj", Kategorija = "Kazneni postupak", Opis = "Obrana u prekršajnom postupku (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 100, SortOrder = 115, Jedinica = "sat" },
        new() { Id = "kaz_dodatni_sat", Kategorija = "Kazneni postupak", Opis = "Svaki sljedeći započeti sat rasprave (Tbr. 4.1)", TarifniBroj = "Tbr. 4.1", FiksniBody = 50, SortOrder = 116, Jedinica = "sat" },
        new() { Id = "kaz_razgovor_pritvor", Kategorija = "Kazneni postupak", Opis = "Razgovor s okrivljenikom u pritvoru (Tbr. 4.5)", TarifniBroj = "Tbr. 4.5", FiksniBody = 50, SortOrder = 120, Jedinica = "komad" },
        new() { Id = "kaz_zalba_presuda", Kategorija = "Kazneni postupak", Opis = "Žalba protiv presude (Tbr. 5.1) – Tbr. 4.1 + 50%", TarifniBroj = "Tbr. 5.1", FiksniBody = null, SortOrder = 130, Jedinica = "komad" },
        new() { Id = "kaz_zalba_troskovi", Kategorija = "Kazneni postupak", Opis = "Žalba protiv odluke o trošku (Tbr. 5.5)", TarifniBroj = "Tbr. 5.5", FiksniBody = 50, SortOrder = 135, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // II. PARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "par_tuzba", Kategorija = "Parnični postupak", Opis = "Tužba / protutužba / prijedlog (Tbr. 7.1)", TarifniBroj = "Tbr. 7.1", Tbr71Postotak = 1.0m, SortOrder = 200, Jedinica = "komad" },
        new() { Id = "par_tuzba_smetanje", Kategorija = "Parnični postupak", Opis = "Tužba – smetanje posjeda / služnost / stambeni / radni spor (Tbr. 7.2)", TarifniBroj = "Tbr. 7.2", FiksniBody = 250, SortOrder = 201, Jedinica = "komad" },
        new() { Id = "par_tuzba_obiteljski", Kategorija = "Parnični postupak", Opis = "Tužba – razvod / očinstvo / uzdržavanje (Tbr. 7.3)", TarifniBroj = "Tbr. 7.3", FiksniBody = 200, SortOrder = 202, Jedinica = "komad" },
        new() { Id = "par_tuzba_platni_nalog", Kategorija = "Parnični postupak", Opis = "Tužba za platni nalog (Tbr. 7.5) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 7.5", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 205, Jedinica = "komad" },
        new() { Id = "par_tuzba_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred domaćom arbitražom (Tbr. 7.7)", TarifniBroj = "Tbr. 7.7", Tbr71Postotak = 1.0m, SortOrder = 207, Jedinica = "komad" },
        new() { Id = "par_tuzba_med_arbitraza", Kategorija = "Parnični postupak", Opis = "Tužba pred međunarodnom arbitražom (Tbr. 7.7) – +100%", TarifniBroj = "Tbr. 7.7", Tbr71Postotak = 1.0m, Tbr71Povecanje = 1.0m, SortOrder = 208, Jedinica = "komad" },
        new() { Id = "par_obrazlozeni_podnesak", Kategorija = "Parnični postupak", Opis = "Obrazloženi podnesci (Tbr. 8.3) – 50% Tbr. 7.1, max 100", TarifniBroj = "Tbr. 8.3", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 210, Jedinica = "komad" },
        new() { Id = "par_ostali_podnesci", Kategorija = "Parnični postupak", Opis = "Ostali podnesci (Tbr. 8.4) – 25% Tbr. 7.1, max 25", TarifniBroj = "Tbr. 8.4", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 211, Jedinica = "komad" },
        new() { Id = "par_rociste_glavno", Kategorija = "Parnični postupak", Opis = "Ročište – glavna stvar / dokazi / nagodba (Tbr. 9.1)", TarifniBroj = "Tbr. 9.1", Tbr71Postotak = 1.0m, SortOrder = 220, Jedinica = "komad" },
        new() { Id = "par_rociste_procesno", Kategorija = "Parnični postupak", Opis = "Ročište – procesna pitanja (Tbr. 9.2) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 9.2", Tbr71Postotak = 0.5m, SortOrder = 221, Jedinica = "komad" },
        new() { Id = "par_rociste_presuda", Kategorija = "Parnični postupak", Opis = "Ročište – objava presude (Tbr. 9.3) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.3", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 222, Jedinica = "komad" },
        new() { Id = "par_pristup_odgodjen", Kategorija = "Parnični postupak", Opis = "Pristup odgođenom ročištu (Tbr. 9.5) – 25% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 9.5", Tbr71Postotak = 0.25m, MaxBodova = 50, SortOrder = 223, Jedinica = "komad" },
        new() { Id = "par_ocevid", Kategorija = "Parnični postupak", Opis = "Nazočnost očevidu (Tbr. 9.7)", TarifniBroj = "Tbr. 9.7", Tbr71Postotak = 1.0m, SortOrder = 224, Jedinica = "komad" },
        new() { Id = "par_zalba_presuda", Kategorija = "Parnični postupak", Opis = "Žalba protiv presude (Tbr. 10.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 10.1", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 230, Jedinica = "komad" },
        new() { Id = "par_zalba_rjesenje", Kategorija = "Parnični postupak", Opis = "Žalba protiv rješenja (Tbr. 10.4) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 10.4", Tbr71Postotak = 0.5m, SortOrder = 231, Jedinica = "komad" },
        new() { Id = "par_revizija", Kategorija = "Parnični postupak", Opis = "Revizija / izvanredni pravni lijek (Tbr. 10.5) – Tbr. 7.1 + 50%", TarifniBroj = "Tbr. 10.5", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.5m, SortOrder = 232, Jedinica = "komad" },
        new() { Id = "par_odgovor_zalba", Kategorija = "Parnični postupak", Opis = "Odgovor na žalbu / reviziju (Tbr. 10.6)", TarifniBroj = "Tbr. 10.6", Tbr71Postotak = 1.0m, SortOrder = 233, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // III. OVRŠNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ovr_prijedlog", Kategorija = "Ovršni postupak", Opis = "Prijedlog za ovrhu / protuovrhu / osiguranje (Tbr. 11.1)", TarifniBroj = "Tbr. 11.1", Tbr71Postotak = 1.0m, SortOrder = 300, Jedinica = "komad" },
        new() { Id = "ovr_prokazna_izjava", Kategorija = "Ovršni postupak", Opis = "Prijedlog za prokaznu izjavu (Tbr. 11.3) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.3", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 303, Jedinica = "komad" },
        new() { Id = "ovr_odgoda", Kategorija = "Ovršni postupak", Opis = "Prijedlog za odgodu ovrhe (Tbr. 11.4) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.4", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 304, Jedinica = "komad" },
        new() { Id = "ovr_sudska_ovrha", Kategorija = "Ovršni postupak", Opis = "Prijedlog sudu za provedbu sudske ovrhe (Tbr. 11.5) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.5", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 305, Jedinica = "komad" },
        new() { Id = "ovr_dostava", Kategorija = "Ovršni postupak", Opis = "Dostava ovršne isprave (Tbr. 11.6) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.6", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 306, Jedinica = "komad" },
        new() { Id = "ovr_prituzba", Kategorija = "Ovršni postupak", Opis = "Pritužba ovršenika (Tbr. 11.7) – 50% Tbr. 11.1, max 100", TarifniBroj = "Tbr. 11.7", Tbr71Postotak = 0.5m, MaxBodova = 100, SortOrder = 307, Jedinica = "komad" },
        new() { Id = "ovr_zaduznica", Kategorija = "Ovršni postupak", Opis = "Zahtjev za naplatu po zadužnici (Tbr. 11.8) – 50% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 11.8", Tbr71Postotak = 0.5m, MaxBodova = 500, SortOrder = 308, Jedinica = "komad" },
        new() { Id = "ovr_drazbeni_uvjeti", Kategorija = "Ovršni postupak", Opis = "Dražbeni uvjeti (Tbr. 11.9)", TarifniBroj = "Tbr. 11.9", Tbr71Postotak = 1.0m, SortOrder = 309, Jedinica = "komad" },
        new() { Id = "ovr_ostali_podnesci", Kategorija = "Ovršni postupak", Opis = "Ostali podnesci u ovrsi (Tbr. 11.10) – 25% Tbr. 7.1, max 250", TarifniBroj = "Tbr. 11.10", Tbr71Postotak = 0.25m, MaxBodova = 250, SortOrder = 310, Jedinica = "komad" },
        new() { Id = "ovr_rociste", Kategorija = "Ovršni postupak", Opis = "Ročište u ovrsi (Tbr. 12.1)", TarifniBroj = "Tbr. 12.1", Tbr71Postotak = 1.0m, SortOrder = 312, Jedinica = "komad" },
        new() { Id = "ovr_pravni_lijek", Kategorija = "Ovršni postupak", Opis = "Pravni lijek u ovrsi (Tbr. 13.1)", TarifniBroj = "Tbr. 13.1", Tbr71Postotak = 1.0m, SortOrder = 315, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IV. STEČAJNI I PREDSTEČAJNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "ste_prijedlog", Kategorija = "Stečajni postupak", Opis = "Prijedlog za pokretanje stečaja/predstečaja (Tbr. 14.1) – Tbr. 7.1, max 1000", TarifniBroj = "Tbr. 14.1", Tbr71Postotak = 1.0m, MaxBodova = 1000, SortOrder = 400, Jedinica = "komad" },
        new() { Id = "ste_prijava_potrazivanja", Kategorija = "Stečajni postupak", Opis = "Prijava potraživanja / obrazloženi podnesak (Tbr. 14.2) – Tbr. 7.1, max 2000", TarifniBroj = "Tbr. 14.2", Tbr71Postotak = 1.0m, MaxBodova = 2000, SortOrder = 401, Jedinica = "komad" },
        new() { Id = "ste_pravni_lijek", Kategorija = "Stečajni postupak", Opis = "Pravni lijek u stečaju (Tbr. 14.3) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 14.3", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 402, Jedinica = "komad" },
        new() { Id = "ste_rociste", Kategorija = "Stečajni postupak", Opis = "Ročište / sjednica vjerovnika (Tbr. 15)", TarifniBroj = "Tbr. 15", Tbr71Postotak = 1.0m, SortOrder = 405, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // V. IZVANPARNIČNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "izv_dioba", Kategorija = "Izvanparnični postupak", Opis = "Pokretanje postupka – dioba / ostavina / procjenjivi (Tbr. 16.1) – 50% Tbr. 7.1", TarifniBroj = "Tbr. 16.1", Tbr71Postotak = 0.5m, SortOrder = 500, Jedinica = "komad" },
        new() { Id = "izv_meda", Kategorija = "Izvanparnični postupak", Opis = "Uređenje međe (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 501, Jedinica = "komad" },
        new() { Id = "izv_neprocjenjivi", Kategorija = "Izvanparnični postupak", Opis = "Ostali neprocjenjivi izvanparnični (Tbr. 16.1)", TarifniBroj = "Tbr. 16.1", FiksniBody = 50, SortOrder = 502, Jedinica = "komad" },
        new() { Id = "izv_amortizacija", Kategorija = "Izvanparnični postupak", Opis = "Amortizacija isprava (Tbr. 16.1) – 50% Tbr. 7.1, max 50", TarifniBroj = "Tbr. 16.1", Tbr71Postotak = 0.5m, MaxBodova = 50, SortOrder = 503, Jedinica = "komad" },
        new() { Id = "izv_rociste", Kategorija = "Izvanparnični postupak", Opis = "Ročište u izvanparnici – procjenjivo (Tbr. 17.1) – 75% Tbr. 7.1", TarifniBroj = "Tbr. 17.1", Tbr71Postotak = 0.75m, SortOrder = 510, Jedinica = "komad" },
        new() { Id = "izv_pravni_lijek", Kategorija = "Izvanparnični postupak", Opis = "Pravni lijek u izvanparnici (Tbr. 18.1) – Tbr. 16 + 25%", TarifniBroj = "Tbr. 18.1", Tbr71Postotak = 0.5m, Tbr71Povecanje = 0.25m, SortOrder = 515, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VI. ZEMLJIŠNOKNJIŽNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "zk_redoviti", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – redoviti postupak (Tbr. 19.1) – 25% Tbr. 7.1, max 500", TarifniBroj = "Tbr. 19.1", Tbr71Postotak = 0.25m, MaxBodova = 500, SortOrder = 600, Jedinica = "komad" },
        new() { Id = "zk_redoviti_neproc", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – neprocjenjiv (Tbr. 19.2)", TarifniBroj = "Tbr. 19.2", FiksniBody = 50, SortOrder = 601, Jedinica = "komad" },
        new() { Id = "zk_ostali", Kategorija = "Zemljišnoknjižni postupak", Opis = "Ostali podnesci u ZK postupku (Tbr. 19.3)", TarifniBroj = "Tbr. 19.3", FiksniBody = 50, SortOrder = 602, Jedinica = "komad" },
        new() { Id = "zk_posebni", Kategorija = "Zemljišnoknjižni postupak", Opis = "ZK prijedlog – posebni postupak (Tbr. 20.1) – 25% Tbr. 7.1", TarifniBroj = "Tbr. 20.1", Tbr71Postotak = 0.25m, SortOrder = 605, Jedinica = "komad" },
        new() { Id = "zk_pravni_lijek", Kategorija = "Zemljišnoknjižni postupak", Opis = "Pravni lijek u ZK postupku (Tbr. 21) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 21", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 610, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VII. UPRAVNI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "upr_neprocjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – neprocjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", FiksniBody = 50, SortOrder = 700, Jedinica = "komad" },
        new() { Id = "upr_procjenjiv", Kategorija = "Upravni postupak", Opis = "Zahtjev u upravnom postupku – procjenjiv (Tbr. 22.1)", TarifniBroj = "Tbr. 22.1", Tbr71Postotak = 1.0m, SortOrder = 701, Jedinica = "komad" },
        new() { Id = "upr_ostali", Kategorija = "Upravni postupak", Opis = "Ostali podnesci u upravnom postupku (Tbr. 22.2) – 25%, max 25", TarifniBroj = "Tbr. 22.2", Tbr71Postotak = 0.25m, MaxBodova = 25, SortOrder = 702, Jedinica = "komad" },
        new() { Id = "upr_zalba", Kategorija = "Upravni postupak", Opis = "Žalba / prigovor / drug. instanca (Tbr. 24) – Tbr. 22 + 25%", TarifniBroj = "Tbr. 24", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 710, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // VIII. UPRAVNI SPOR
        // ═══════════════════════════════════════════════════════════
        new() { Id = "usp_tuzba_proc", Kategorija = "Upravni spor", Opis = "Tužba u upravnom sporu – procjenjiv (Tbr. 27.1)", TarifniBroj = "Tbr. 27.1", Tbr71Postotak = 1.0m, SortOrder = 800, Jedinica = "komad" },
        new() { Id = "usp_tuzba_neproc", Kategorija = "Upravni spor", Opis = "Tužba u upravnom sporu – neprocjenjiv (Tbr. 27.1)", TarifniBroj = "Tbr. 27.1", FiksniBody = 500, SortOrder = 801, Jedinica = "komad" },
        new() { Id = "usp_rasprava", Kategorija = "Upravni spor", Opis = "Zastupanje na raspravi (Tbr. 27.2)", TarifniBroj = "Tbr. 27.2", Tbr71Postotak = 1.0m, SortOrder = 802, Jedinica = "komad" },
        new() { Id = "usp_zalba", Kategorija = "Upravni spor", Opis = "Žalba / zahtjev VUS (Tbr. 27.3) – Tbr. 27.1 + 25%", TarifniBroj = "Tbr. 27.3", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 805, Jedinica = "komad" },
        new() { Id = "usp_izvrsenje", Kategorija = "Upravni spor", Opis = "Zahtjev za izvršenje presude (Tbr. 27.4) – 50% Tbr. 27.1", TarifniBroj = "Tbr. 27.4", Tbr71Postotak = 0.5m, SortOrder = 806, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // IX. REGISTARSKI POSTUPAK
        // ═══════════════════════════════════════════════════════════
        new() { Id = "reg_upis", Kategorija = "Registarski postupak", Opis = "Podnesak za upis / promjenu u registru (Tbr. 28.1)", TarifniBroj = "Tbr. 28.1", FiksniBody = 50, SortOrder = 900, Jedinica = "komad" },
        new() { Id = "reg_ostali", Kategorija = "Registarski postupak", Opis = "Ostali podnesci za upis (Tbr. 28.3)", TarifniBroj = "Tbr. 28.3", FiksniBody = 50, SortOrder = 903, Jedinica = "komad" },
        new() { Id = "reg_pravni_lijek", Kategorija = "Registarski postupak", Opis = "Pravni lijek u registarskom postupku (Tbr. 28.4) – 50 + 50%", TarifniBroj = "Tbr. 28.4", FiksniBody = 75, SortOrder = 904, Jedinica = "komad" },

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
        new() { Id = "ost_naknada_stete", Kategorija = "Ostali postupci", Opis = "Zahtjev za naknadu štete prema državi / osiguranju (Tbr. 32.1)", TarifniBroj = "Tbr. 32.1", Tbr71Postotak = 1.0m, SortOrder = 1200, Jedinica = "komad" },
        new() { Id = "ost_stegovni", Kategorija = "Ostali postupci", Opis = "Stegovni postupak – podnesci / pravni lijekovi (Tbr. 32.2)", TarifniBroj = "Tbr. 32.2", FiksniBody = 100, SortOrder = 1201, Jedinica = "komad" },

        // ═══════════════════════════════════════════════════════════
        // XIII. RAZNO – ISPRAVE, SAVJETI, OPOMENE
        // ═══════════════════════════════════════════════════════════
        new() { Id = "raz_ugovor", Kategorija = "Razno", Opis = "Sastavljanje ugovora (Tbr. 33.1) – Tbr. 7.1 + 25%", TarifniBroj = "Tbr. 33.1", Tbr71Postotak = 1.0m, Tbr71Povecanje = 0.25m, SortOrder = 1300, Jedinica = "komad" },
        new() { Id = "raz_ugovor_neproc", Kategorija = "Razno", Opis = "Ugovor – neprocjenjiv (Tbr. 33.2)", TarifniBroj = "Tbr. 33.2", FiksniBody = 200, SortOrder = 1301, Jedinica = "komad" },
        new() { Id = "raz_izjava", Kategorija = "Razno", Opis = "Jednostavna izjava volje (Tbr. 33.3)", TarifniBroj = "Tbr. 33.3", FiksniBody = 50, SortOrder = 1302, Jedinica = "komad" },
        new() { Id = "raz_oporuka", Kategorija = "Razno", Opis = "Sastavljanje oporuke (Tbr. 33.4)", TarifniBroj = "Tbr. 33.4", FiksniBody = 100, SortOrder = 1303, Jedinica = "komad" },
        new() { Id = "raz_usmeni_savjet", Kategorija = "Razno", Opis = "Usmeni pravni savjet (Tbr. 34.1) – 50 bodova/sat", TarifniBroj = "Tbr. 34.1", FiksniBody = 50, SortOrder = 1310, Jedinica = "sat" },
        new() { Id = "raz_pisani_savjet", Kategorija = "Razno", Opis = "Pisani pravni savjet / mišljenje (Tbr. 34.2)", TarifniBroj = "Tbr. 34.2", Tbr71Postotak = 1.0m, MinBodova = 200, SortOrder = 1311, Jedinica = "komad" },
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
        new() { Id = "zaj_vise_osoba", Kategorija = "Zajedničke odredbe", Opis = "Povišenje za zastupanje više osoba +10%/os (Tbr. 40)", TarifniBroj = "Tbr. 40", FiksniBody = 0, SortOrder = 1410, Jedinica = "osoba" },
    ];

    // ── Calculate a troškovnik ────────────────────────────────────

    public TroskovnikResponse Izracunaj(TroskovnikRequest request)
    {
        var baseBodovi = Tbr71Bodovi(request.VrijednostSpora);
        var catalogMap = _katalog.ToDictionary(s => s.Id);
        var lines = new List<TroskovnikLineItem>();
        var rbr = 0;

        foreach (var stavka in request.Stavke)
        {
            if (!catalogMap.TryGetValue(stavka.ItemId, out var def))
                continue;
            if (stavka.Kolicina <= 0)
                continue;

            var bodovi = IzracunajBodove(def, baseBodovi);
            rbr++;

            lines.Add(new TroskovnikLineItem
            {
                Rbr = rbr,
                ItemId = def.Id,
                Opis = def.Opis,
                TarifniBroj = def.TarifniBroj,
                Kolicina = stavka.Kolicina,
                BodovaPoJedinici = bodovi,
                UkupnoBodova = bodovi * stavka.Kolicina,
                IznosEur = bodovi * stavka.Kolicina * TarifaConstants.BodValue
            });
        }

        var ukupnoBodova = lines.Sum(l => l.UkupnoBodova);
        var ukupnoEur = ukupnoBodova * TarifaConstants.BodValue;
        decimal pdv = 0;
        if (request.HasVat)
            pdv = Math.Round(ukupnoEur * TarifaConstants.PdvRate, 2);

        return new TroskovnikResponse
        {
            Stavke = lines,
            UkupnoBodova = ukupnoBodova,
            UkupnoEurBezPdv = ukupnoEur,
            Pdv = pdv,
            UkupnoEurSPdv = ukupnoEur + pdv,
            Klijent = request.Klijent,
            Odvjetnik = request.Odvjetnik,
            OpisPredmeta = request.OpisPredmeta,
            DatumIzrade = DateTime.UtcNow
        };
    }

    // ── Point calculation for a single item ───────────────────────

    private static int IzracunajBodove(TarifnaStavka def, int baseBodovi)
    {
        int bodovi;

        if (def.FiksniBody.HasValue)
        {
            bodovi = def.FiksniBody.Value;
        }
        else if (def.Tbr71Postotak.HasValue)
        {
            var raw = (int)Math.Ceiling(baseBodovi * def.Tbr71Postotak.Value);
            if (def.Tbr71Povecanje.HasValue)
                raw = (int)Math.Ceiling(raw * (1 + def.Tbr71Povecanje.Value));
            bodovi = raw;
        }
        else
        {
            bodovi = baseBodovi;
        }

        if (def.MaxBodova.HasValue && bodovi > def.MaxBodova.Value)
            bodovi = def.MaxBodova.Value;
        if (def.MinBodova.HasValue && bodovi < def.MinBodova.Value)
            bodovi = def.MinBodova.Value;

        return bodovi;
    }
}
