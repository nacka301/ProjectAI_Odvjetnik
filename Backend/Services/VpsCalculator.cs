using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Vrijednost predmeta spora prema Zakonu o sudskim pristojbama (NN 118/18, 51/23),
/// čl. 14–26.
///
/// <para>Za niz postupaka VPS nije stvar unosa nego zakonske odredbe: smetanje posjeda je
/// uvijek 663,61 €, razvod isto, poništenje arbitražnog pravorijeka nikad ispod
/// 6.634,14 €. Prije ovog kalkulatora korisnik je mogao upisati bilo koji broj i dobiti
/// pristojbu koja ne odgovara zakonu.</para>
///
/// <para>Rezultat obavezno vrijedi za SUDSKU PRISTOJBU. Na odvjetničku nagradu po
/// Tbr. 7. toč. 1 prenosi se samo kad je <c>vps_primijeni_na_nagradu</c> uključen —
/// čl. 14–26 pisani su za pristojbe, a ne za Tarifu, pa sustav to ne pretpostavlja.</para>
/// </summary>
public static class VpsCalculator
{
    /// <summary>
    /// Razrješava vrijednost predmeta spora, tri koraka redom:
    ///
    /// <list type="number">
    /// <item>čl. 16 — sastavi osnovnu vrijednost iz pojedinačnih zahtjeva</item>
    /// <item>čl. 15, 17–21, 25 — primijeni pravilo vrste postupka na tu vrijednost</item>
    /// <item>čl. 24 — kod pravnog lijeka pregazi vrijednošću pobijanog dijela</item>
    /// </list>
    ///
    /// Redoslijed nije proizvoljan: čl. 16 kaže od čega se vrijednost sastavlja, čl. 21
    /// je ograničava, a čl. 24 određuje da se za pravni lijek uzima drugi broj.
    /// </summary>
    public static VpsRezultat Razrijesi(TroskovnikRequest request)
    {
        var pravilo = DohvatiPravilo(request.VrstaPostupka);

        var (uneseni, ostali) = OsnovniVps(request);
        var (vps, napomena, izvor) = PrimijeniPravilo(pravilo, uneseni, request);
        (vps, napomena, izvor) = PrimijeniPravniLijek(request, vps, napomena, izvor);

        return new VpsRezultat(vps, uneseni, pravilo.Kljuc, izvor, vps != uneseni, napomena, ostali);
    }

    /// <summary>
    /// Vrijednost predmeta spora bez ijednog zakonskog pravila — ono što je korisnik
    /// unio. Koristi se kad je odabrano opće pravilo iz čl. 14.
    /// </summary>
    public static VpsRezultat Neprilagodjen(decimal vrijednostSpora)
        => new(
            vrijednostSpora, vrijednostSpora, VrstaPostupka.Opci,
            $"{TarifaConstants.NazivZakonaOPristojbama}, čl. 14 — vrijednost u vrijeme podnošenja tužbe",
            false, null, []);

    private static VpsPravilo DohvatiPravilo(string? kljuc)
    {
        if (string.IsNullOrWhiteSpace(kljuc))
            return VrstaPostupka.Pravila[VrstaPostupka.Opci];

        if (!VrstaPostupka.Pravila.TryGetValue(kljuc, out var pravilo))
            throw new TroskovnikValidationException(
                $"Nepoznata vrsta postupka \"{kljuc}\". " +
                $"Dopušteno: {string.Join(", ", VrstaPostupka.Sve)}.");

        return pravilo;
    }

    // ── Čl. 16 — više zahtjeva ────────────────────────────────────

