using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

public class ChronologyRecord
{
    public string? UserId { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalEvents { get; set; }
    public string DateRange { get; set; } = "";
    public object? RawJsonOutput { get; set; }
}
