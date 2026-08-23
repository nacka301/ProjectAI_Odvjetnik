using Backend.Middleware;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// The paywall as the network sees it.
///
/// The notice on /troskovnik runs in the user's browser and can be skipped by
/// typing a URL or calling the API with curl. What actually closes the module is
/// this middleware, so these tests are the ones that matter: they assert the
/// request never reaches a controller.
/// </summary>
[Collection(AdminEmailEnvCollection.Name)]
public class SubscriptionGateMiddlewareTests
{
    private const string AdminEmail = "admin@pravotek.hr";
    private const string UserEmail = "odvjetnik@example.com";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    /// <summary>Paths belonging to finished modules — the ones a subscription pays for.</summary>
    public static TheoryData<string> PaidPaths =>
    [
        "/api/troskovnik",
        "/api/troskovnik/popis",
        "/api/troskovnik/izracunaj",
        "/api/client",
        "/api/client/abc",
        "/api/case",
        "/api/case/abc",
        // Belongs to the same module as /api/case but is NOT nested under it.
        // A bare StartsWith would have matched it by accident rather than on
        // purpose, which is a different bug wearing the same green test.
        "/api/case-event",
        "/api/case-event/abc",
    ];

    /// <summary>
    /// Paths the gate must never touch. /api/settings above all: it is where the
    /// user reads that access is closed and how to pay for it, so closing it
    /// with everything else would leave them at a dead end.
    /// </summary>
    public static TheoryData<string> UngatedPaths =>
    [
        "/api/settings/subscription",
        "/api/settings/profile",
        "/api/health",
        "/api/admin/users",
        // In-development modules. AdminGuardMiddleware already refused these
        // before this middleware runs; if the gate claimed them too, an ordinary
        // user would be told to pay for a feature that does not exist yet.
        "/api/evaluate",
        "/api/contract",
        "/api/invoice",
    ];

    // ── Harness ──

    private sealed record Answer(bool ReachedController, int Status, string Body);

    /// <summary>A gate that answers from memory, so no database is involved.</summary>
    private sealed class StubGate(bool allowed) : ISubscriptionGate
    {
        public int Calls { get; private set; }

        public Task<SubscriptionAccess> EvaluateAsync(string? tenantId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(allowed
                ? new SubscriptionAccess(true, SubscriptionState.Paid, null, null)
                : new SubscriptionAccess(false, SubscriptionState.Expired, 0, null));
        }

        public void InvalidateCache(string tenantId) { }
    }

