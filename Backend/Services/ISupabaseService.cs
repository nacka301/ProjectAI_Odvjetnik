using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za SupabaseService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ISupabaseService
{
    Task<string?> SaveEvaluationAsync(EvaluationRecord record) ;

    Task<string?> SaveContractAnalysisAsync(ContractAnalysisRecord record) ;

    Task<string?> SaveChronologyAsync(ChronologyRecord record) ;

    Task<string?> SaveLegalStrategyAsync(LegalStrategyRecord record) ;

    Task<string?> SaveInvoiceAsync(InvoiceRecord record) ;

    Task<string?> SaveAuditResultAsync(AuditResultRecord record) ;

    Task<string?> SaveZkAnalysisAsync(ZkAnalysisRecord record) ;

    Task<string?> SaveAuditLogAsync(AuditLogRecord record) ;

/// <summary>Save multiple audit log entries in a single batch POST to PostgREST.</summary>
    Task SaveAuditLogBatchAsync(IEnumerable<AuditLogRecord> records) ;

    Task<EvaluationReadRecord?> GetEvaluationByIdAsync(string evaluationId) ;

/// <summary>
    /// Fetch recent evaluations that have NOT been audited yet.
    /// </summary>
    Task<List<EvaluationReadRecord>> GetUnauditedEvaluationsAsync(int limit = 20) ;
}
