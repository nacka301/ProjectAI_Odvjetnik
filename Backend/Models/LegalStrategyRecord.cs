using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

public class LegalStrategyRecord
{
    public string? UserId { get; set; }
    public string KeyLegalIssue { get; set; } = "";
    public string ClientPosition { get; set; } = "kupac";
    public int WinProbability { get; set; }
    public int WinProbabilityMin { get; set; }
    public int WinProbabilityMax { get; set; }
    public int PrecedentsCount { get; set; }
    public object? RawJsonOutput { get; set; }
}
