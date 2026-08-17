using Backend.Models;
using Backend.Services.Pristojbe;

namespace Backend.Services;

/// <summary>
/// Obračun sudskih pristojbi po Uredbi o Tarifi sudskih pristojbi.
///
/// Uredba naplaćuje po RADNJI, a ne po predmetu: tužba (Tar. br. 1), presuda
/// (Tar. br. 2) i žalba (Tar. br. 3) u istoj su parnici tri zasebne pristojbe. Zato je
/// ishod lista redaka, a ne jedan broj.
///
/// Kalkulator zna dvije verzije Uredbe istodobno. NN 50/2026 je na snazi od 1.6.2026.,
/// ali čl. 2 zadržava NN 37/2023 za dva slučaja, pa jedan troškovnik smije nositi retke
/// po objema — vidi <see cref="RazrijesiVerziju"/>.
///
/// Same iznose ne drži ovdje: tablice su u <see cref="PristojbeTarifaNn50_2026"/> i
/// <see cref="PristojbeTarifaNn37_2023"/>, a veza s odvjetničkom tarifom u
/// <see cref="PristojbeneRadnjeMapa"/>.
/// </summary>
public static class SudskePristojbeCalculator
{
    /// <summary>Dublje od ovoga upute „polovica od…" u Uredbi ne idu; čuvar od ciklusa.</summary>
    private const int MaxDubinaUpute = 8;

    /// <summary>Redoslijed prikaza — redom kojim odredbe stoje u Prilogu Uredbe.</summary>
    private static readonly Dictionary<string, int> Redoslijed =
        PristojbeTarifaNn50_2026.Sve
            .Select((o, i) => (o.Id, i))
            .ToDictionary(p => p.Id, p => p.i);

    /// <summary>
    /// Cijeli obračun sudskih pristojbi za jedan troškovnik.
    ///
    /// Oslobođenje po čl. 11 Zakona nuli iznose, ali retke ostavlja — iz istog razloga
    /// iz kojeg je i prije zadržavalo osnovu obračuna: „0,00 €" bez obrazloženja ne
    /// razlikuje se od greške u računu.
    /// </summary>
    public static PristojbeRezultat Obracunaj(
        decimal vrijednostSpora,
        List<TarifnaStavka> odabrane,
        bool oslobodjen,
        PristojbeOpcije? opcije = null)
    {
        opcije ??= PristojbeOpcije.Zadano;

        var retci = RazrijesiRadnje(odabrane, opcije)
            .Select(r => SastaviRedak(r.OdredbaId, r.ItemId, r.Rucna, vrijednostSpora, opcije))
            .ToList();

        retci.Sort((a, b) => Poredak(a).CompareTo(Poredak(b)));

        var ukupno = oslobodjen ? 0m : retci.Sum(r => r.Iznos);
        if (oslobodjen)
            retci = [.. retci.Select(r => r with { IznosPoJedinici = 0m, Iznos = 0m })];

        return new PristojbeRezultat(retci, ukupno, oslobodjen);
    }

    /// <summary>
    /// Iznos jedne odredbe uz zadanu osnovicu i verziju Uredbe.
    ///
    /// Zaokružuje se jednom, na kraju cijelog lanca uputa: „polovica pristojbe iz st. 1"
    /// računa se iz nezaokruženog st. 1, pa polovica od 546,4516 daje 273,23, a ne 273,22.
    /// AwayFromZero, ne bankarsko — 38,2258 → 38,23.
    /// </summary>
    public static decimal Izracunaj(string odredbaId, decimal osnovica, UredbaVerzija verzija)
        => ZaokruziNovac(Nezaokruzeno(odredbaId, osnovica, verzija, MaxDubinaUpute));

    /// <summary>Odredba po ključu iz tablice tražene verzije; null kad je ta verzija nema.</summary>
    public static PristojbenaOdredba? Odredba(string odredbaId, UredbaVerzija verzija)
    {
        var tablica = verzija == UredbaVerzija.Nn37_2023
            ? PristojbeTarifaNn37_2023.PoId
            : PristojbeTarifaNn50_2026.PoId;

        return tablica.TryGetValue(odredbaId, out var odredba) ? odredba : null;
    }

    // ── Koje se radnje uopće naplaćuju ───────────────────────────

