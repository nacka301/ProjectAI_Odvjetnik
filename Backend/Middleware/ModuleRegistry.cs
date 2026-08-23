namespace Backend.Middleware;

public enum ModuleStatus
{
    Spreman,
    URazvoju,
}

/// <summary>
/// Which product modules are finished, and which are still in development.
///
/// This is the backend half of a list that also exists in
/// <c>frontend/lib/modules.ts</c>. Two copies, because C# cannot import
/// TypeScript and neither runtime should have to ask the other over the network
/// on every request. <c>Backend.Tests/AdminGuardMiddlewareTests.cs</c> parses the
/// TypeScript file and fails if the two ever disagree — that test is the only
/// thing standing between us and a module that is blurred in the UI while its
/// API is wide open.
///
/// The frontend list is the readable one (it also carries routes and titles);
/// this one carries only what the guard needs.
/// </summary>
public static class ModuleRegistry
{
    public sealed record ModuleDefinition(string Id, string[] ApiPrefixes, ModuleStatus Status);

    public static readonly ModuleDefinition[] All =
    [
        new("troskovnik", ["/api/troskovnik"], ModuleStatus.Spreman),
        new("stranke", ["/api/client", "/api/case", "/api/case-event"], ModuleStatus.Spreman),

        // /wizard calls /api/valuation/draft before it ever reaches
        // /api/evaluate, so both prefixes belong to the same module.
        new("procjena", ["/api/evaluate", "/api/valuation"], ModuleStatus.URazvoju),
        new("ugovor", ["/api/contract"], ModuleStatus.URazvoju),
        new("zk", ["/api/zk"], ModuleStatus.URazvoju),
        new("prijevod", ["/api/translation"], ModuleStatus.URazvoju),
        new("fiskalizacija", ["/api/invoice"], ModuleStatus.URazvoju),
    ];

    /// <summary>
    /// API prefixes belonging to finished modules — the ones a subscription pays
    /// for. Derived from the same list, so a module that later flips to Spreman
    /// lands behind the paywall by that edit alone; nobody has to remember to
    /// add it in a second place.
    /// </summary>
    public static readonly string[] PaidApiPrefixes =
        All.Where(m => m.Status == ModuleStatus.Spreman)
           .SelectMany(m => m.ApiPrefixes)
           .ToArray();

    /// <summary>API prefixes belonging to modules that are not finished yet.</summary>
    public static readonly string[] LockedApiPrefixes =
        All.Where(m => m.Status == ModuleStatus.URazvoju)
           .SelectMany(m => m.ApiPrefixes)
           .ToArray();

    /// <summary>
    /// True when <paramref name="path"/> is <paramref name="prefix"/> itself, or
    /// something nested under it.
    ///
    /// A bare StartsWith would make "/api/zk" swallow a future "/api/zk-nesto",
    /// and "/api/case" swallow "/api/case-event" — which belongs to a module
    /// that is NOT locked. Over-matching here takes working features offline.
    /// </summary>
    public static bool MatchesPrefix(string path, string prefix) =>
        string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this path belongs to a module the subscription pays for.</summary>
    public static bool IsPaidPath(string path) =>
        PaidApiPrefixes.Any(p => MatchesPrefix(path, p));

    /// <summary>The id of the in-development module owning this path, or null.</summary>
    public static string? FindLockedModuleId(string path) =>
        All.FirstOrDefault(m =>
                m.Status == ModuleStatus.URazvoju &&
                m.ApiPrefixes.Any(p => MatchesPrefix(path, p)))
           ?.Id;
}
