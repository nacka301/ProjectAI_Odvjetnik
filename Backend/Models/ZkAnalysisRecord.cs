using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

/// <summary>Record written to zk_analyses table.</summary>
public class ZkAnalysisRecord : IFirmScoped
{
    public string? UserId { get; set; }
    public string? LawFirmId { get; set; }
    public string PropertyId { get; set; } = "";
    public string? CadastralMunicipality { get; set; }
    public string? ParcelNumber { get; set; }
    public string? PropertyType { get; set; }
    public string Location { get; set; } = "";
    public double? LandAreaM2 { get; set; }
    public double? BuildingAreaM2 { get; set; }
    public string RiskScore { get; set; } = "";
    public object? Owners { get; set; }
    public object? Mortgages { get; set; }
    public object? Disputes { get; set; }
    public object? Encumbrances { get; set; }
    public string? LawyerComment { get; set; }
    public string? EvaluationId { get; set; }
    public object? RawJsonOutput { get; set; }
}