    /// <summary>
    /// Iz odabranih tarifnih stavaka i ručnih dodataka izvodi popis radnji koje se
    /// naplaćuju.
    ///
    /// Ista se odredba naplaćuje samo jednom, i kad na nju vode dvije tarifne stavke:
    /// „tužba" i „tužba zbog smetanja posjeda" u istom troškovniku i dalje su jedan
    /// podnesak. Ručno dodan redak nadjačava automatski izvedeni za istu odredbu, jer
    /// nosi osnovicu i količinu koje je korisnik unio namjerno.
    /// </summary>
    private static List<(string OdredbaId, string? ItemId, RucnaPristojba? Rucna)> RazrijesiRadnje(
        List<TarifnaStavka> odabrane, PristojbeOpcije opcije)
    {
        var iskljucene = opcije.Iskljucene ?? new HashSet<string>();
        var rezultat = new List<(string, string?, RucnaPristojba?)>();
        var vidjene = new HashSet<string>();

        foreach (var rucna in opcije.Dodane ?? [])
        {
            if (iskljucene.Contains(rucna.OdredbaId) || !vidjene.Add(rucna.OdredbaId))
                continue;

            rezultat.Add((rucna.OdredbaId, null, rucna));
        }

        foreach (var stavka in odabrane)
        {
            foreach (var odredbaId in PristojbeneRadnjeMapa.Za(stavka.Id))
            {
                if (iskljucene.Contains(odredbaId) || !vidjene.Add(odredbaId))
                    continue;

                rezultat.Add((odredbaId, stavka.Id, null));
            }
        }

        return rezultat;
    }

    private static PristojbenaStavka SastaviRedak(
        string odredbaId,
        string? izvorniItemId,
        RucnaPristojba? rucna,
        decimal vrijednostSpora,
        PristojbeOpcije opcije)
    {
        var verzija = RazrijesiVerziju(odredbaId, izvorniItemId, opcije);

        var odredba = Odredba(odredbaId, verzija)
            ?? throw new TroskovnikValidationException(
                $"Nepoznata odredba Uredbe: \"{odredbaId}\". Ključ mora biti jedan od onih koje "
                + "vraća popis pristojbenih odredbi; ručno dodana pristojba s nepoznatim ključem "
                + "tiho bi ispala iz troškovnika.");

        var osnovica = rucna?.Osnovica ?? vrijednostSpora;
        var kolicina = Math.Max(1, rucna?.Kolicina ?? 1);

        var poJedinici = Izracunaj(odredbaId, osnovica, verzija);

        // Fiksni iznosi ne ovise o osnovici, pa je ne prikazuj — inače bi uz upravni
        // spor od 110,00 € stajao VPS od 50.000 € i sugerirao da je iznos iz njega izveden.
        var prikazanaOsnovica = KoristiOsnovicu(odredba.Izracun) ? osnovica : 0m;

        return new PristojbenaStavka(
            OdredbaId: odredbaId,
            TarifniBroj: odredba.TarifniBroj,
            Poglavlje: odredba.Poglavlje,
            Opis: odredba.Opis,
            Osnovica: prikazanaOsnovica,
            Kolicina: kolicina,
            IznosPoJedinici: poJedinici,
            Iznos: poJedinici * kolicina,
            Verzija: verzija,
            PravniIzvor: PravniIzvor(odredba, verzija),
            Automatski: rucna is null);
    }

    // ── Čl. 2 — koja se Uredba primjenjuje na ovaj redak ─────────

    /// <summary>
    /// Čl. 2 NN 50/2026: „Pristojba za odgovor na tužbu podnesen prije stupanja na
    /// snagu ove Uredbe i pristojba u upravnom sporu pokrenutom prije stupanja na snagu
    /// ove Uredbe utvrđuju se prema dosadašnjim propisima."
    ///
    /// Dva slučaja, ni jedan više. Verzija se bira po RETKU, ne po troškovniku, pa
    /// predmet u kojem je odgovor na tužbu podnesen u ožujku, a žalba u srpnju 2026.
    /// dobiva jedan redak po staroj i jedan po novoj Uredbi — što je upravo ono što
    /// prijelazna odredba proizvodi.
    /// </summary>
    private static UredbaVerzija RazrijesiVerziju(
        string odredbaId, string? izvorniItemId, PristojbeOpcije opcije)
    {
        var jeOdgovorNaTuzbu = izvorniItemId is not null
            && PristojbeneRadnjeMapa.ItemiOdgovorNaTuzbu.Contains(izvorniItemId);

        var jeUpravniSpor = Odredba(odredbaId, UredbaVerzija.Nn50_2026)?.Poglavlje
            == PristojbaPoglavlje.UpravniSpor;

        var prijelazni =
            (jeOdgovorNaTuzbu && PrijeNoveUredbe(opcije.DatumPodnosenjaOdgovoraNaTuzbu))
            || (jeUpravniSpor && PrijeNoveUredbe(opcije.DatumPokretanjaUpravnogSpora));

        // Stara je tablica namjerno djelomična — drži samo ono što čl. 2 traži. Odredbu
        // koje u njoj nema stara Uredba nije ni regulirala na način na koji je čl. 2
        // zadržava, pa takav redak ostaje na novoj Uredbi umjesto da ispadne iz zbroja.
        if (prijelazni && Odredba(odredbaId, UredbaVerzija.Nn37_2023) is not null)
            return UredbaVerzija.Nn37_2023;

        return UredbaVerzija.Nn50_2026;
    }

