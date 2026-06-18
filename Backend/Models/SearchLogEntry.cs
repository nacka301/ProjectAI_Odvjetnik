using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

public class SearchLogEntry
{
    public string Phase { get; set; } = "";
    public string Query { get; set; } = "";
    public int ResultCount { get; set; }
    public int PassedFilter { get; set; }
}
