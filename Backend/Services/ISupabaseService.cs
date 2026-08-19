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

    Task<string?> SaveZkAnalysisAsync(ZkAnalysisRecord record) ;

    Task<string?> SaveAuditLogAsync(AuditLogRecord record) ;

/// <summary>Save multiple audit log entries in a single batch POST to PostgREST.</summary>
    Task SaveAuditLogBatchAsync(IEnumerable<AuditLogRecord> records) ;
}