    private static async Task<Answer> CallAsync(
        string path,
        bool subscriptionAllows,
        string? email = UserEmail,
        StubGate? gate = null)
    {
        // AdminIdentity reads ADMIN_EMAIL in its constructor and the env var
        // wins over configuration, so it is set here rather than left to
        // whatever the shell happens to hold.
        Environment.SetEnvironmentVariable("ADMIN_EMAIL", AdminEmail);

        var reached = false;
        var middleware = new SubscriptionGateMiddleware(
            _ => { reached = true; return Task.CompletedTask; },
            new AdminIdentity(new ConfigurationBuilder().Build()),
            NullLogger<SubscriptionGateMiddleware>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<ISubscriptionGate>(gate ?? new StubGate(subscriptionAllows));

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Items["AuthUserId"] = "22222222-2222-2222-2222-222222222222";
        context.Items["AuthTenantId"] = TenantId;
        if (email is not null)
            context.Items["AuthEmail"] = email;

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new Answer(reached, context.Response.StatusCode, body);
    }

    // ═══════════════════════════════════════════════════════════
    //  Expired → 402, and the controller never runs
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(PaidPaths))]
    public async Task ExpiredSubscription_IsRefusedWith402(string path)
    {
        var answer = await CallAsync(path, subscriptionAllows: false);

        Assert.False(answer.ReachedController);
        Assert.Equal(402, answer.Status);
    }

    [Fact]
    public async Task Refusal_CarriesTheCodeTheFrontendSwitchesOn()
    {
        var answer = await CallAsync("/api/troskovnik/popis", subscriptionAllows: false);

        Assert.Contains("TRIAL_EXPIRED", answer.Body);
        // The message has to say how to get access back, because for most users
        // this response is the whole explanation they will ever see.
        Assert.Contains("vackadavid@gmail.com", answer.Body);
    }

    // ═══════════════════════════════════════════════════════════
    //  Valid subscription → straight through
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(PaidPaths))]
    public async Task ValidSubscription_ReachesTheController(string path)
    {
        var answer = await CallAsync(path, subscriptionAllows: true);

        Assert.True(answer.ReachedController);
        Assert.Equal(200, answer.Status);
    }

    // ═══════════════════════════════════════════════════════════
    //  Paths the gate must not claim
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(UngatedPaths))]
    public async Task UngatedPaths_PassEvenWhenExpired(string path)
    {
        var answer = await CallAsync(path, subscriptionAllows: false);

        Assert.True(answer.ReachedController);
    }

    [Fact]
    public async Task UngatedPath_DoesNotEvenConsultTheGate()
    {
        // Not just "it passes" — the database is not touched at all on the hot
        // path for every request that has nothing to do with a paid module.
        var gate = new StubGate(allowed: false);
        await CallAsync("/api/settings/subscription", subscriptionAllows: false, gate: gate);

        Assert.Equal(0, gate.Calls);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// The one account that can lift a lockout must not be caught by it.
    /// </summary>
    [Theory]
    [MemberData(nameof(PaidPaths))]
    public async Task Admin_WorksWithAnExpiredSubscription(string path)
    {
        var answer = await CallAsync(path, subscriptionAllows: false, email: AdminEmail);

        Assert.True(answer.ReachedController);
    }

    [Fact]
    public async Task Admin_IsRecognisedCaseInsensitively()
    {
        var answer = await CallAsync("/api/troskovnik", subscriptionAllows: false, email: "ADMIN@PRAVOTEK.HR");

        Assert.True(answer.ReachedController);
    }

    [Fact]
    public async Task Admin_ShortCircuitsBeforeTheGateIsAsked()
    {
        var gate = new StubGate(allowed: false);
        await CallAsync("/api/troskovnik", subscriptionAllows: false, email: AdminEmail, gate: gate);

        Assert.Equal(0, gate.Calls);
    }

    // ═══════════════════════════════════════════════════════════
    //  Prefix matching
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// "/api/case" must not swallow a future "/api/casement". Over-matching here
    /// takes a working feature offline and looks like a billing bug.
    /// </summary>
    [Theory]
    [InlineData("/api/clientele")]
    [InlineData("/api/casement")]
    [InlineData("/api/troskovnikx")]
    public async Task NeighbouringPaths_AreNotSwallowed(string path)
    {
        var answer = await CallAsync(path, subscriptionAllows: false);

        Assert.True(answer.ReachedController);
    }

    // ═══════════════════════════════════════════════════════════
    //  The registry the middleware reads from
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void PaidPrefixes_AreTheFinishedModules()
    {
        Assert.Contains("/api/troskovnik", ModuleRegistry.PaidApiPrefixes);
        Assert.Contains("/api/client", ModuleRegistry.PaidApiPrefixes);
        Assert.Contains("/api/case", ModuleRegistry.PaidApiPrefixes);
        Assert.Contains("/api/case-event", ModuleRegistry.PaidApiPrefixes);
    }

    /// <summary>
    /// Paid and locked are disjoint by construction — a module is either
    /// finished or it is not. If they ever overlapped, one prefix would carry
    /// two different refusals and the user would get whichever ran first.
    /// </summary>
    [Fact]
    public void PaidAndLockedPrefixes_DoNotOverlap()
    {
        Assert.Empty(ModuleRegistry.PaidApiPrefixes.Intersect(ModuleRegistry.LockedApiPrefixes));
    }
}
