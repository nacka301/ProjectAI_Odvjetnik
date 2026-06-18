using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

public class ContractAnalysisRecord
{
    public string? UserId { get; set; }
    public string DocumentType { get; set; } = "";
    public string Summary { get; set; } = "";
    public string OverallRisk { get; set; } = "";
    public int RiskScore { get; set; }
    public int RedFlagsCount { get; set; }
    public object? RawJsonOutput { get; set; }
}
