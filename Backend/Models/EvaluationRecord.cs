using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Record DTOs — match Supabase table columns
// ═══════════════════════════════════════════════════════════

public class EvaluationRecord : IFirmScoped
{
    public string? UserId { get; set; }
    public string? LawFirmId { get; set; }
    public string PropertyType { get; set; } = "";
    public string Location { get; set; } = "";
    public double? SizeM2 { get; set; }
    public int? EstimatedValue { get; set; }
    public object? RawJsonOutput { get; set; }
}