    /// <summary>
    /// Čl. 16 — od čega se sastoji vrijednost kad tužba sadrži više zahtjeva.
    ///
    /// Vraća i zahtjeve koje ovaj troškovnik NE pokriva: kod suparničarstva se svaki
    /// zahtjev procjenjuje zasebno, pa se ne smiju ni zbrojiti ni tiho izostaviti.
    /// </summary>
    private static (decimal Vps, IReadOnlyList<ZahtjevStavka> Ostali) OsnovniVps(
        TroskovnikRequest request)
    {
        var zahtjevi = request.Zahtjevi;
        if (zahtjevi is null || zahtjevi.Count == 0)
            return (request.VrijednostSpora, []);

        if (zahtjevi.Exists(z => z.Iznos < 0))
            throw new TroskovnikValidationException(
                "Vrijednost pojedinog zahtjeva po čl. 16 ne može biti negativna.");

        var nacin = request.ZahtjeviNacin;
        if (string.IsNullOrWhiteSpace(nacin))
            throw new TroskovnikValidationException(
                "Uneseno je više zahtjeva, a nije odabrano kako se sastavljaju. Čl. 16 razlikuje " +
                "zbroj (isti tuženik), zasebnu procjenu (suparničarstvo) i veći od povezanih " +
                "zahtjeva — razlika mijenja vrijednost predmeta spora.");

        switch (nacin)
        {
            // "Ako tužba sadrži više zahtjeva protiv istog tuženika, vrijednost se
            //  određuje prema zbroju vrijednosti svih zahtjeva."
            case ZahtjeviNacin.Zbroj:
                return (zahtjevi.Sum(z => z.Iznos), []);

            // "Kod povezanih zahtjeva mjerodavan je zahtjev čija je vrijednost veća."
            case ZahtjeviNacin.Eventualni:
                return (zahtjevi.Max(z => z.Iznos), []);

            // "Kod suparničarstva i istovrsnih zahtjeva uzima se vrijednost svakog
            //  pojedinog zahtjeva." Zbrajanje bi ovdje bilo pogrešno, pa troškovnik
            //  pokriva jedan zahtjev, a ostali se navode kao zasebne pristojbe.
            case ZahtjeviNacin.Zasebno:
            {
                var primarni = zahtjevi.FindAll(z => z.Primarni);
                if (primarni.Count != 1)
                    throw new TroskovnikValidationException(
                        "Uz suparničarstvo (čl. 16) svaki se zahtjev procjenjuje zasebno, pa se " +
                        "vrijednosti ne zbrajaju. Označite točno jedan zahtjev koji ovaj " +
                        $"troškovnik pokriva — označeno ih je {primarni.Count}.");

                var ostali = zahtjevi.FindAll(z => !z.Primarni);
                return (primarni[0].Iznos, ostali);
            }

            default:
                throw new TroskovnikValidationException(
                    $"Nepoznat način sastavljanja zahtjeva \"{nacin}\". " +
                    $"Dopušteno: {string.Join(", ", ZahtjeviNacin.Sve)}.");
        }
    }

    // ── Čl. 15, 17–21, 25 — pravilo vrste postupka ────────────────

    private static (decimal Vps, string? Napomena, string Izvor) PrimijeniPravilo(
        VpsPravilo pravilo, decimal uneseni, TroskovnikRequest request)
    {
        var izvor = $"{TarifaConstants.NazivZakonaOPristojbama}, {pravilo.Clanak} — {pravilo.Naziv}";

        switch (pravilo.Nacin)
        {
            case VpsNacin.Fiksan:
            {
                var iznos = pravilo.Iznos!.Value;
                var napomena = uneseni == iznos
                    ? null
                    : $"{pravilo.Naziv}: Zakon propisuje vrijednost predmeta spora od " +
                      $"{Novac(iznos)}, pa uneseni iznos od {Novac(uneseni)} nije primijenjen " +
                      $"({pravilo.Clanak}).";
                return (iznos, napomena, izvor);
            }

            case VpsNacin.Minimalan:
            {
                var granica = pravilo.Iznos!.Value;
                if (uneseni >= granica)
                    return (uneseni, null, izvor);

                return (granica,
                    $"{pravilo.Naziv}: vrijednost predmeta spora ne može biti niža od " +
                    $"{Novac(granica)}, pa je uneseni iznos od {Novac(uneseni)} podignut na " +
                    $"zakonski minimum ({pravilo.Clanak}).",
                    izvor);
            }

            case VpsNacin.Maksimalan:
            {
                var granica = pravilo.Iznos!.Value;
                if (uneseni <= granica)
                    return (uneseni, null, izvor);

                return (granica,
                    $"{pravilo.Naziv}: vrijednost predmeta spora ne može biti viša od " +
                    $"{Novac(granica)}, pa je uneseni iznos od {Novac(uneseni)} spušten na " +
                    $"zakonski maksimum ({pravilo.Clanak}).",
                    izvor);
            }

            // Čl. 15: "zbroj davanja za tri mjeseca". Bez mjesečnog iznosa nema se što
            // izvesti — isti obrazac kao Tbr. 45 bez odabrane razine suda.
            case VpsNacin.IzvedenUzdrzavanje:
            {
                if (request.UzdrzavanjeMjesecno <= 0)
                    throw new TroskovnikValidationException(
                        "U postupcima radi uzdržavanja vrijednost predmeta spora je zbroj davanja " +
                        "za tri mjeseca (čl. 15), pa je potreban mjesečni iznos davanja.");

                var vps = request.UzdrzavanjeMjesecno * VpsIznosi.UzdrzavanjeMjeseci;
                return (vps,
                    $"Uzdržavanje: vrijednost predmeta spora je zbroj davanja za tri mjeseca — " +
                    $"{VpsIznosi.UzdrzavanjeMjeseci} × {Novac(request.UzdrzavanjeMjesecno)} = " +
                    $"{Novac(vps)} ({pravilo.Clanak}).",
                    izvor);
            }

            // Čl. 14, 17, 18 — Zakon kaže ŠTO se upisuje, ne koliko. Naputak stoji uz
            // polje u sučelju; iznos ostaje korisnikov.
            default:
                return (uneseni, null, izvor);
        }
    }

