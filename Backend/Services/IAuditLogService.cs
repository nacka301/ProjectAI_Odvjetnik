namespace Backend.Services;

/// <summary>
/// Persists HTTP-request-level audit entries to the api_audit_logs table.
/// 
/// GDPR / attorney-client privilege constraints:
///   • NO request body / response body / payload is ever captured.
///   • Only: who, what endpoint, HTTP method, status code, duration.
///   • IP is stored as a SHA-256 hash (not raw).
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Fire-and-forget insert into api_audit_logs.
    /// Must never throw or block the caller.
    /// </summary>
    Task LogAsync(
        string? userId,
        string? tenantId,
        string actionName,
        string endpoint,
        string httpMethod,
        int statusCode,
        int? durationMs = null,
        string? ipHash = null);
}
