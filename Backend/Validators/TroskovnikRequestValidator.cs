using Backend.Models;
using FluentValidation;

namespace Backend.Validators;

/// <summary>
/// Validation rules for incoming TroskovnikRequest that validate the new nested schema.
/// Registered automatically via AddValidatorsFromAssemblyContaining in Program.cs.
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

        // ── Tbr. 41 — rasponi iz same Tarife ──────────────────────

        RuleFor(x => x.Tbr41PovisenjePostotak)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Tbr. 41. toč. 1 dopušta povišenje tarifnih stavaka najviše za 100 %.");

        RuleFor(x => x.Tbr41SnizenjePostotak)
            .InclusiveBetween(0m, 50m)
            .WithMessage("Tbr. 41. toč. 2 dopušta sniženje tarifnih stavaka najviše za 50 %.");
    }
}

