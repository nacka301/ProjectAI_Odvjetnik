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
    }
}

