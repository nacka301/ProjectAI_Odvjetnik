using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

/// <summary>Record written to audit_logs table (EU AI Act Article 13 compliance).</summary>
public class AuditLogRecord : IFirmScoped
{
    public string? UserId { get; set; }
    public string? LawFirmId { get; set; }
    public string Module { get; set; } = "";
    public string? AnalysisId { get; set; }
    public int Step { get; set; } = 1;
    public string Action { get; set; } = "";
    public string LegalBasis { get; set; } = "";
    public string Citation { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string Confidence { get; set; } = "SREDNJE";
    public string ModelUsed { get; set; } = "";
    public double ModelTemperature { get; set; } = 0.10;
}
