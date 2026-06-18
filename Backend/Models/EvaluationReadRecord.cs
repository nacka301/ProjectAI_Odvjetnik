using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

/// <summary>Record read back from evaluations table (includes id + created_at).</summary>
public class EvaluationReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string? UserId { get; set; }
    public string PropertyType { get; set; } = "";
    public string Location { get; set; } = "";
    public double? SizeM2 { get; set; }
    public double? EstimatedValue { get; set; }
    public object? RawJsonOutput { get; set; }
}
