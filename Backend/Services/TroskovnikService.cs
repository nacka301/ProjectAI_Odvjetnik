using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Odvjetnička tarifa – izračun troškovnika prema Tarifi o nagradama
/// i naknadi troškova za rad odvjetnika (NN 2023).
/// Tbr. 54: Vrijednost boda = 2,00 EUR.
/// </summary>
public sealed class TroskovnikService : ITroskovnikService
{
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

    /// <summary>Tbr. 41. toč. 1 — "tarifne stavke mogu se povisiti za 100%".</summary>
    public const decimal Tbr41MaxPovisenje = 100m;

    /// <summary>Tbr. 41. toč. 2 — "pojedine tarifne stavke odvjetnik može sniziti za 50%".</summary>
    public const decimal Tbr41MaxSnizenje = 50m;

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
        TroskovnikItemRequest stavka, TroskovnikRequest request, TarifnaStavka definicija)
    {
        var povisenje = stavka.Tbr41PovisenjePostotak ?? request.Tbr41PovisenjePostotak;
        var snizenje = stavka.Tbr41SnizenjePostotak ?? request.Tbr41SnizenjePostotak;

        if (povisenje > 0m && snizenje > 0m)
            throw new TroskovnikValidationException(
                $"Stavka \"{definicija.Opis}\" ima istovremeno povišenje ({povisenje} %) i sniženje " +
                $"({snizenje} %) po Tbr. 41. Toč. 1 i toč. 2 polaze od suprotnih razloga, pa se " +
                "na istu stavku ne mogu primijeniti istovremeno.");

        if (povisenje < 0m || povisenje > Tbr41MaxPovisenje)
            throw new TroskovnikValidationException(
                $"Povišenje po Tbr. 41. toč. 1 za stavku \"{definicija.Opis}\" iznosi {povisenje} %. " +
                $"Tarifa dopušta najviše {Tbr41MaxPovisenje} %.");

        if (snizenje < 0m || snizenje > Tbr41MaxSnizenje)
            throw new TroskovnikValidationException(
                $"Sniženje po Tbr. 41. toč. 2 za stavku \"{definicija.Opis}\" iznosi {snizenje} %. " +
                $"Tarifa dopušta najviše {Tbr41MaxSnizenje} %.");

        return (povisenje, snizenje);
    }

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

    // ── Tbr. 7. toč. 1 — razredi prema vrijednosti predmeta spora ──
    //
    // Tarifa propisuje šest fiksnih razreda, zatim tri progresivna u kojima na svaki
    // započeti korak preko donje granice dolazi jedan bod. Tri granice i iznos od
    // 500 bodova pojavljuju se na po dva mjesta — jednom kao gornja granica razreda,
    // jednom kao polazište sljedećeg — pa moraju biti jedna konstanta, ne dva broja.

    private const decimal FiksniRazred1Granica = 332.00m;
    private const decimal FiksniRazred2Granica = 664.00m;
    private const decimal FiksniRazred3Granica = 1_327.00m;
    private const decimal FiksniRazred4Granica = 13_272.00m;
    private const decimal FiksniRazred5Granica = 33_181.00m;

    /// <summary>Gornja granica zadnjeg fiksnog razreda; ujedno polazište prvog progresivnog.</summary>
    private const decimal FiksniRazred6Granica = 66_361.00m;

    private const int FiksniRazred1Bodovi = 25;
    private const int FiksniRazred2Bodovi = 50;
    private const int FiksniRazred3Bodovi = 75;
    private const int FiksniRazred4Bodovi = 100;
    private const int FiksniRazred5Bodovi = 250;

    /// <summary>Bodovi zadnjeg fiksnog razreda; ujedno polazište prvog progresivnog.</summary>
    private const int FiksniRazred6Bodovi = 500;

    /// <summary>Gornja granica prvog progresivnog razreda; ujedno polazište drugog.</summary>
    private const decimal ProgresivniRazred1Granica = 663_614.00m;

    /// <summary>Gornja granica drugog progresivnog razreda; ujedno polazište trećeg.</summary>
    private const decimal ProgresivniRazred2Granica = 1_327_228.00m;

    /// <summary>Iznos svakog započetog koraka unutar pojedinog progresivnog razreda, u eurima.</summary>
    private const decimal ProgresivniRazred1Korak = 133.00m;
    private const decimal ProgresivniRazred2Korak = 265.00m;
    private const decimal ProgresivniRazred3Korak = 664.00m;

    /// <summary>Bodovi na donjoj granici drugog odnosno trećeg progresivnog razreda.</summary>
    private const int ProgresivniRazred2PocetniBodovi = 4991;
    private const int ProgresivniRazred3PocetniBodovi = 7496;

    /// <summary>Tbr. 7. toč. 1 — nagrada ne može prijeći 10.000 bodova.</summary>
    private const int Tbr71MaxBodovi = 10_000;

    /// <summary>
    /// Izračunava broj bodova prema Tbr. 7. toč. 1. na temelju
    /// vrijednosti predmeta spora u EUR.
    /// </summary>
    public static int Tbr71Bodovi(decimal vrijednost)
    {
        if (vrijednost <= 0) return FiksniRazred1Bodovi;

        // Fiksni razredi
        if (vrijednost <= FiksniRazred1Granica) return FiksniRazred1Bodovi;
        if (vrijednost <= FiksniRazred2Granica) return FiksniRazred2Bodovi;
        if (vrijednost <= FiksniRazred3Granica) return FiksniRazred3Bodovi;
        if (vrijednost <= FiksniRazred4Granica) return FiksniRazred4Bodovi;
        if (vrijednost <= FiksniRazred5Granica) return FiksniRazred5Bodovi;
        if (vrijednost <= FiksniRazred6Granica) return FiksniRazred6Bodovi;

        // Progresivni razredi
        if (vrijednost <= ProgresivniRazred1Granica)
        {
            var koraci = (int)Math.Ceiling((vrijednost - FiksniRazred6Granica) / ProgresivniRazred1Korak);
            return FiksniRazred6Bodovi + koraci;
        }

        if (vrijednost <= ProgresivniRazred2Granica)
        {
            var koraci = (int)Math.Ceiling((vrijednost - ProgresivniRazred1Granica) / ProgresivniRazred2Korak);
            return ProgresivniRazred2PocetniBodovi + koraci;
        }

        // Preko 1.327.228,00 EUR
        var koraciPrekoGornjeGranice =
            (int)Math.Ceiling((vrijednost - ProgresivniRazred2Granica) / ProgresivniRazred3Korak);

        return Math.Min(ProgresivniRazred3PocetniBodovi + koraciPrekoGornjeGranice, Tbr71MaxBodovi);
    }

    /// <summary>
    /// Katalog tarifnih stavki. Podaci žive u <see cref="TarifaKatalog"/>; ovdje ostaje
    /// samo pristupna točka, jer je katalog dio javnog sučelja servisa.
    /// </summary>
    public static IReadOnlyList<TarifnaStavka> DohvatiKatalog() => TarifaKatalog.Sve;

    // ── Instancijski pristup istoj logici (DIP) ───────────────────
    //
    // Kontroler ne smije ovisiti o konkretnoj klasi, pa katalog i bodove Tbr. 7. toč. 1.
    // dobiva kroz ITroskovnikService. Statičke inačice ostaju jer ih izravno zove
    // 35 mjesta u testovima Tarife.
    //
    // Implementacija mora biti eksplicitna: C# ne dopušta statičku i instancijsku
    // metodu istog potpisa u istoj klasi (CS0111).

    IReadOnlyList<TarifnaStavka> ITroskovnikService.DohvatiKatalog() => DohvatiKatalog();

    int ITroskovnikService.Tbr71Bodovi(decimal vrijednost) => Tbr71Bodovi(vrijednost);

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

    public TroskovnikResponse Izracunaj(TroskovnikRequest request)
    {
        var katalogPoId = TarifaKatalog.Sve.ToDictionary(s => s.Id);
        var odabraneDefinicije = ProvjeriIRazrijesiDefinicije(request, katalogPoId);
        var kontekst = RazrijesiKontekst(request, odabraneDefinicije);

        var stavkeRetci = IzracunajRetke(request, katalogPoId, kontekst);
        var zbrojevi = IzracunajZbrojeve(request, stavkeRetci, odabraneDefinicije, kontekst);

        return SastaviOdgovor(request, stavkeRetci, zbrojevi, kontekst);
    }

    /// <summary>
    /// Provjerava svaku traženu stavku i vraća njezinu definiciju iz kataloga.
    ///
    /// Nepoznata stavka i nevaljana količina odbijaju se odmah — prije se takva stavka
    /// tiho preskakala i nestajala iz troškovnika bez ijedne poruke.
    /// </summary>
    private static List<TarifnaStavka> ProvjeriIRazrijesiDefinicije(
        TroskovnikRequest request, Dictionary<string, TarifnaStavka> katalogPoId)
    {
        var odabraneDefinicije = new List<TarifnaStavka>();

        foreach (var stavka in request.Stavke)
        {
            if (!katalogPoId.TryGetValue(stavka.ItemId, out var definicija))
                throw new TroskovnikValidationException(
                    $"Nepoznata tarifna stavka: \"{stavka.ItemId}\". Nije pronađena u katalogu.");

            if (stavka.Kolicina <= 0)
                throw new TroskovnikValidationException(
                    $"Stavka \"{definicija.Opis}\" ima količinu {stavka.Kolicina}. Količina mora biti veća od 0.");

            odabraneDefinicije.Add(definicija);
        }

        return odabraneDefinicije;
    }

    /// <summary>
    /// Razrješava parametre predmeta koji vrijede za cijeli troškovnik, prije nego se
    /// izračuna ijedna stavka.
    ///
    /// U kaznenom se postupku sve veže na zapriječenu kaznu (Tbr. 4. toč. 1), nikad na
    /// vrijednost predmeta spora. VPS je institut parničnog postupka, pa se u kaznenom
    /// toku ne koristi ni za bodove ni za sudsku pristojbu. Jedina iznimka je Tbr. 4.
    /// toč. 1 al. 8 (prekršaj s oduzimanjem), koji ide po Tbr. 7. toč. 1 prema vrijednosti
    /// ODUZETOG PREDMETA — to se računa zasebno, po pojedinoj stavci.
    /// </summary>
    private static KontekstIzracuna RazrijesiKontekst(
        TroskovnikRequest request, List<TarifnaStavka> odabraneDefinicije)
    {
        var jeSamoKazneni = odabraneDefinicije.Count > 0
            && odabraneDefinicije.TrueForAll(d => d.Kategorija == TarifaKatalog.KategorijaKazneni);

        var vrijednostSpora = jeSamoKazneni ? 0m : EfektivniVps(request);

        // Osnovica na koju upućuju Tbr. 8. toč. 1, 3, 4 i Tbr. 9. toč. 1–6:
        // "Tbr. 7. toč. 1., 4., 7. i 8." — četiri varijante iste osnovice, ne četiri iznosa.
        // U kaznenom postupku vrijednost predmeta spora ne postoji, pa se osnovica ne razrješava.
        var osnovica = jeSamoKazneni
            ? new OsnovicaRezultat(Tbr71Bodovi(0m), "", false)
            : IzracunajOsnovicu(request);

        return new KontekstIzracuna(
            osnovica, vrijednostSpora, jeSamoKazneni, RazrijesiKazneniRazred(request, odabraneDefinicije));
    }

    /// <summary>
    /// Tbr. 4. toč. 1 — bodovi razreda prema zapriječenoj kazni.
    ///
    /// Kaznene stavke bez toga nemaju osnovicu, pa se kazna traži samo kad je stvarno treba.
    /// Vraća 0 kad nijedna odabrana stavka ne ovisi o zapriječenoj kazni.
    /// </summary>
    private static int RazrijesiKazneniRazred(
        TroskovnikRequest request, List<TarifnaStavka> odabraneDefinicije)
    {
        if (!odabraneDefinicije.Exists(d => d.KazneniBase))
            return 0;

        if (string.IsNullOrWhiteSpace(request.ZaprijecenaKazna))
            throw new TroskovnikValidationException(
                "Odabrane su stavke iz kaznenog postupka, a nije odabrana zapriječena kazna. " +
                "Tbr. 4. toč. 1 nagradu veže isključivo uz visinu zapriječene kazne.");

        if (!ZaprijecenaKaznaTiers.Bodovi.TryGetValue(request.ZaprijecenaKazna, out var razredBodovi))
            throw new TroskovnikValidationException(
                $"Nepoznata zapriječena kazna: \"{request.ZaprijecenaKazna}\". " +
                $"Dopuštene vrijednosti: {string.Join(", ", ZaprijecenaKaznaTiers.Bodovi.Keys)}.");

        return razredBodovi;
    }

    /// <summary>Sastavlja retke troškovnika, jedan po odabranoj stavci.</summary>
    private static List<TroskovnikLineItem> IzracunajRetke(
        TroskovnikRequest request,
        Dictionary<string, TarifnaStavka> katalogPoId,
        KontekstIzracuna kontekst)
    {
        var stavkeRetci = new List<TroskovnikLineItem>();
        var redniBroj = 0;

        foreach (var stavka in request.Stavke)
        {
            var definicija = katalogPoId[stavka.ItemId];

            var bodovi = BodoviZaStavku(definicija, request, kontekst);

            // Zaštitna mreža: nijedna stavka ne smije završiti na nula bodova.
            // Nula je u troškovniku neupotrebljiva — izgleda kao valjan iznos, a nije.
            if (bodovi <= 0)
                throw new TroskovnikValidationException(
                    $"Stavka \"{definicija.Opis}\" ({definicija.TarifniBroj}) daje 0 bodova. " +
                    "Provjerite unesene parametre predmeta.");

            redniBroj++;

            var ukupnoStavke = UkupnoBodovaZaStavku(definicija, bodovi, stavka.Kolicina);

            // ── Tbr. 41 po POJEDINOJ stavci ──
            // Toč. 2 kaže "POJEDINE tarifne stavke odvjetnik može sniziti za 50%" —
            // riječ "pojedine" stoji samo ondje, pa se prilagodba veže uz stavku.
            var (tbr41Povisenje, tbr41Snizenje) = RazrijesiTbr41(stavka, request, definicija);
            var tbr41BodoviStavke = ukupnoStavke * Tbr41Udio(tbr41Povisenje, tbr41Snizenje);

            stavkeRetci.Add(new TroskovnikLineItem
            {
                Rbr = redniBroj,
                ItemId = definicija.Id,
                Opis = definicija.Opis,
                TarifniBroj = definicija.TarifniBroj,
                PravniIzvor = definicija.PravniIzvor,
                Kolicina = stavka.Kolicina,
                BodovaPoJedinici = bodovi,
                UkupnoBodova = ukupnoStavke,
                // Redak se prikazuje u eurima, pa se ovdje zaokružuje. Zbrojevi niže
                // računaju se iz nezaokruženih bodova, da se greška ne akumulira.
                IznosEur = ZaokruziNovac(ukupnoStavke * TarifaConstants.BodValue),
                IsJednokratna = definicija.JednokratnaNagrada,
                Tbr41PovisenjePostotak = tbr41Povisenje,
                Tbr41SnizenjePostotak = tbr41Snizenje,
                Tbr41Bodovi = tbr41BodoviStavke,
                Tbr41PravniIzvor = Tbr41Izvor(tbr41Povisenje, tbr41Snizenje)
            });
        }

        return stavkeRetci;
    }

    /// <summary>
    /// Bodovi jedne stavke prije množenja količinom.
    ///
    /// Tri su izvora, i međusobno se isključuju: prekršaj s oduzimanjem predmeta,
    /// stavka vezana uz zapriječenu kaznu, te obična stavka koja ide po osnovici.
    /// </summary>
    private static decimal BodoviZaStavku(
        TarifnaStavka definicija, TroskovnikRequest request, KontekstIzracuna kontekst)
    {
        if (definicija.PrekrsajOduzimanje)
        {
            if (!request.IsOduzimanjePrekrsaj)
                // Tbr. 4. toč. 1 al. 7 — "za obranu u prekršajnom postupku 100 bodova".
                return PrekrsajBezOduzimanjaBodovi;

            // Tbr. 4. toč. 1 al. 8 — prekršaj s oduzimanjem ide po Tbr. 7. toč. 1
            // prema vrijednosti oduzetog predmeta odnosno imovinske koristi.
            if (request.VrijednostOduzimanja <= 0)
                throw new TroskovnikValidationException(
                    "Uključeno je oduzimanje predmeta, a nije unesena vrijednost oduzetog " +
                    "predmeta odnosno imovinske koristi. Tbr. 4. toč. 1 al. 8 nagradu veže " +
                    "uz tu vrijednost po Tbr. 7. toč. 1.");

            return Tbr71Bodovi(request.VrijednostOduzimanja);
        }

        if (definicija.KazneniBase)
            return IzracunajKazneneBodove(definicija, kontekst.KazneniRazredBodovi, request);

        return IzracunajBodove(definicija, kontekst.Osnovica);
    }

    /// <summary>Tbr. 4. toč. 1 al. 7 — "za obranu u prekršajnom postupku 100 bodova".</summary>
    private const decimal PrekrsajBezOduzimanjaBodovi = 100m;

    /// <summary>
    /// Zbraja retke u ukupne iznose troškovnika: povišenje po Tbr. 40, prilagodbe po
    /// Tbr. 41, PDV i sudsku pristojbu.
    /// </summary>
    private static Zbrojevi IzracunajZbrojeve(
        TroskovnikRequest request,
        List<TroskovnikLineItem> stavkeRetci,
        List<TarifnaStavka> odabraneDefinicije,
        KontekstIzracuna kontekst)
    {
        var osnovicaBodova = stavkeRetci.Sum(redak => redak.UkupnoBodova);

        // ── Tbr. 40. toč. 1 — zastupanje više osoba ──
        var povisenjePostotak = PovisenjeViseOsobaPostotak(request.BrojZastupanihOsoba);
        var povisenjeBodovi = osnovicaBodova * povisenjePostotak;

        // ── Tbr. 41 — zbroj prilagodbi po stavkama ──
        // Doprinos je ADITIVAN na ukupan iznos, kao i Tbr. 40, iako se računa po stavci.
        // Da je umjesto toga multiplikativan (osnovica' = Σ stavka × (1 + udio)), Tbr. 40
        // bi se složio na već prilagođenu osnovicu i ukupan bi se iznos promijenio —
        // vidi TarifaTbr41PoStavciTests.RegresijaSaTbr40_OstajeAditivna.
        // Sudska pristojba se ne dira jer nije odvjetnička nagrada.
        var tbr41Bodovi = stavkeRetci.Sum(redak => redak.Tbr41Bodovi);

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
        var obracunajPdv = request.IsPdvUkljucen ?? request.HasVat;
        if (obracunajPdv)
            pdv = ZaokruziNovac(ukupnoEur * TarifaConstants.PdvRate);

        // Sudska pristojba je drugi propis (Uredba NN 37/2023) i cijela se računa ondje.
        var pristojba = SudskePristojbeCalculator.Obracunaj(
            kontekst.VrijednostSpora, odabraneDefinicije, request.IsOslobodjenPristojbi);

        var odvjetnickiTrosak = ukupnoEur + pdv;
        var sveukupniTrosak = odvjetnickiTrosak + pristojba.Iznos;

        return new Zbrojevi(
            ukupnoBodova, povisenjePostotak, povisenjeBodovi, tbr41Bodovi, ukupnoEur,
            bodoviPoOsobi, nagradaPoOsobi, pdv, pristojba.Osnova, pristojba.Iznos, sveukupniTrosak,
            brojOsoba, obracunajPdv);
    }

    /// <summary>Preslikava zahtjev i izračunate zbrojeve u odgovor koji ide klijentu.</summary>
    private static TroskovnikResponse SastaviOdgovor(
        TroskovnikRequest request,
        List<TroskovnikLineItem> stavkeRetci,
        Zbrojevi zbrojevi,
        KontekstIzracuna kontekst)
    {
        return new TroskovnikResponse
        {
            Stavke = stavkeRetci,
            UkupnoBodova = zbrojevi.UkupnoBodova,
            BrojZastupanihOsoba = zbrojevi.BrojZastupanihOsoba,
            PovisenjeViseOsobaPostotak = zbrojevi.PovisenjePostotak,
            OsnovicaTbr7 = request.OsnovicaTbr7,
            OsnovicaBodovi = kontekst.Osnovica.Bodovi,
            OsnovicaPravniIzvor = kontekst.Osnovica.PravniIzvor,
            RazinaSuda = request.RazinaSuda,
            Tbr45Nacin = request.Tbr45Nacin,
            Tbr41PovisenjePostotak = Math.Clamp(request.Tbr41PovisenjePostotak, 0m, 100m),
            Tbr41SnizenjePostotak = Math.Clamp(request.Tbr41SnizenjePostotak, 0m, 50m),
            Tbr41Bodovi = zbrojevi.Tbr41Bodovi,
            PovisenjeViseOsobaBodovi = zbrojevi.PovisenjeBodovi,
            BodoviPoOsobi = zbrojevi.BodoviPoOsobi,
            NagradaPoOsobi = zbrojevi.NagradaPoOsobi,
            VrijednostSpora = kontekst.VrijednostSpora,
            IsSamoKazneni = kontekst.JeSamoKazneni,
            UkupnoEurBezPdv = zbrojevi.UkupnoEur,
            Pdv = zbrojevi.Pdv,
            UkupnoEurSPdv = zbrojevi.UkupnoEur + zbrojevi.Pdv,
            Klijent = request.Klijent,
            // Ugniježđeni odvjetnik iz zahtjeva preslikava se u stranku odgovora
            // (naziv → ime), radi spojivosti sa starijim oblikom odgovora.
            Odvjetnik = request.Odvjetnik is null
                ? null
                : new TroskovnikParty
                {
                    Ime = request.Odvjetnik.Naziv ?? "",
                    Oib = request.Odvjetnik.Oib,
                    Adresa = request.Odvjetnik.Adresa
                },
            // Protustranka se preuzima iz ugniježđenih stranaka, ako su poslane.
            Protustranka = request.Stranke?.Protustranka is null
                ? null
                : new TroskovnikParty
                {
                    Ime = request.Stranke.Protustranka.Naziv ?? "",
                    Oib = request.Stranke.Protustranka.Oib,
                    Adresa = request.Stranke.Protustranka.Adresa
                },
            ProtustrankaPolozaj = request.Stranke?.Protustranka?.Polozaj ?? request.ProtustrankaPolozaj,
            IsPdvUkljucen = zbrojevi.ObracunajPdv,
            HasVat = request.HasVat,
            OpisPredmeta = request.OpisPredmeta,
            // Podaci o predmetu
            Sud = request.Predmet?.Sud,
            BrojPredmeta = request.Predmet?.PoslovniBroj ?? request.CaseId,
            DatumIzrade = DateTime.UtcNow,
            // Ugniježđeni objekti (novi oblik) — prosljeđuju se nepromijenjeni sučelju.
            OdvjetnikInfo = request.Odvjetnik,
            PredmetInfo = request.Predmet,
            StrankeInfo = request.Stranke,
            // Kazneni parametri vraćeni natrag u odgovoru
            ZaprijecenaKazna = request.ZaprijecenaKazna,
            IsPoSluzbenojDuznosti = request.IsPoSluzbenojDuznosti,
            IsNaTeretProracuna = request.IsNaTeretProracuna,
            IsZastupnikDjeteta = request.IsZastupnikDjeteta,
            IsStjecaj = request.IsStjecaj,
            NapomenaStjecaj = request.IsStjecaj ? NapomenaZaStjecaj : null,
            IsOduzimanjePrekrsaj = request.IsOduzimanjePrekrsaj,
            VrijednostOduzimanja = request.VrijednostOduzimanja,
            // Sudske pristojbe
            SudskaPristojba = zbrojevi.SudskaPristojba,
            PristojbaOsnova = zbrojevi.PristojbaOsnova,
            IsOslobodjenPristojbi = request.IsOslobodjenPristojbi,
            SveukupniTrosak = zbrojevi.SveukupniTrosak
        };
    }

    /// <summary>
    /// Parametri predmeta koji vrijede za cijeli troškovnik, razriješeni jednom prije
    /// nego se izračuna ijedna stavka.
    /// </summary>
    private sealed record KontekstIzracuna(
        OsnovicaRezultat Osnovica,
        decimal VrijednostSpora,
        bool JeSamoKazneni,
        int KazneniRazredBodovi);

    /// <summary>Izvedene ukupne vrijednosti troškovnika, izračunate iz gotovih redaka.</summary>
    private sealed record Zbrojevi(
        decimal UkupnoBodova,
        decimal PovisenjePostotak,
        decimal PovisenjeBodovi,
        decimal Tbr41Bodovi,
        decimal UkupnoEur,
        decimal BodoviPoOsobi,
        decimal NagradaPoOsobi,
        decimal Pdv,
        PristojbaOsnova PristojbaOsnova,
        decimal SudskaPristojba,
        decimal SveukupniTrosak,
        int BrojZastupanihOsoba,
        bool ObracunajPdv);

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

    /// <summary>
    /// Za obične stavke: osnovica × količina.
    ///
    /// Za jednokratne nagrade (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) osnovica pokriva
    /// cijeli prvostupanjski postupak i NE množi se količinom. Ako tarifni broj
    /// propisuje naknadu za daljnje radnje ("Za svaku daljnju radnju... 50 bodova"),
    /// dodaje se samo za radnje preko uključenog broja.
    /// </summary>
    private static decimal UkupnoBodovaZaStavku(TarifnaStavka definicija, decimal bodovi, int kolicina)
    {
        if (!definicija.JednokratnaNagrada)
            return bodovi * kolicina;

        var ukupno = bodovi;

        if (definicija.UkljuceneRadnje.HasValue && definicija.DaljnjaRadnjaBodovi.HasValue)
        {
            var daljnjeRadnje = Math.Max(0, kolicina - definicija.UkljuceneRadnje.Value);
            ukupno += daljnjeRadnje * definicija.DaljnjaRadnjaBodovi.Value;
        }

        return ukupno;
    }

    /// <summary>
    /// Lanac se računa u decimalu, bez zaokruživanja između koraka. Tarifa
    /// zaokruživanje bodova nigdje ne propisuje, a Math.Ceiling na svakom koraku
    /// sustavno je podizao iznose.
    /// </summary>
    private static decimal IzracunajBodove(TarifnaStavka definicija, OsnovicaRezultat osnovica)
    {
        decimal bodovi;

        if (definicija.FiksniBody.HasValue)
        {
            // Iznos koji Tarifa određuje apsolutno (npr. Tbr. 7. toč. 10 = 250 bodova) —
            // ne ovisi ni o vrijednosti spora ni o osnovici.
            bodovi = definicija.FiksniBody.Value;
        }
        else if (osnovica.FiksnoPoRadnji)
        {
            // Tbr. 45 uz način "fiksno po radnji": svaka poduzeta radnja nosi puni iznos
            // iz Tbr. 45. toč. 1–3. Postoci i stropovi iz Tbr. 8./9. se NE primjenjuju —
            // to je upravo ono po čemu se to čitanje razlikuje od "kao osnovica".
            return osnovica.Bodovi;
        }
        else if (definicija.Tbr71Postotak.HasValue)
        {
            bodovi = osnovica.Bodovi * definicija.Tbr71Postotak.Value;
            if (definicija.Tbr71Povecanje.HasValue)
                bodovi *= 1 + definicija.Tbr71Povecanje.Value;
        }
        else
        {
            bodovi = osnovica.Bodovi;
        }

        return ClampBodovi(bodovi, definicija);
    }

    /// <summary>
    /// Izračunava bodove za stavke s KazneniBase = true.
    /// Koristi ZaprijecenaKaznaTiers lookup i primjenjuje modifikatore:
    ///   - KazneniPovecanje (npr. +50% za žalbu)
    ///   - IsZastupnikDjeteta → 70% od Tbr. 4.8 (= 70% × 50% = 35% baze)
    ///   - IsPoSluzbenojDuznosti / IsNaTeretProracuna → 50% umanjenje
    /// </summary>
    private static decimal IzracunajKazneneBodove(TarifnaStavka definicija, int tierBodovi, TroskovnikRequest req)
    {
        decimal bodovi = tierBodovi;

        // Povišenje vezano uz tarifni broj (npr. +50% za žalbu protiv presude, Tbr. 5.1).
        //
        // Tbr. 4. toč. 2 al. 3 propisuje drukčiji postotak za raspravu pred drugim i
        // trećim stupnjem kad je zapriječena kazna dugotrajni zatvor — tada se koristi
        // KazneniPovecanjeDugotrajni umjesto redovnog povišenja.
        var jeDugotrajni = req.ZaprijecenaKazna == ZaprijecenaKaznaTiers.DugotrajniZatvor;
        var povecanje = jeDugotrajni && definicija.KazneniPovecanjeDugotrajni.HasValue
            ? definicija.KazneniPovecanjeDugotrajni
            : definicija.KazneniPovecanje;

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

        return ClampBodovi(bodovi, definicija);
    }

    private static decimal ClampBodovi(decimal bodovi, TarifnaStavka definicija)
    {
        if (definicija.MaxBodova.HasValue && bodovi > definicija.MaxBodova.Value)
            bodovi = definicija.MaxBodova.Value;
        if (definicija.MinBodova.HasValue && bodovi < definicija.MinBodova.Value)
            bodovi = definicija.MinBodova.Value;

        return bodovi;
    }
}
