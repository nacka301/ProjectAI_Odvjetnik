using System.Text;
using Backend.Models;

namespace Backend.Helpers;

/// <summary>
/// Generates a human-readable Croatian description of a property input.
/// Used as context for AI prompts and email summaries.
/// </summary>
public static class PropertyDescriptionGenerator
{
    public static string Generate(PropertyInputBase input)
    {
        var description = input switch
        {
            ApartmentInput apt => GenerateApartment(apt),
            HouseInput house => GenerateHouse(house),
            LandInput land => GenerateLand(land),
            _ => $"Nekretnina u {FormatLocation(input.Location)}"
        };

        // Append user's free-form notes if provided
        if (!string.IsNullOrWhiteSpace(input.Notes))
            description += $" Dodatne informacije: {input.Notes.Trim()}";

        return description;
    }

    private static string GenerateApartment(ApartmentInput a)
    {
        var sb = new StringBuilder();

        // Room count → Croatian descriptor
        string roomDesc = a.RoomCount switch
        {
            1 => "Jednosoban stan",
            2 => "Dvosoban stan",
            3 => "Trosoban stan",
            4 => "Četverosoban stan",
            _ => $"{a.RoomCount}-soban stan"
        };

        sb.Append($"{roomDesc} u {FormatLocation(a.Location)}");
        sb.Append($", {a.AreaM2:0.#} m²");

        // Floor
        string floorText = a.Floor switch
        {
            FloorType.Subbasement => "podrum",
            FloorType.SemiBasement => "suteren",
            FloorType.Ground => "prizemlje",
            FloorType.Attic => "potkrovlje",
            FloorType.Penthouse => "penthouse",
            _ => $"{(int)a.Floor}. kat"
        };
        sb.Append($", {floorText}");

        if (a.TotalFloors > 0)
            sb.Append($" od {a.TotalFloors}");

        // Year built
        if (a.YearBuilt.HasValue && a.YearBuilt.Value > 0)
            sb.Append($", godina izgradnje {a.YearBuilt.Value}");

        // Features
        var features = new List<string>();
        if (a.HasElevator) features.Add("lift");
        if (a.HasBalcony) features.Add("balkon");
        if (a.HasParking) features.Add("parking");
        if (a.SeaView) features.Add("pogled na more");

        // Orientation
        if (a.Orientation != Orientation.Unknown)
            features.Add($"orijentacija: {FormatOrientation(a.Orientation)}");

        // View type
        if (a.ViewType != ViewType.Unknown && a.ViewType != ViewType.Sea) // SeaView already covered above
            features.Add($"pogled: {FormatViewType(a.ViewType)}");

        if (features.Count > 0)
            sb.Append($", {string.Join(", ", features)}");

        // Condition
        sb.Append($". Stanje: {FormatCondition(a.Condition)}.");

        // Distance to sea
        if (a.DistanceToSeaM.HasValue && a.DistanceToSeaM.Value > 0)
            sb.Append($" Udaljenost od mora: {a.DistanceToSeaM.Value} m.");

        return sb.ToString();
    }

    private static string GenerateHouse(HouseInput h)
    {
        var sb = new StringBuilder();

        string floorDesc = h.Floors switch
        {
            HouseFloors.OneStory => "Prizemna kuća",
            HouseFloors.TwoStory => "Katnica",
            HouseFloors.MultiStory => "Višekatnica",
            _ => "Kuća"
        };

        sb.Append($"{floorDesc} u {FormatLocation(h.Location)}");
        sb.Append($", stambena površina {h.LivingAreaM2:0.#} m²");
        sb.Append($", okućnica {h.LandAreaM2:0.#} m²");

        // Year built
        if (h.YearBuilt.HasValue && h.YearBuilt.Value > 0)
            sb.Append($", godina izgradnje {h.YearBuilt.Value}");

        // Features
        var features = new List<string>();
        if (h.HasGarage) features.Add("garaža");
        if (h.HasPool) features.Add("bazen");
        if (h.HasAuxiliaryObject) features.Add("pomoćni objekt");
        if (h.FirstRowToSea) features.Add("1. red do mora");

        // Orientation
        if (h.Orientation != Orientation.Unknown)
            features.Add($"orijentacija: {FormatOrientation(h.Orientation)}");

        // View type
        if (h.ViewType != ViewType.Unknown)
            features.Add($"pogled: {FormatViewType(h.ViewType)}");

        if (features.Count > 0)
            sb.Append($", {string.Join(", ", features)}");

        sb.Append($". Stanje: {FormatCondition(h.Condition)}.");

        // Distance to sea
        if (h.DistanceToSeaM.HasValue && h.DistanceToSeaM.Value > 0)
            sb.Append($" Udaljenost od mora: {h.DistanceToSeaM.Value} m.");

        return sb.ToString();
    }

    private static string GenerateLand(LandInput l)
    {
        var sb = new StringBuilder();

        string useDesc = l.LandUse switch
        {
            LandUse.Construction => "Građevinsko zemljište",
            LandUse.Agricultural => "Poljoprivredno zemljište",
            LandUse.Mixed => "Mješovito zemljište",
            _ => "Zemljište"
        };

        sb.Append($"{useDesc} u {FormatLocation(l.Location)}");
        sb.Append($", {l.LandAreaM2:0.#} m²");

        // Features
        var features = new List<string>();
        if (l.HasAccessRoad) features.Add("pristupni put");
        if (l.HasInfrastructure) features.Add("komunalna infrastruktura (voda/struja)");
        if (l.FirstRowToSea) features.Add("1. red do mora");

        if (features.Count > 0)
            sb.Append($", {string.Join(", ", features)}");

        sb.Append('.');

        // Legalization
        if (!l.IsLegalized)
            sb.Append(" NAPOMENA: Objekt nije legaliziran — potrebna legalizacija.");

        // Distance to sea
        if (l.DistanceToSeaM.HasValue && l.DistanceToSeaM.Value > 0)
            sb.Append($" Udaljenost od mora: {l.DistanceToSeaM.Value} m.");

        return sb.ToString();
    }

    // ── Helpers ──

    private static string FormatLocation(LocationInfo loc)
    {
        if (!string.IsNullOrWhiteSpace(loc.District) && !string.IsNullOrWhiteSpace(loc.City))
            return $"{loc.City}, {loc.District}";
        if (!string.IsNullOrWhiteSpace(loc.City))
            return loc.City;
        return "nepoznata lokacija";
    }

    private static string FormatCondition(PropertyCondition c) => c switch
    {
        PropertyCondition.New => "novogradnja",
        PropertyCondition.Renovated => "renovirano",
        PropertyCondition.Good => "dobro stanje",
        PropertyCondition.Original => "izvorno stanje",
        PropertyCondition.NeedsWork => "potrebna adaptacija",
        _ => "nepoznato"
    };

    private static string FormatOrientation(Orientation o) => o switch
    {
        Orientation.North => "sjever",
        Orientation.NorthEast => "sjeveroistok",
        Orientation.East => "istok",
        Orientation.SouthEast => "jugoistok",
        Orientation.South => "jug",
        Orientation.SouthWest => "jugozapad",
        Orientation.West => "zapad",
        Orientation.NorthWest => "sjeverozapad",
        _ => "nepoznato"
    };

    private static string FormatViewType(ViewType v) => v switch
    {
        ViewType.Street => "ulica",
        ViewType.Courtyard => "dvorište",
        ViewType.Park => "park",
        ViewType.City => "grad",
        ViewType.Sea => "more",
        ViewType.Panoramic => "panoramski",
        _ => "nepoznato"
    };
}
