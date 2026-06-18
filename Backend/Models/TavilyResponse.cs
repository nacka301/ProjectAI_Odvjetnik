using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

public class TavilyResponse
{
    public List<TavilyResult> Results { get; set; } = [];
}
