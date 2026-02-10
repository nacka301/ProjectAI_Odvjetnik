using System.Text.Json.Serialization;

namespace Backend.Models;

// ══════════════════════════════════════════════════════════
//  ENUMS — Property type, condition, land use, house floors
// ══════════════════════════════════════════════════════════

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    Apartment,
    House,
    Land
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyCondition
{
    New,           // Novogradnja
    Renovated,     // Renovirano / adaptirano
    Good,          // Dobro stanje / održavano
    Original,      // Izvorno stanje
    NeedsWork      // Potrebna adaptacija / ruševina
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HouseFloors
{
    OneStory,
    TwoStory,
    MultiStory
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LandUse
{
    Construction,   // Građevinsko
    Agricultural,   // Poljoprivredno
    Mixed           // Mješovito
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Orientation
{
    Unknown,
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ViewType
{
    Unknown,
    Street,
    Courtyard,
    Park,
    City,
    Sea,
    Panoramic
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FloorType
{
    Subbasement = -2,   // Podrum
    SemiBasement = -1,  // Suteren
    Ground = 0,         // Prizemlje
    Floor1 = 1,
    Floor2 = 2,
    Floor3 = 3,
    Floor4 = 4,
    Floor5 = 5,
    Floor6 = 6,
    Floor7 = 7,
    Floor8 = 8,
    Floor9 = 9,
    Floor10 = 10,
    Floor11 = 11,
    Floor12 = 12,
    Floor13 = 13,
    Floor14 = 14,
    Floor15 = 15,
    Floor16 = 16,
    Floor17 = 17,
    Floor18 = 18,
    Floor19 = 19,
    Floor20 = 20,
    Floor21 = 21,
    Floor22 = 22,
    Floor23 = 23,
    Floor24 = 24,
    Floor25 = 25,
    Floor26 = 26,
    Floor27 = 27,
    Floor28 = 28,
    Floor29 = 29,
    Floor30Plus = 30,   // 30. kat i više
    Attic = 100,        // Potkrovlje
    Penthouse = 101     // Penthouse
}

// ══════════════════════════════════════════════════════════
//  LOCATION — shared across all property types
// ══════════════════════════════════════════════════════════

public class LocationInfo
{
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;  // Kvart / naselje
    public string? Zip { get; set; }
}

// ══════════════════════════════════════════════════════════
//  BASE CLASS — polymorphic discriminator
// ══════════════════════════════════════════════════════════

/// <summary>
/// Polymorphic base for all property input types.
/// JSON discriminator field: "type" → "Apartment" | "House" | "Land".
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ApartmentInput), "Apartment")]
[JsonDerivedType(typeof(HouseInput), "House")]
[JsonDerivedType(typeof(LandInput), "Land")]
public abstract class PropertyInputBase
{
    public LocationInfo Location { get; set; } = new();
    public PropertyType PropertyType { get; set; }

    /// <summary>Optional free-form notes from the user.</summary>
    public string? Notes { get; set; }
}

// ══════════════════════════════════════════════════════════
//  APARTMENT
// ══════════════════════════════════════════════════════════

public class ApartmentInput : PropertyInputBase
{
    // ── Mandatory ──
    public double AreaM2 { get; set; }
    public int RoomCount { get; set; }           // 1, 2, 3, 4, 5+
    public FloorType Floor { get; set; } = FloorType.Ground;
    public int TotalFloors { get; set; }         // Floors in the building
    public PropertyCondition Condition { get; set; } = PropertyCondition.Good;

    // ── Optional detail ──
    public int? YearBuilt { get; set; }          // Godina izgradnje
    public int? DistanceToSeaM { get; set; }     // Udaljenost od mora u metrima

    // ── Orientation & view ──
    public Orientation Orientation { get; set; } = Orientation.Unknown;
    public ViewType ViewType { get; set; } = ViewType.Unknown;

    // ── Feature flags ──
    public bool HasElevator { get; set; }
    public bool HasParking { get; set; }
    public bool HasBalcony { get; set; }
    public bool SeaView { get; set; }
}

// ══════════════════════════════════════════════════════════
//  HOUSE
// ══════════════════════════════════════════════════════════

public class HouseInput : PropertyInputBase
{
    // ── Mandatory ──
    public double LivingAreaM2 { get; set; }
    public double LandAreaM2 { get; set; }       // Okućnica / yard
    public HouseFloors Floors { get; set; } = HouseFloors.OneStory;
    public PropertyCondition Condition { get; set; } = PropertyCondition.Good;

    // ── Optional detail ──
    public int? YearBuilt { get; set; }          // Godina izgradnje
    public int? DistanceToSeaM { get; set; }     // Udaljenost od mora u metrima

    // ── Orientation & view ──
    public Orientation Orientation { get; set; } = Orientation.Unknown;
    public ViewType ViewType { get; set; } = ViewType.Unknown;

    // ── Feature flags ──
    public bool HasGarage { get; set; }
    public bool HasPool { get; set; }
    public bool FirstRowToSea { get; set; }
    public bool HasAuxiliaryObject { get; set; } // Pomoćni objekt
}

// ══════════════════════════════════════════════════════════
//  LAND
// ══════════════════════════════════════════════════════════

public class LandInput : PropertyInputBase
{
    // ── Mandatory ──
    public double LandAreaM2 { get; set; }
    public LandUse LandUse { get; set; } = LandUse.Construction;

    // ── Optional detail ──
    public int? DistanceToSeaM { get; set; }     // Udaljenost od mora u metrima

    // ── Feature flags ──
    public bool HasAccessRoad { get; set; }
    public bool HasInfrastructure { get; set; }  // Voda + struja
    public bool FirstRowToSea { get; set; }
    public bool IsLegalized { get; set; } = true; // Je li objekt legaliziran
}