    // ── Čl. 24 — pravni lijekovi ──────────────────────────────────

    private static (decimal Vps, string? Napomena, string Izvor) PrimijeniPravniLijek(
        TroskovnikRequest request, decimal vps, string? napomena, string izvor)
    {
        var lijek = string.IsNullOrWhiteSpace(request.PravniLijek)
            ? PravniLijek.Nema
            : request.PravniLijek;

        if (lijek == PravniLijek.Nema)
            return (vps, napomena, izvor);

        if (!PravniLijek.Sve.Contains(lijek))
            throw new TroskovnikValidationException(
                $"Nepoznata vrsta pravnog lijeka \"{lijek}\". " +
                $"Dopušteno: {string.Join(", ", PravniLijek.Sve)}.");

        // "Pristojba se plaća prema vrijednosti predmeta spora utvrđenoj u
        //  prvostupanjskom postupku" — dakle prema onome što je već izračunato.
        if (lijek == PravniLijek.CijelaOdluka)
            return (vps,
                Spoji(napomena,
                    "Pravni lijek protiv cijele odluke: pristojba se plaća prema vrijednosti " +
                    "predmeta spora utvrđenoj u prvostupanjskom postupku (čl. 24)."),
                izvor);

        if (request.PravniLijekIznos <= 0)
            throw new TroskovnikValidationException(
                lijek == PravniLijek.PobijaniDio
                    ? "Kad se pobija samo dio odluke, pristojba se plaća prema vrijednosti " +
                      "pobijanog dijela (čl. 24), pa je taj iznos potreban."
                    : "Kad se pravni lijek podnosi samo protiv odluke o troškovima ili " +
                      "sporednim potraživanjima, pristojba se plaća prema iznosu troškova " +
                      "odnosno sporednih potraživanja (čl. 24), pa je taj iznos potreban.");

        var opis = lijek == PravniLijek.PobijaniDio
            ? $"Pobija se samo dio odluke: vrijednost predmeta spora je vrijednost pobijanog " +
              $"dijela, {Novac(request.PravniLijekIznos)} (čl. 24)."
            : $"Pravni lijek samo protiv odluke o troškovima odnosno sporednim potraživanjima: " +
              $"vrijednost predmeta spora je iznos troškova, {Novac(request.PravniLijekIznos)} (čl. 24).";

        return (request.PravniLijekIznos,
            Spoji(napomena, opis),
            $"{TarifaConstants.NazivZakonaOPristojbama}, čl. 24 — pristojba na pravni lijek");
    }

    private static string Spoji(string? prva, string druga)
        => string.IsNullOrEmpty(prva) ? druga : $"{prva} {druga}";

    /// <summary>Iznos u napomeni piše se hrvatski, isto kao na ispisu troškovnika.</summary>
    private static string Novac(decimal iznos)
        => iznos.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("hr-HR")) + " €";
}
