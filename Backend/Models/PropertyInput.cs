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
    public int Floor { get; set; }               // 0 = prizemlje, -1 = suteren
    public int TotalFloors { get; set; }         // Floors in the building
    public PropertyCondition Condition { get; set; } = PropertyCondition.Good;

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

    // ── Feature flags ──
    public bool HasAccessRoad { get; set; }
    public bool HasInfrastructure { get; set; }  // Voda + struja
    public bool FirstRowToSea { get; set; }
}
