using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

public class ConfidenceScore
{
    public string Level { get; set; } = "";
    public int Score { get; set; }
    public string Explanation { get; set; } = "";
    public string Color { get; set; } = "";
}
