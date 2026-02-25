using System.Globalization;

namespace Backend.Utils
{
    public static class FormatHelpers
    {
        public static string FormatEur(decimal value)
        {
            var ci = new CultureInfo("hr-HR");
            return value.ToString("N2", ci) + " €";
        }

        public static string FormatEur(double value)
        {
            var ci = new CultureInfo("hr-HR");
            return value.ToString("N2", ci) + " €";
        }
    }
}
