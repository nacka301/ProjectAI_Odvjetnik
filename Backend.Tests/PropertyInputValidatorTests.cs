using Backend.Models;
using Backend.Validators;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Backend.Tests;

/// <summary>
/// Comprehensive tests for FluentValidation validators:
/// LocationValidator, ApartmentInputValidator, HouseInputValidator, LandInputValidator, PropertyInputBaseValidator.
/// Tests boundaries, invalid inputs, edge cases, and cross-field rules.
/// </summary>
public class PropertyInputValidatorTests
{
    // ══════════════════════════════════════════════════════════
    //  LocationValidator
    // ══════════════════════════════════════════════════════════

    private readonly LocationValidator _locationValidator = new();

    [Fact]
    public void Location_ValidCity_Passes()
    {
        var location = new LocationInfo { City = "Zagreb", District = "Maksimir", Zip = "10000" };
        var result = _locationValidator.TestValidate(location);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Location_EmptyCity_Fails()
    {
        var location = new LocationInfo { City = "" };
        var result = _locationValidator.TestValidate(location);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void Location_NullCity_Fails()
    {
        var location = new LocationInfo { City = null! };
        var result = _locationValidator.TestValidate(location);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Theory]
    [InlineData("10000", true)]   // Valid 5-digit
    [InlineData("51000", true)]   // Valid 5-digit
    [InlineData("00000", true)]   // Edge: all zeros is technically valid format
    [InlineData("1234", false)]   // Too short
    [InlineData("123456", false)] // Too long
    [InlineData("abcde", false)]  // Not digits
    [InlineData("1234a", false)]  // Mixed
    public void Location_ZipCode_Validation(string zip, bool shouldPass)
    {
        var location = new LocationInfo { City = "Zagreb", Zip = zip };
        var result = _locationValidator.TestValidate(location);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.Zip);
        else
            result.ShouldHaveValidationErrorFor(x => x.Zip);
    }

    [Fact]
    public void Location_NullZip_Passes()
    {
        // Zip is optional — null should pass
        var location = new LocationInfo { City = "Zagreb", Zip = null };
        var result = _locationValidator.TestValidate(location);
        result.ShouldNotHaveValidationErrorFor(x => x.Zip);
    }

    [Fact]
    public void Location_EmptyZip_Passes()
    {
        // Empty zip should be treated as "not provided" and pass
        var location = new LocationInfo { City = "Zagreb", Zip = "" };
        var result = _locationValidator.TestValidate(location);
        result.ShouldNotHaveValidationErrorFor(x => x.Zip);
    }

    [Fact]
    public void Location_CityMaxLength_Passes()
    {
        var location = new LocationInfo { City = new string('a', 100) };
        var result = _locationValidator.TestValidate(location);
        result.ShouldNotHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void Location_CityOverMaxLength_Fails()
    {
        var location = new LocationInfo { City = new string('a', 101) };
        var result = _locationValidator.TestValidate(location);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void Location_DistrictOverMaxLength_Fails()
    {
        var location = new LocationInfo { City = "Zagreb", District = new string('a', 101) };
        var result = _locationValidator.TestValidate(location);
        result.ShouldHaveValidationErrorFor(x => x.District);
    }

    // ══════════════════════════════════════════════════════════
    //  ApartmentInputValidator
    // ══════════════════════════════════════════════════════════

    private readonly ApartmentInputValidator _apartmentValidator = new();

    [Fact]
    public void Apartment_ValidInput_Passes()
    {
        var apt = MakeValidApartment();
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Area boundaries ---

    [Theory]
    [InlineData(10, true)]   // Min boundary
    [InlineData(300, true)]  // Max boundary
    [InlineData(9, false)]   // Below min
    [InlineData(301, false)] // Above max
    [InlineData(0, false)]   // Zero
    [InlineData(-1, false)]  // Negative
    [InlineData(150, true)]  // Middle
    public void Apartment_AreaM2_Boundaries(double area, bool shouldPass)
    {
        var apt = MakeValidApartment();
        apt.AreaM2 = area;
        var result = _apartmentValidator.TestValidate(apt);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.AreaM2);
        else
            result.ShouldHaveValidationErrorFor(x => x.AreaM2);
    }

    // --- Room count boundaries ---

    [Theory]
    [InlineData(1, true)]    // Min
    [InlineData(10, true)]   // Max
    [InlineData(0, false)]   // Below min
    [InlineData(11, false)]  // Above max
    [InlineData(-1, false)]  // Negative
    [InlineData(5, true)]    // Middle
    public void Apartment_RoomCount_Boundaries(int rooms, bool shouldPass)
    {
        var apt = MakeValidApartment();
        apt.RoomCount = rooms;
        var result = _apartmentValidator.TestValidate(apt);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.RoomCount);
        else
            result.ShouldHaveValidationErrorFor(x => x.RoomCount);
    }

    // --- Floor validation ---

    [Fact]
    public void Apartment_ValidFloor_Passes()
    {
        var apt = MakeValidApartment();
        apt.Floor = FloorType.Floor3;
        apt.TotalFloors = 5;
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldNotHaveValidationErrorFor(x => x.Floor);
    }

    [Fact]
    public void Apartment_InvalidFloorEnum_Fails()
    {
        var apt = MakeValidApartment();
        apt.Floor = (FloorType)999; // Invalid enum value
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldHaveValidationErrorFor(x => x.Floor);
    }

    // --- Total floors ---

    [Theory]
    [InlineData(1, true)]    // Min
    [InlineData(50, true)]   // Max
    [InlineData(0, false)]   // Below min
    [InlineData(51, false)]  // Above max
    public void Apartment_TotalFloors_Boundaries(int totalFloors, bool shouldPass)
    {
        var apt = MakeValidApartment();
        apt.TotalFloors = totalFloors;
        apt.Floor = FloorType.Ground; // Keep floor low to avoid cross-validation
        var result = _apartmentValidator.TestValidate(apt);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.TotalFloors);
        else
            result.ShouldHaveValidationErrorFor(x => x.TotalFloors);
    }

    // --- Cross-field: TotalFloors >= Floor ---

    [Fact]
    public void Apartment_FloorHigherThanTotalFloors_Fails()
    {
        var apt = MakeValidApartment();
        apt.Floor = FloorType.Floor5;   // 5th floor
        apt.TotalFloors = 3;            // But building only has 3 floors
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldHaveValidationErrorFor(x => x.TotalFloors);
    }

    [Fact]
    public void Apartment_FloorEqualToTotalFloors_Passes()
    {
        var apt = MakeValidApartment();
        apt.Floor = FloorType.Floor3;
        apt.TotalFloors = 3; // Penthouse
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldNotHaveValidationErrorFor(x => x.TotalFloors);
    }

    // --- Elevator warning (not error) for high floors ---

    [Fact]
    public void Apartment_HighFloorNoElevator_WarningNotError()
    {
        var apt = MakeValidApartment();
        apt.Floor = FloorType.Floor5;
        apt.TotalFloors = 8;
        apt.HasElevator = false;

        var result = _apartmentValidator.TestValidate(apt);

        // Should have warning but NOT error-level failure
        var elevatorFailures = result.Errors
            .Where(e => e.PropertyName == nameof(ApartmentInput.HasElevator))
            .ToList();

        // FluentValidation: Severity.Warning means it passes validation but has warnings
        Assert.All(elevatorFailures, e =>
            Assert.Equal(Severity.Warning, e.Severity));
    }

    // --- NullLocation ---

    [Fact]
    public void Apartment_NullLocation_Fails()
    {
        var apt = MakeValidApartment();
        apt.Location = null!;
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    // --- Condition enum ---

    [Fact]
    public void Apartment_InvalidCondition_Fails()
    {
        var apt = MakeValidApartment();
        apt.Condition = (PropertyCondition)99;
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldHaveValidationErrorFor(x => x.Condition);
    }

    [Theory]
    [InlineData(PropertyCondition.New)]
    [InlineData(PropertyCondition.Renovated)]
    [InlineData(PropertyCondition.Good)]
    [InlineData(PropertyCondition.Original)]
    [InlineData(PropertyCondition.NeedsWork)]
    [InlineData(PropertyCondition.Unknown)]
    public void Apartment_AllValidConditions_Pass(PropertyCondition condition)
    {
        var apt = MakeValidApartment();
        apt.Condition = condition;
        var result = _apartmentValidator.TestValidate(apt);
        result.ShouldNotHaveValidationErrorFor(x => x.Condition);
    }

    // ══════════════════════════════════════════════════════════
    //  HouseInputValidator
    // ══════════════════════════════════════════════════════════

    private readonly HouseInputValidator _houseValidator = new();

    [Fact]
    public void House_ValidInput_Passes()
    {
        var house = MakeValidHouse();
        var result = _houseValidator.TestValidate(house);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Living area ---

    [Theory]
    [InlineData(21, true)]      // Just above min
    [InlineData(20, false)]     // Boundary (must be > 20, not >=)
    [InlineData(20.01, true)]   // Just above
    [InlineData(2000, true)]    // Max boundary
    [InlineData(2001, false)]   // Above max
    [InlineData(0, false)]      // Zero
    [InlineData(-10, false)]    // Negative
    [InlineData(150, true)]     // Normal
    public void House_LivingAreaM2_Boundaries(double area, bool shouldPass)
    {
        var house = MakeValidHouse();
        house.LivingAreaM2 = area;
        var result = _houseValidator.TestValidate(house);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.LivingAreaM2);
        else
            result.ShouldHaveValidationErrorFor(x => x.LivingAreaM2);
    }

    // --- Land area ---

    [Theory]
    [InlineData(1, true)]           // Minimum positive
    [InlineData(0, false)]          // Zero (must be > 0)
    [InlineData(-1, false)]         // Negative
    [InlineData(100000, true)]      // Max boundary
    [InlineData(100001, false)]     // Above max
    [InlineData(500, true)]         // Normal
    public void House_LandAreaM2_Boundaries(double area, bool shouldPass)
    {
        var house = MakeValidHouse();
        house.LandAreaM2 = area;
        var result = _houseValidator.TestValidate(house);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.LandAreaM2);
        else
            result.ShouldHaveValidationErrorFor(x => x.LandAreaM2);
    }

    // --- Multi-story house: living > land is VALID (V20 fix) ---

    [Fact]
    public void House_LivingAreaExceedsLandArea_IsValid()
    {
        // Multi-story houses can have living > plot:
        // e.g., 3-story house on 180m² plot = 300m² living
        var house = MakeValidHouse();
        house.LivingAreaM2 = 300;
        house.LandAreaM2 = 180;

        var result = _houseValidator.TestValidate(house);

        // Should NOT fail — V20 removed this constraint
        result.ShouldNotHaveValidationErrorFor(x => x.LandAreaM2);
        result.ShouldNotHaveValidationErrorFor(x => x.LivingAreaM2);
    }

    // --- Floors enum ---

    [Fact]
    public void House_InvalidFloorsEnum_Fails()
    {
        var house = MakeValidHouse();
        house.Floors = (HouseFloors)99;
        var result = _houseValidator.TestValidate(house);
        result.ShouldHaveValidationErrorFor(x => x.Floors);
    }

    [Theory]
    [InlineData(HouseFloors.OneStory)]
    [InlineData(HouseFloors.TwoStory)]
    [InlineData(HouseFloors.MultiStory)]
    public void House_AllValidFloors_Pass(HouseFloors floors)
    {
        var house = MakeValidHouse();
        house.Floors = floors;
        var result = _houseValidator.TestValidate(house);
        result.ShouldNotHaveValidationErrorFor(x => x.Floors);
    }

    [Fact]
    public void House_NullLocation_Fails()
    {
        var house = MakeValidHouse();
        house.Location = null!;
        var result = _houseValidator.TestValidate(house);
        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    // ══════════════════════════════════════════════════════════
    //  LandInputValidator
    // ══════════════════════════════════════════════════════════

    private readonly LandInputValidator _landValidator = new();

    [Fact]
    public void Land_ValidInput_Passes()
    {
        var land = MakeValidLand();
        var result = _landValidator.TestValidate(land);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Land area ---

    [Theory]
    [InlineData(51, true)]          // Just above min
    [InlineData(50, false)]         // Boundary (must be > 50)
    [InlineData(1000000, true)]     // Max boundary
    [InlineData(1000001, false)]    // Above max
    [InlineData(0, false)]          // Zero
    [InlineData(-100, false)]       // Negative
    [InlineData(5000, true)]        // Normal
    public void Land_LandAreaM2_Boundaries(double area, bool shouldPass)
    {
        var land = MakeValidLand();
        land.LandAreaM2 = area;
        var result = _landValidator.TestValidate(land);

        if (shouldPass)
            result.ShouldNotHaveValidationErrorFor(x => x.LandAreaM2);
        else
            result.ShouldHaveValidationErrorFor(x => x.LandAreaM2);
    }

    // --- Land use enum ---

    [Fact]
    public void Land_InvalidLandUseEnum_Fails()
    {
        var land = MakeValidLand();
        land.LandUse = (LandUse)99;
        var result = _landValidator.TestValidate(land);
        result.ShouldHaveValidationErrorFor(x => x.LandUse);
    }

    [Theory]
    [InlineData(LandUse.Construction)]
    [InlineData(LandUse.Agricultural)]
    [InlineData(LandUse.Mixed)]
    public void Land_AllValidLandUses_Pass(LandUse landUse)
    {
        var land = MakeValidLand();
        land.LandUse = landUse;
        var result = _landValidator.TestValidate(land);
        result.ShouldNotHaveValidationErrorFor(x => x.LandUse);
    }

    [Fact]
    public void Land_NullLocation_Fails()
    {
        var land = MakeValidLand();
        land.Location = null!;
        var result = _landValidator.TestValidate(land);
        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    // ══════════════════════════════════════════════════════════
    //  PropertyInputBaseValidator (dispatch validator)
    // ══════════════════════════════════════════════════════════

    private readonly PropertyInputBaseValidator _baseValidator = new();

    [Fact]
    public void BaseValidator_ApartmentInput_DispatchesToApartmentValidator()
    {
        var apt = MakeValidApartment();
        apt.AreaM2 = 5; // Below apartment min (10)

        var result = _baseValidator.TestValidate(apt);
        // Should have the apartment-specific error
        result.ShouldHaveValidationErrorFor(x => ((ApartmentInput)x).AreaM2);
    }

    [Fact]
    public void BaseValidator_HouseInput_DispatchesToHouseValidator()
    {
        var house = MakeValidHouse();
        house.LivingAreaM2 = 10; // Below house min (> 20)

        var result = _baseValidator.TestValidate(house);
        result.ShouldHaveValidationErrorFor(x => ((HouseInput)x).LivingAreaM2);
    }

    [Fact]
    public void BaseValidator_LandInput_DispatchesToLandValidator()
    {
        var land = MakeValidLand();
        land.LandAreaM2 = 10; // Below land min (> 50)

        var result = _baseValidator.TestValidate(land);
        result.ShouldHaveValidationErrorFor(x => ((LandInput)x).LandAreaM2);
    }

    // ══════════════════════════════════════════════════════════
    //  Stress: Multiple simultaneous validation errors
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Apartment_MultipleErrors_AllReported()
    {
        var apt = new ApartmentInput
        {
            Location = null!,  // Error: location required
            AreaM2 = 0,        // Error: below 10
            RoomCount = 0,     // Error: below 1
            TotalFloors = 0,   // Error: below 1
            Floor = (FloorType)999, // Error: invalid enum
        };

        var result = _apartmentValidator.TestValidate(apt);

        // Should report multiple errors, not stop at first
        Assert.True(result.Errors.Count >= 3,
            $"Expected multiple errors, got {result.Errors.Count}");
    }

    // ══════════════════════════════════════════════════════════
    //  Croatian-specific: Error messages in Croatian
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Apartment_ErrorMessages_AreInCroatian()
    {
        var apt = MakeValidApartment();
        apt.AreaM2 = 5; // Below min
        var result = _apartmentValidator.TestValidate(apt);

        var areaError = result.Errors.FirstOrDefault(e => e.PropertyName == nameof(ApartmentInput.AreaM2));
        Assert.NotNull(areaError);
        Assert.Contains("Površina", areaError!.ErrorMessage);
    }

    [Fact]
    public void House_ErrorMessages_AreInCroatian()
    {
        var house = MakeValidHouse();
        house.LivingAreaM2 = 5; // Below min
        var result = _houseValidator.TestValidate(house);

        var areaError = result.Errors.FirstOrDefault(e => e.PropertyName == nameof(HouseInput.LivingAreaM2));
        Assert.NotNull(areaError);
        Assert.Contains("površina", areaError!.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER FACTORIES
    // ══════════════════════════════════════════════════════════

    private static ApartmentInput MakeValidApartment() => new()
    {
        Location = new LocationInfo { City = "Zagreb", District = "Maksimir", Zip = "10000" },
        PropertyType = PropertyType.Apartment,
        AreaM2 = 65,
        RoomCount = 3,
        Floor = FloorType.Floor2,
        TotalFloors = 5,
        Condition = PropertyCondition.Good,
        HasElevator = true,
    };

    private static HouseInput MakeValidHouse() => new()
    {
        Location = new LocationInfo { City = "Zagreb", District = "Maksimir", Zip = "10000" },
        PropertyType = PropertyType.House,
        LivingAreaM2 = 150,
        LandAreaM2 = 500,
        Floors = HouseFloors.TwoStory,
        Condition = PropertyCondition.Good,
    };

    private static LandInput MakeValidLand() => new()
    {
        Location = new LocationInfo { City = "Zadar", District = "Okolica", Zip = "23000" },
        PropertyType = PropertyType.Land,
        LandAreaM2 = 1000,
        LandUse = LandUse.Construction,
    };
}
