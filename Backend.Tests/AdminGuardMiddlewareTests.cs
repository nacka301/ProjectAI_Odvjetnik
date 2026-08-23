using System.Text.RegularExpressions;
using Backend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// <see cref="AdminGuardMiddleware"/> is the only thing that actually closes a
/// module. The blurred cards on the dashboard and the redirect in
/// frontend/middleware.ts both run in the user's own browser; this runs on our
/// side of the wire, off a verified token.
///
/// So the cases below are less about the happy path than about the two ways this
/// fails silently: locking something that should stay open (Troškovnik and
/// Stranke go dark and nobody can work), or leaving something open that should
/// be locked (the whole feature is theatre).
/// </summary>
[Collection(AdminEmailEnvCollection.Name)]
public class AdminGuardMiddlewareTests
{
    private const string AdminEmail = "admin@odvjetnik.hr";
    private const string UserEmail = "korisnik@odvjetnik.hr";
    private const string UserId = "11111111-1111-1111-1111-111111111111";

    /// <summary>One endpoint from each module that is still in development.</summary>
    public static TheoryData<string> LockedPaths =>
    [
        "/api/evaluate/stream",
        "/api/evaluate/text/stream",
        "/api/valuation/draft",
        "/api/contract/analyze-text-stream",
        "/api/contract/analyze-pdf",
        "/api/zk/stream",
        "/api/zk/stream-text",
        "/api/translation/stream",
    ];

    /// <summary>
    /// Endpoints Troškovnik and Stranke depend on. If any of these ever answers
    /// 403 to an ordinary user, the two finished modules are broken.
    /// </summary>
    public static TheoryData<string> OpenPaths =>
    [
        "/api/troskovnik/katalog",
        "/api/troskovnik/izracunaj",
        "/api/client",
        "/api/client/abc/profile",
        "/api/case",
        // Shares a prefix with /api/case but is a different route, and belongs to
        // a module that is open. A sloppy StartsWith would take it down too.
        "/api/case-event",
        "/api/health",
        "/api/settings/profile",
    ];

    // ── Harness ──

    private sealed record Answer(bool ReachedController, int Status, string Body);

