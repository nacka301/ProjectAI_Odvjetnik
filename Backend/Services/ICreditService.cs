using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za CreditService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ICreditService
{
/// <summary>
    /// Check if the user's firm has remaining credits for the given module.
    /// If yes, increment used_credits by 1 and return Allowed=true.
    /// If no, return Allowed=false with a professional Croatian error message.
    /// If the firm_usage row doesn't exist yet, auto-create it with default limits.
    /// </summary>
    Task<CreditCheckResult> ConsumeCreditAsync(string userId, string moduleName) ;

/// <summary>Get all credit usage rows for a user's firm.</summary>
    Task<List<FirmUsageResponse>> GetFirmUsageAsync(string userId) ;
}