using Backend.Models;
using FluentValidation;

namespace Backend.Validators;

// ══════════════════════════════════════════════════════════
//  LOCATION VALIDATOR (shared)
// ══════════════════════════════════════════════════════════

public class LocationValidator : AbstractValidator<LocationInfo>
{
    public LocationValidator()
    {
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Grad je obavezan.")
            .MaximumLength(100);

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("Kvart / naselje je obavezno.")
            .MaximumLength(100);

        RuleFor(x => x.Zip)
            .Matches(@"^\d{5}$").When(x => !string.IsNullOrWhiteSpace(x.Zip))
            .WithMessage("Poštanski broj mora imati 5 znamenki.");
    }
}

// ══════════════════════════════════════════════════════════
//  APARTMENT VALIDATOR
// ══════════════════════════════════════════════════════════

public class ApartmentInputValidator : AbstractValidator<ApartmentInput>
{
    public ApartmentInputValidator()
    {
        RuleFor(x => x.Location).NotNull().SetValidator(new LocationValidator());

        RuleFor(x => x.AreaM2)
            .InclusiveBetween(10, 300)
            .WithMessage("Površina stana mora biti između 10 i 300 m².");

        RuleFor(x => x.RoomCount)
            .InclusiveBetween(1, 10)
            .WithMessage("Broj soba mora biti između 1 i 10.");

        RuleFor(x => x.Floor)
            .IsInEnum()
            .WithMessage("Nepoznata vrijednost za kat.");

        RuleFor(x => x.TotalFloors)
            .InclusiveBetween(1, 50)
            .WithMessage("Ukupan broj katova mora biti između 1 i 50.");

        RuleFor(x => x.TotalFloors)
            .GreaterThanOrEqualTo(x => (int)x.Floor)
            .When(x => (int)x.Floor >= 0 && (int)x.Floor <= 30)
            .WithMessage("Ukupan broj katova ne može biti manji od kata stana.");

        // Warning: No elevator on floor >= 2 (Severity.Warning → passes, but flags it)
        RuleFor(x => x.HasElevator)
            .Must(hasElevator => hasElevator)
            .When(x => (int)x.Floor >= 2)
            .WithSeverity(Severity.Warning)
            .WithMessage("Stan je na visokom katu bez lifta — to utječe na vrijednost.");

        RuleFor(x => x.Condition).IsInEnum();
    }
}

// ══════════════════════════════════════════════════════════
//  HOUSE VALIDATOR
// ══════════════════════════════════════════════════════════

public class HouseInputValidator : AbstractValidator<HouseInput>
{
    public HouseInputValidator()
    {
        RuleFor(x => x.Location).NotNull().SetValidator(new LocationValidator());

        RuleFor(x => x.LivingAreaM2)
            .GreaterThan(20)
            .WithMessage("Stambena površina kuće mora biti veća od 20 m².");

        RuleFor(x => x.LivingAreaM2)
            .LessThanOrEqualTo(2000)
            .WithMessage("Stambena površina ne može biti veća od 2000 m².");

        RuleFor(x => x.LandAreaM2)
            .GreaterThan(0)
            .WithMessage("Površina zemljišta (okućnica) mora biti veća od 0 m².");

        RuleFor(x => x.LandAreaM2)
            .GreaterThanOrEqualTo(x => x.LivingAreaM2)
            .WithMessage("Površina zemljišta bi trebala biti veća ili jednaka stambenoj površini.");

        RuleFor(x => x.LandAreaM2)
            .LessThanOrEqualTo(100_000)
            .WithMessage("Površina zemljišta ne može biti veća od 100.000 m².");

        RuleFor(x => x.Floors).IsInEnum();
        RuleFor(x => x.Condition).IsInEnum();
    }
}

// ══════════════════════════════════════════════════════════
//  LAND VALIDATOR
// ══════════════════════════════════════════════════════════

public class LandInputValidator : AbstractValidator<LandInput>
{
    public LandInputValidator()
    {
        RuleFor(x => x.Location).NotNull().SetValidator(new LocationValidator());

        RuleFor(x => x.LandAreaM2)
            .GreaterThan(50)
            .WithMessage("Površina zemljišta mora biti veća od 50 m².");

        RuleFor(x => x.LandAreaM2)
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("Površina zemljišta ne može biti veća od 1.000.000 m².");

        RuleFor(x => x.LandUse).IsInEnum();
    }
}

// ══════════════════════════════════════════════════════════
//  DISPATCHER — validates the correct derived type
// ══════════════════════════════════════════════════════════

/// <summary>
/// Dispatches validation to the correct derived validator based on property type.
/// Register this as IValidator&lt;PropertyInputBase&gt; in DI.
/// </summary>
public class PropertyInputBaseValidator : AbstractValidator<PropertyInputBase>
{
    public PropertyInputBaseValidator()
    {
        RuleFor(x => x.Location).NotNull().WithMessage("Lokacija je obavezna.");
        RuleFor(x => x.PropertyType).IsInEnum();

        // Dispatch to derived validators
        RuleFor(x => x)
            .SetInheritanceValidator(v =>
            {
                v.Add(new ApartmentInputValidator());
                v.Add(new HouseInputValidator());
                v.Add(new LandInputValidator());
            });
    }
}