    private static async Task<Answer> CallAsync(
        string path,
        string? email,
        string? adminEmail = AdminEmail)
    {
        // The middleware reads ADMIN_EMAIL once, in its constructor, and the env
        // var wins over configuration — so it is set here rather than left to
        // whatever the shell happens to hold.
        Environment.SetEnvironmentVariable("ADMIN_EMAIL", adminEmail);

        var reached = false;
        var middleware = new AdminGuardMiddleware(
            _ => { reached = true; return Task.CompletedTask; },
            new AdminIdentity(new ConfigurationBuilder().Build()),
            NullLogger<AdminGuardMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (email is not null)
        {
            context.Items["AuthUserId"] = UserId;
            context.Items["AuthEmail"] = email;
        }

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new Answer(reached, context.Response.StatusCode, body);
    }

    // ═══════════════════════════════════════════════════════════
    //  Modules still in development
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(LockedPaths))]
    public async Task InDevelopmentModule_RefusesOrdinaryUser(string path)
    {
        var answer = await CallAsync(path, UserEmail);

        Assert.False(answer.ReachedController);
        Assert.Equal(403, answer.Status);
    }

    [Theory]
    [MemberData(nameof(LockedPaths))]
    public async Task InDevelopmentModule_LetsAdminThrough(string path)
    {
        var answer = await CallAsync(path, AdminEmail);

        Assert.True(answer.ReachedController);
    }

    [Fact]
    public async Task InDevelopmentModule_SaysWhyItRefused()
    {
        var answer = await CallAsync("/api/evaluate/stream", UserEmail);

        // The frontend needs to tell "not finished" apart from "not for you";
        // they call for different words on screen.
        Assert.Contains("MODULE_IN_DEVELOPMENT", answer.Body);
    }

    [Fact]
    public async Task InDevelopmentModule_WithoutToken_Is401NotFound()
    {
        var answer = await CallAsync("/api/evaluate/stream", email: null);

        Assert.False(answer.ReachedController);
        Assert.Equal(401, answer.Status);
    }

    // ═══════════════════════════════════════════════════════════
    //  Finished modules must keep working
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(OpenPaths))]
    public async Task FinishedModule_IsUntouchedForOrdinaryUsers(string path)
    {
        var answer = await CallAsync(path, UserEmail);

        Assert.True(answer.ReachedController);
    }

    [Theory]
    // Prefix matching is exact-or-nested. Without that, each of these would be
    // swallowed by the locked prefix it happens to start with.
    [InlineData("/api/evaluateXyz")]
    [InlineData("/api/zk-nesto/stream")]
    [InlineData("/api/contracts")]
    [InlineData("/api/valuations")]
    public async Task NeighbouringPath_IsNotSwallowedByALockedPrefix(string path)
    {
        var answer = await CallAsync(path, UserEmail);

        Assert.True(answer.ReachedController);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin-only tooling — behaviour that existed before
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/rag/seed")]
    public async Task AdminOnlyTooling_StillRefusesWithItsOwnMessage(string path)
    {
        var answer = await CallAsync(path, UserEmail);

        Assert.Equal(403, answer.Status);
        // Not an unfinished module — a place users were never meant to go.
        Assert.DoesNotContain("MODULE_IN_DEVELOPMENT", answer.Body);
    }

    [Fact]
    public async Task NoAdminEmailConfigured_ClosesEverythingGuarded()
    {
        var locked = await CallAsync("/api/evaluate/stream", AdminEmail, adminEmail: null);
        var tooling = await CallAsync("/api/admin/users", AdminEmail, adminEmail: null);
        var open = await CallAsync("/api/troskovnik/katalog", AdminEmail, adminEmail: null);

        // Fail closed: with no admin to recognise, nobody is one.
        Assert.Equal(403, locked.Status);
        Assert.Equal(403, tooling.Status);
        // But only for guarded paths — the finished modules stay up.
        Assert.True(open.ReachedController);
    }

    // ═══════════════════════════════════════════════════════════
    //  The two registries must agree
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// ModuleRegistry is a hand-copy of frontend/lib/modules.ts, because C#
    /// cannot import TypeScript. This is the only thing that notices when
    /// somebody unlocks a module on one side and forgets the other — which would
    /// leave a module either blurred with a live API, or open in the UI and 403
    /// on every click.
    ///
    /// Skips rather than fails when the frontend is not on disk, so a
    /// backend-only checkout still builds.
    /// </summary>
    [Fact]
    public void RegistryMatchesTheFrontendList()
    {
        var source = FindFrontendRegistry();
        if (source is null) return;

        var text = File.ReadAllText(source);

        // Each entry is `id: "...",` … `status: "...",` in that order. The
        // interface declaration above them writes `id: string;` without quotes,
        // so it does not match.
        const string entryPattern =
            "id:\\s*\"(?<id>[^\"]+)\"[\\s\\S]*?status:\\s*\"(?<status>[^\"]+)\"";

        var entries = Regex.Matches(text, entryPattern)
            .ToDictionary(m => m.Groups["id"].Value, m => m.Groups["status"].Value);

        Assert.NotEmpty(entries);

        var mine = ModuleRegistry.All.ToDictionary(
            m => m.Id,
            m => m.Status == ModuleStatus.URazvoju ? "u-razvoju" : "spreman");

        Assert.Equal(mine.Keys.OrderBy(k => k), entries.Keys.OrderBy(k => k));

        foreach (var (id, status) in mine)
            Assert.Equal(status, entries[id]);
    }

    /// <summary>Walks up from the test binary until a repository root turns up.</summary>
    private static string? FindFrontendRegistry()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "lib", "modules.ts");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