    private static bool PrijeNoveUredbe(DateOnly? datum)
        => datum is { } d && d < UredbaGranice.NovaUredbaNaSnazi;

    // ── Izračun ──────────────────────────────────────────────────

    private static decimal Nezaokruzeno(
        string odredbaId, decimal osnovica, UredbaVerzija verzija, int preostalaDubina)
    {
        if (preostalaDubina <= 0)
            throw new InvalidOperationException(
                $"Upute između odredbi vrte se u krug kod „{odredbaId}\".");

        var odredba = Odredba(odredbaId, verzija);
        if (odredba is null)
            return 0m;

        return odredba.Izracun switch
        {
            NePlacaSe => 0m,

            Fiksno f => f.Iznos,

            UdioOd u => Ograniceno(
                Nezaokruzeno(u.OdredbaId, osnovica, verzija, preostalaDubina - 1) * u.Faktor,
                u.Max),

            LjestvicaPostotak lj => PoLjestviciPostotak(lj, osnovica),

            LjestvicaKorak lj => PoLjestviciKorak(lj, osnovica),

            PostotakVrijednosti p => osnovica <= 0
                ? 0m
                : Math.Min(osnovica * p.Stopa, p.Max),

            Stepenasto s => PoStepenicama(s, osnovica),

            _ => throw new NotSupportedException(
                $"Nepoznat način izračuna za odredbu „{odredbaId}\".")
        };
    }

    /// <summary>Tar. br. 1 st. 1 — razredi, pa „baza + stopa × razlika iznad praga", uz kapu.</summary>
    private static decimal PoLjestviciPostotak(LjestvicaPostotak lj, decimal osnovica)
    {
        if (osnovica <= 0)
            return 0m;

        foreach (var razred in lj.Razredi)
        {
            if (osnovica <= razred.DoIznosa)
                return razred.Pristojba;
        }

        return Math.Min(lj.Baza + lj.Stopa * (osnovica - lj.Prag), lj.Max);
    }

    /// <summary>
    /// Tar. br. 5, 11 i 34 — razredi, pa „baza + iznos na svakih ZAPOČETIH korak", uz kapu.
    /// „Započetih" znači zaokruživanje broja koraka naviše: i 1,00 € preko praga nosi
    /// puni korak.
    /// </summary>
    private static decimal PoLjestviciKorak(LjestvicaKorak lj, decimal osnovica)
    {
        if (osnovica <= 0)
            return lj.Razredi.Count > 0 ? 0m : lj.Baza;

        foreach (var razred in lj.Razredi)
        {
            if (osnovica <= razred.DoIznosa)
                return razred.Pristojba;
        }

        var koraka = Math.Ceiling((osnovica - lj.Prag) / lj.Korak);
        return Math.Min(lj.Baza + koraka * lj.PoKoraku, lj.Max);
    }

    /// <summary>Tar. br. 15 — stepenice po broju (etažni dijelovi), ne po novcu.</summary>
    private static decimal PoStepenicama(Stepenasto s, decimal osnovica)
    {
        foreach (var stepenica in s.Stepenice)
        {
            if (osnovica <= stepenica.DoIznosa)
                return stepenica.Pristojba;
        }

        return s.Stepenice.Count > 0 ? s.Stepenice[^1].Pristojba : 0m;
    }

    private static decimal Ograniceno(decimal iznos, decimal? max)
        => max is { } m ? Math.Min(iznos, m) : iznos;

    /// <summary>Ovisi li iznos o osnovici — fiksni iznosi je ne koriste.</summary>
    private static bool KoristiOsnovicu(PristojbaIzracun izracun) => izracun switch
    {
        Fiksno or NePlacaSe => false,
        UdioOd u => Odredba(u.OdredbaId, UredbaVerzija.Nn50_2026) is { } o && KoristiOsnovicu(o.Izracun),
        _ => true
    };

    private static string PravniIzvor(PristojbenaOdredba odredba, UredbaVerzija verzija)
    {
        var naziv = verzija == UredbaVerzija.Nn37_2023
            ? $"{TarifaConstants.NazivStareUredbeOPristojbama} — čl. 2 NN 50/2026"
            : TarifaConstants.NazivUredbeOPristojbama;

        return $"{naziv}, {odredba.TarifniBroj}";
    }

    private static int Poredak(PristojbenaStavka stavka)
        => Redoslijed.TryGetValue(stavka.OdredbaId, out var i) ? i : int.MaxValue;

    /// <summary>Novac se zaokružuje jednom, na dvije decimale, standardno (ne bankarski).</summary>
    private static decimal ZaokruziNovac(decimal iznos)
        => Math.Round(iznos, 2, MidpointRounding.AwayFromZero);
}
