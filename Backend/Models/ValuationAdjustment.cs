using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

public class ValuationAdjustment
{
    public string Factor { get; set; } = "";
    public string Description { get; set; } = "";
    public double Percent { get; set; }
}
