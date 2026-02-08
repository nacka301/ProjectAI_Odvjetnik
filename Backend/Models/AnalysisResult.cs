namespace Backend.Models;

public class AnalysisResult
{
    public string PropertyId { get; set; } = string.Empty;
    public List<string> Owners { get; set; } = [];
    public List<MortgageInfo> ActiveMortgages { get; set; } = [];
    public List<string> ActiveDisputes { get; set; } = [];
    public string RiskScore { get; set; } = string.Empty;
    public string LawyerComment { get; set; } = string.Empty;
}

public class MortgageInfo
{
    public string Amount { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
