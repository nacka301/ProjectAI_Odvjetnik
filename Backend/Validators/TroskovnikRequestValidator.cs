using Backend.Models;
using FluentValidation;

namespace Backend.Validators;

/// <summary>
/// UPOZORENJE: OVA SE PRAVILA TRENUTAČNO NE IZVRŠAVAJU.
///
/// Validacijska pravila za TroskovnikRequest. Napisana su, ali ih ništa ne poziva,
/// pa nijedan zahtjev kroz njih ne prolazi. Tri stvari to zajedno uzrokuju:
///
///  1. Program.cs poziva AddValidatorsFromAssemblyContaining, što validator samo
///     REGISTRIRA u DI spremnik. Registriran nije isto što i pozvan.
///  2. Automatsku provjeru u MVC cjevovodu donosi paket FluentValidation.AspNetCore,
///     a Backend.csproj referencira samo FluentValidation.DependencyInjectionExtensions.
///  3. Nijedan kontroler ne traži IValidator&lt;TroskovnikRequest&gt; niti ga ručno
///     pokreće. Jedini koji validator stvarno koristi je ValuationDraftController,
///     i to za IValidator&lt;PropertyInputBase&gt;.
///
/// Posljedica: OIB od tri slova, povišenje po Tbr. 41 od 500 % i stavka bez item_id
/// prolaze ovdje netaknuti. Dio toga TroskovnikService uhvati kasnije i baci
/// TroskovnikValidationException, dio ne uhvati nitko.
///
/// Dva pravila su usto duplicirana u TroskovnikController.ProvjeriZahtjev, s
/// različitim porukama: kontroler kaže „Vrijednost spora ne može biti negativna.",
/// ovdje piše „Vrijednost spora mora biti veća ili jednaka 0.".
///
/// Uključivanje ovih pravila PROMIJENILO BI PONAŠANJE — zahtjevi koji danas prolaze
/// počeli bi vraćati 400 — pa je to svjesna odluka za zaseban zadatak, s testovima,
/// a ne usputna izmjena. Datoteka nema nijedan test.
/// </summary>
public sealed class TroskovnikRequestValidator : AbstractValidator<TroskovnikRequest>
{
    public TroskovnikRequestValidator()
    {
        // Basic fields
        RuleFor(x => x.VrijednostSpora)
            .GreaterThanOrEqualTo(0).WithMessage("Vrijednost spora mora biti veća ili jednaka 0.");

        RuleFor(x => x.Stavke)
            .NotNull().WithMessage("Polje 'stavke' je obavezno.")
            .Must(s => s.Count > 0).WithMessage("Morate odabrati barem jednu stavku.");

        // Validate each stavka
        RuleForEach(x => x.Stavke).ChildRules(stavka =>
        {
            stavka.RuleFor(s => s.ItemId).NotEmpty().WithMessage("stavke[].item_id je obavezan.");
            stavka.RuleFor(s => s.Kolicina).GreaterThan(0).WithMessage("stavke[].kolicina mora biti veća od 0.");
            // Priznati bodovi (rješenje suda o troškovima) — isti raspon stvarno
            // provjerava TroskovnikService.ProvjeriIRazrijesiDefinicije.
            stavka.When(s => s.PriznatiBodovi.HasValue, () =>
            {
                stavka.RuleFor(s => s.PriznatiBodovi!.Value)
                    .GreaterThan(0m).WithMessage("stavke[].priznati_bodovi moraju biti veći od 0.")
                    .LessThanOrEqualTo(10_000m).WithMessage("stavke[].priznati_bodovi mogu biti najviše 10.000.");
            });
        });

        // Odvjetnik: if present, require at least naziv
        When(x => x.Odvjetnik != null, () =>
        {
            RuleFor(x => x.Odvjetnik!.Naziv).NotEmpty().WithMessage("Odvjetnik.naziv je obavezan ako je polje 'odvjetnik' poslan.");
        });

        // Predmet: if present, require at least one identifier: sud or poslovni_broj or CaseId
        When(x => x.Predmet != null, () =>
        {
            RuleFor(x => x.Predmet)
                .Must((req, predmet) => !string.IsNullOrWhiteSpace(predmet!.Sud) || !string.IsNullOrWhiteSpace(predmet!.PoslovniBroj) || !string.IsNullOrWhiteSpace(req.CaseId))
                .WithMessage("Ako pošaljete 'predmet', morate navesti najmanje 'sud' ili 'poslovni_broj', ili poslati 'case_id'.");
        });

        // Stranke: validate nested shape
        When(x => x.Stranke != null, () =>
        {
            When(x => x.Stranke.ZastupanaStranka != null, () =>
            {
                RuleFor(x => x.Stranke!.ZastupanaStranka!.Naziv).NotEmpty().WithMessage("ZastupanaStranka.naziv je obavezan ako je polje 'zastupana_stranka' poslan.");
            });

            When(x => x.Stranke.Protustranka != null, () =>
            {
                RuleFor(x => x.Stranke!.Protustranka!.Naziv).NotEmpty().WithMessage("Protustranka.naziv je obavezan ako je polje 'protustranka' poslan.");
            });
        });

        // Optional: validate OIB format (11 digits) when provided
        When(x => !string.IsNullOrWhiteSpace(x.Odvjetnik?.Oib), () =>
        {
            RuleFor(x => x.Odvjetnik!.Oib).Matches("^\\d{11}$").WithMessage("Odvjetnik.oib mora sadržavati 11 znamenki.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Stranke?.ZastupanaStranka?.Oib), () =>
        {
            RuleFor(x => x.Stranke!.ZastupanaStranka!.Oib).Matches("^\\d{11}$").WithMessage("ZastupanaStranka.oib mora sadržavati 11 znamenki.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Stranke?.Protustranka?.Oib), () =>
        {
            RuleFor(x => x.Stranke!.Protustranka!.Oib).Matches("^\\d{11}$").WithMessage("Protustranka.oib mora sadržavati 11 znamenki.");
        });

        // ── Osnovica po Tbr. 7. / Tbr. 45 ─────────────────────────
        //
        // Nevaljan unos daje GREŠKU, nikad tihi pad na zadanu vrijednost:
        // razlika između razina suda po Tbr. 45 je do peterostruka.

        RuleFor(x => x.OsnovicaTbr7)
            .Must(o => OsnovicaTbr7.Sve.Contains(o))
            .WithMessage($"Nepoznata osnovica. Dopušteno: {string.Join(", ", OsnovicaTbr7.Sve)}.");

        When(x => x.OsnovicaTbr7 == OsnovicaTbr7.Tbr45Neprocjenjiv, () =>
        {
            RuleFor(x => x.RazinaSuda)
                .NotEmpty().WithMessage(
                    "Tbr. 45 zahtijeva razinu suda: 100 bodova pred općinskim, 200 pred " +
                    "županijskim i trgovačkim, 500 pred Vrhovnim sudom RH.")
                .Must(r => r is null || RazinaSuda.Bodovi.ContainsKey(r))
                .WithMessage($"Nepoznata razina suda. Dopušteno: {string.Join(", ", RazinaSuda.Bodovi.Keys)}.");

            RuleFor(x => x.Tbr45Nacin)
                .NotEmpty().WithMessage(
                    "Tbr. 45 zahtijeva način obračuna: Tarifa ne kaže primjenjuju li se postoci " +
                    "iz Tbr. 8 i Tbr. 9 na iznos iz Tbr. 45, pa odabir mora biti izričit.")
                .Must(n => n is null || Tbr45Nacin.Sve.Contains(n))
                .WithMessage($"Nepoznat način. Dopušteno: {string.Join(", ", Tbr45Nacin.Sve)}.");
        });

        // Tbr. 7. toč. 4 al. 2 — petogodišnji iznos
        RuleFor(x => x.RentaIznos)
            .GreaterThanOrEqualTo(0).WithMessage("Iznos rente / zakupa / najma ne može biti negativan.");

        When(x => x.RentaIznos > 0, () =>
        {
            RuleFor(x => x.RentaRazdoblje)
                .Must(r => r is RentaRazdoblje.Mjesecno or RentaRazdoblje.Godisnje)
                .WithMessage(
                    "Uz periodični iznos rente / zakupa / najma morate odabrati razdoblje " +
                    "(mjesecno ili godisnje) da bi se izveo petogodišnji iznos iz Tbr. 7. toč. 4.");
        });

        // ── Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26 ──
        //
        // Ista pravila stvarno izvršava TroskovnikService kroz VpsCalculator; ovdje stoje
        // da se, ako se validator ikad uključi, dvije provjere ne raziđu.

        RuleFor(x => x.VrstaPostupka)
            .Must(v => string.IsNullOrWhiteSpace(v) || VrstaPostupka.Pravila.ContainsKey(v))
            .WithMessage($"Nepoznata vrsta postupka. Dopušteno: {string.Join(", ", VrstaPostupka.Sve)}.");

        When(x => x.VrstaPostupka == VrstaPostupka.Uzdrzavanje, () =>
        {
            RuleFor(x => x.UzdrzavanjeMjesecno)
                .GreaterThan(0).WithMessage(
                    "U postupcima radi uzdržavanja vrijednost predmeta spora je zbroj davanja " +
                    "za tri mjeseca (čl. 15), pa je potreban mjesečni iznos davanja.");
        });

        When(x => x.Zahtjevi.Count > 0, () =>
        {
            RuleFor(x => x.ZahtjeviNacin)
                .Must(n => n is not null && ZahtjeviNacin.Sve.Contains(n))
                .WithMessage($"Uz više zahtjeva (čl. 16) obavezan je način. Dopušteno: {string.Join(", ", ZahtjeviNacin.Sve)}.");

            RuleForEach(x => x.Zahtjevi).ChildRules(z =>
            {
                z.RuleFor(s => s.Iznos)
                    .GreaterThanOrEqualTo(0).WithMessage("Vrijednost zahtjeva po čl. 16 ne može biti negativna.");
            });
        });

        // Čl. 16: kod suparničarstva se zahtjevi ne zbrajaju, pa troškovnik mora znati
        // koji pokriva.
        When(x => x.ZahtjeviNacin == ZahtjeviNacin.Zasebno && x.Zahtjevi.Count > 0, () =>
        {
            RuleFor(x => x.Zahtjevi)
                .Must(z => z.Count(s => s.Primarni) == 1)
                .WithMessage(
                    "Uz suparničarstvo (čl. 16) označite točno jedan zahtjev koji ovaj " +
                    "troškovnik pokriva — vrijednosti se ne zbrajaju.");
        });

        RuleFor(x => x.PravniLijek)
            .Must(p => string.IsNullOrWhiteSpace(p) || PravniLijek.Sve.Contains(p))
            .WithMessage($"Nepoznata vrsta pravnog lijeka. Dopušteno: {string.Join(", ", PravniLijek.Sve)}.");

        When(x => x.PravniLijek is PravniLijek.PobijaniDio or PravniLijek.SamoTroskovi, () =>
        {
            RuleFor(x => x.PravniLijekIznos)
                .GreaterThan(0).WithMessage(
                    "Čl. 24 pristojbu na pravni lijek veže uz vrijednost pobijanog dijela " +
                    "odnosno iznos troškova, pa je taj iznos obavezan.");
        });

        // ── Oslobođenja — Zakon o sudskim pristojbama, čl. 11–13 ──

        RuleFor(x => x.OslobodjenjeKategorija)
            .Must(k => k is null || OslobodjenjeKategorija.Nazivi.ContainsKey(k))
            .WithMessage($"Nepoznata osnova oslobođenja. Dopušteno: {string.Join(", ", OslobodjenjeKategorija.Sve)}.");

        RuleFor(x => x.BrojOslobodjenihOsoba)
            .GreaterThanOrEqualTo(0).WithMessage("Broj oslobođenih osoba ne može biti negativan.")
            .Must((req, broj) => broj <= Math.Max(1, req.BrojZastupanihOsoba))
            .WithMessage(
                "Oslobođenih osoba ne može biti više nego zastupanih — čl. 12 dijeli vrijednost " +
                "predmeta spora među osobama koje podnesak podnose.");

        RuleFor(x => x.UdioUVps)
            .Must(u => u is null || u >= 0m)
            .WithMessage("Dio vrijednosti predmeta spora po čl. 12 ne može biti negativan.");

        RuleFor(x => x.PostotakUspjeha)
            .Must(p => p is null || (p >= 0m && p <= 100m))
            .WithMessage("Omjer uspjeha po čl. 13 mora biti između 0 i 100 %.");

        // ── Tbr. 41 — rasponi iz same Tarife ──────────────────────

        RuleFor(x => x.Tbr41PovisenjePostotak)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Tbr. 41. toč. 1 dopušta povišenje tarifnih stavaka najviše za 100 %.");

        RuleFor(x => x.Tbr41SnizenjePostotak)
            .InclusiveBetween(0m, 50m)
            .WithMessage("Tbr. 41. toč. 2 dopušta sniženje tarifnih stavaka najviše za 50 %.");
    }
}

