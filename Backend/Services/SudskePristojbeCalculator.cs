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
    /// Cijeli obračun sudskih pristojbi za jedan troškovnik — stari trojni oblik
    /// oslobođenja („sve ili ništa"), koji je i dalje u uporabi.
    /// </summary>
    public static PristojbeRezultat Obracunaj(
        decimal vrijednostSpora,
        List<TarifnaStavka> odabrane,
        bool oslobodjen,
        PristojbeOpcije? opcije = null)
        => Obracunaj(vrijednostSpora, odabrane, OslobodjenjeUlaz.Jednostavni(oslobodjen), opcije);

    /// <summary>
    /// Cijeli obračun sudskih pristojbi za jedan troškovnik, s oslobođenjima po
    /// Zakonu o sudskim pristojbama, čl. 11–13.
    ///
    /// <para><b>Čl. 11</b> — potpuno oslobođenje nuli iznose, ali retke ostavlja: „0,00 €"
    /// bez obrazloženja ne razlikuje se od greške u računu. Nuli se sam redak, a ne
    /// osnovica, jer fiksne pristojbe (upravni spor, ovjere) o osnovici ne ovise pa bi
    /// inače preživjele oslobođenje.</para>
    ///
    /// <para><b>Čl. 12</b> — kad podnesak podnosi više osoba, a neke su oslobođene,
    /// neoslobođene plaćaju „samo prema svom dijelu vrijednosti predmeta spora".
    /// Umanjuje se OSNOVICA, ne gotov iznos: Tar. br. 1 je progresivan, pa pristojba na
    /// polovicu VPS-a nije polovica pristojbe.</para>
    ///
    /// <para><b>Čl. 13</b> — kad oslobođena stranka uspije, pristojbu koju bi ona platila
    /// plaća protivna stranka, razmjerno uspjehu. Taj iznos stoji zasebno; obveza nastaje
    /// tek po pravomoćnosti, pa ne ulazi u trošak zastupane stranke.</para>
    /// </summary>
    public static PristojbeRezultat Obracunaj(
        decimal vrijednostSpora,
        List<TarifnaStavka> odabrane,
        OslobodjenjeUlaz oslobodjenje,
        PristojbeOpcije? opcije = null)
    {
        opcije ??= PristojbeOpcije.Zadano;

        var potpuno = SviOslobodjeni(oslobodjenje);
        var vpsNeoslobodjenih = VpsNeoslobodjenih(vrijednostSpora, oslobodjenje);

        var radnje = RazrijesiRadnje(odabrane, opcije);

        var retci = radnje
            .Select(r => SastaviRedak(r.OdredbaId, r.ItemId, r.Rucna, vpsNeoslobodjenih, opcije))
            .ToList();

        retci.Sort((a, b) => Poredak(a).CompareTo(Poredak(b)));

        var ukupno = potpuno ? 0m : retci.Sum(r => r.Iznos);
        if (potpuno)
            retci = [.. retci.Select(r => r with { IznosPoJedinici = 0m, Iznos = 0m })];

        // Čl. 13 se veže uz dio VPS-a koji otpada na oslobođene — pristojba koju bi
        // upravo oni platili. Računa se iz njihova dijela, a ne kao razlika dvaju
        // zaokruženih iznosa, jer je Tar. br. 1 progresivan.
        var naTeretProtustranke = NaTeretProtustranke(
            radnje, vrijednostSpora - vpsNeoslobodjenih, oslobodjenje, opcije);

        return new PristojbeRezultat(
            retci, ukupno, potpuno, vpsNeoslobodjenih, naTeretProtustranke,
            oslobodjenje.Kategorija);
    }

    /// <summary>
    /// Dio vrijednosti predmeta spora na koji se pristojba stvarno obračunava.
    ///
    /// Čl. 12 st. 2: kad se dio pojedine osobe ne može utvrditi, vrijednost se dijeli
    /// brojem osoba. Izričito upisan udio ima prednost pred tom diobom.
    /// </summary>
    private static decimal VpsNeoslobodjenih(decimal vrijednostSpora, OslobodjenjeUlaz oslobodjenje)
    {
        if (SviOslobodjeni(oslobodjenje))
            return 0m;

        if (oslobodjenje.BrojOslobodjenih <= 0)
            return vrijednostSpora;

        if (oslobodjenje.UdioVps is { } udio)
            return Math.Clamp(udio, 0m, vrijednostSpora);

        var brojOsoba = Math.Max(1, oslobodjenje.BrojOsoba);
        var neoslobodjeni = Math.Max(0, brojOsoba - oslobodjenje.BrojOslobodjenih);

        return vrijednostSpora * neoslobodjeni / brojOsoba;
    }

    /// <summary>
    /// Čl. 13 — pristojba na teret protivne stranke, razmjerno uspjehu oslobođene.
    ///
    /// Obračunava se na ISTIM odredbama kao i glavni obračun, samo na dijelu VPS-a koji
    /// otpada na oslobođene. Nula kad uspjeh nije upisan ili kad nitko nije oslobođen.
    /// </summary>
    private static decimal NaTeretProtustranke(
        List<(string OdredbaId, string? ItemId, RucnaPristojba? Rucna)> radnje,
        decimal vpsOslobodjenih,
        OslobodjenjeUlaz oslobodjenje,
        PristojbeOpcije opcije)
    {
        if (oslobodjenje.PostotakUspjeha is not { } postotak || postotak <= 0m)
            return 0m;

        if (vpsOslobodjenih <= 0m)
            return 0m;

        var pristojbaOslobodjenih = radnje
            .Select(r => SastaviRedak(r.OdredbaId, r.ItemId, r.Rucna, vpsOslobodjenih, opcije))
            .Sum(r => r.Iznos);

        return ZaokruziNovac(pristojbaOslobodjenih * Math.Clamp(postotak, 0m, 100m) / 100m);
    }

    /// <summary>Cijela zastupana strana je oslobođena — pristojba je 0.</summary>
    private static bool SviOslobodjeni(OslobodjenjeUlaz oslobodjenje)
        => oslobodjenje.Oslobodjen
           || (oslobodjenje.BrojOslobodjenih > 0
               && oslobodjenje.BrojOslobodjenih >= Math.Max(1, oslobodjenje.BrojOsoba));

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

        // Čl. 7 — polovica se uzima od PROPISANOG iznosa, dakle nakon svih uputa i kapa
        // iz Uredbe („polovica propisanog iznosa pristojbe utvrđene Tarifom"), a
        // zaokružuje se jednom, na kraju: polovica od 546,4516 je 273,23, ne 273,22.
        var umanjeno = UmanjujeSeCl7(odredba, opcije);

        var poJedinici = umanjeno
            ? ZaokruziNovac(Nezaokruzeno(odredbaId, osnovica, verzija, MaxDubinaUpute) * 0.5m)
            : Izracunaj(odredbaId, osnovica, verzija);

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
            PravniIzvor: PravniIzvor(odredba, verzija, umanjeno),
            Automatski: rucna is null,
            Cl7Rezim: odredba.Cl7Rezim,
            UmanjenoZaElektronickiOblik: umanjeno);
    }

    // ── Čl. 7 — umanjenje za elektronički oblik ──────────────────

    /// <summary>
    /// Umanjuje li se ovaj redak na polovicu po čl. 7 Zakona o sudskim pristojbama.
    ///
    /// Tri stavka, tri uvjeta, i svaki se veže uz drugu činjenicu: st. 1 uz oblik
    /// podneska, st. 2 uz dostavu odluke od suda, st. 3 uz oblik prijedloga za upis.
    /// Zato ih ne pokriva jedan prekidač — vidi <see cref="PristojbeOpcije"/>.
    ///
    /// St. 4 nema svoju granu: propušten rok znači neispunjen uvjet, a to je isto što i
    /// isključena oznaka.
    /// </summary>
    private static bool UmanjujeSeCl7(PristojbenaOdredba odredba, PristojbeOpcije opcije)
        // Uredba na tri mjesta sama propisuje polovicu ZA ELEKTRONIČKO PODNOŠENJE
        // (Tar. br. 25 st. 3, 26 st. 6, 31 st. 3). To je isti razlog zbog kojeg umanjuje
        // i čl. 7, pa bi drugo umanjenje spustilo iznos na 25 % propisanog.
        => !odredba.VecImaElektronickoUmanjenje && odredba.Cl7Rezim switch
        {
            Cl7Rezim.St1Podnesak => opcije.PodnesciElektronicki,
            Cl7Rezim.St3Zemljisnoknjizni => opcije.ZkUpisElektronicki,
            Cl7Rezim.St2Odluka =>
                opcije.OdlukeElektronicki?.Contains(odredba.Id) == true,
            _ => false
        };

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

    private static string PravniIzvor(
        PristojbenaOdredba odredba, UredbaVerzija verzija, bool umanjenoCl7)
    {
        var naziv = verzija == UredbaVerzija.Nn37_2023
            ? $"{TarifaConstants.NazivStareUredbeOPristojbama} — čl. 2 NN 50/2026"
            : TarifaConstants.NazivUredbeOPristojbama;

        var izvor = $"{naziv}, {odredba.TarifniBroj}";

        // Prepolovljena pristojba mora na ispisu reći PO ČEMU je prepolovljena, inače
        // se ne razlikuje od greške u računu.
        if (!umanjenoCl7)
            return izvor;

        var stavak = odredba.Cl7Rezim switch
        {
            Cl7Rezim.St1Podnesak => "st. 1 — podnesak u elektroničkom obliku",
            Cl7Rezim.St2Odluka => "st. 2 — odluka dostavljena elektronički, plaćeno u roku od 3 dana",
            Cl7Rezim.St3Zemljisnoknjizni => "st. 3 — zemljišnoknjižni upis predložen elektronički",
            _ => ""
        };

        return $"{izvor} · umanjeno na polovicu: {TarifaConstants.NazivZakonaOPristojbama}, čl. 7 {stavak}";
    }

    private static int Poredak(PristojbenaStavka stavka)
        => Redoslijed.TryGetValue(stavka.OdredbaId, out var i) ? i : int.MaxValue;

    /// <summary>Novac se zaokružuje jednom, na dvije decimale, standardno (ne bankarski).</summary>
    private static decimal ZaokruziNovac(decimal iznos)
        => Math.Round(iznos, 2, MidpointRounding.AwayFromZero);
}
