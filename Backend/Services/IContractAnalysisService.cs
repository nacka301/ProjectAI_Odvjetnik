using System.ClientModel;
using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ContractAnalysisService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IContractAnalysisService
{
/// <summary>
    /// V65: Single-pass contract analysis (replaces old 3-pass system).
    /// One DeepSeek V3.2 call does audit + traps + deviations, then deterministic scoring.
    /// </summary>
    Task<ContractAnalysisResult> AnalyzeContractAsync(string contractText) ;

/// <summary>
    /// V65: Single-pass contract analysis with optional SSE progress callback.
    /// onProgress(stage, status, label, detail) — called at each stage transition.
    /// </summary>
    Task<ContractAnalysisResult> AnalyzeContractAsync(
        string contractText,
        Func<string, string, string, string?, Task>? onProgress) ;

/// <summary>
    /// V58: Generate an improved version of the contract based on the analysis results.
    /// Uses the Heavy model to rewrite the contract with all amendments, fixes, and missing clauses.
    /// </summary>
    Task<GeneratedContractResult> GenerateImprovedContractAsync(
        string originalContractText,
        ContractAnalysisResult analysisResult,
        Func<string, string, string, string?, Task>? onProgress = null) ;
}