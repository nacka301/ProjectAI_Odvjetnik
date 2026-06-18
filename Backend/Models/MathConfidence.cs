using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

internal class MathConfidence
{
    public int Score { get; set; }
    public string Level { get; set; } = "";
    public string Explanation { get; set; } = "";
}
