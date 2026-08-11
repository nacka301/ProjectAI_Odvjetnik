using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

public class InvoiceRecord : IFirmScoped
{
    public string? UserId { get; set; }
    public string? LawFirmId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string IssuerName { get; set; } = "";
    public string IssuerOib { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientOib { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal PdvRate { get; set; }
    public decimal PdvAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Module { get; set; } = "";
    public string? AnalysisId { get; set; }
    public string? CaseReference { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string UblXml { get; set; } = "";
    public object? RawJsonOutput { get; set; }
}
