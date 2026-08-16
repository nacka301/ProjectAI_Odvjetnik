using System.Net;
using System.Security.Cryptography;
using System.Text;
using Backend.Services;
using Backend.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Fetching and caching of Supabase's published signing keys, exercised through
/// the real provider with a stubbed transport — no network.
///
/// The documents here use the exact shape Supabase serves:
/// {"keys":[{"alg":"ES256","crv":"P-256","kid":"...","kty":"EC","use":"sig","x":"...","y":"..."}]}
/// </summary>
public class SupabaseJwksProviderTests
{
    private const string Kid = "dc5b3e82-1e83-499e-9827-90114e6f87ee";
    private const string SupabaseUrl = "https://project.supabase.co";

    // ── Stubs ──

    private sealed class StubHandler(Func<int, (HttpStatusCode Status, string Body)> respond)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = respond(++Calls);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static SupabaseJwksProvider Create(HttpMessageHandler handler, string? supabaseUrl = SupabaseUrl)
    {
        Environment.SetEnvironmentVariable("SUPABASE_URL", supabaseUrl);

        return new SupabaseJwksProvider(
            new StubFactory(handler),
            new ConfigurationBuilder().Build(),
            NullLogger<SupabaseJwksProvider>.Instance);
    }

    private static string JwksFor(ECDsa key, string kid = Kid, string kty = "EC", string crv = "P-256")
    {
        var p = key.ExportParameters(false);
        return $$"""
        {"keys":[{"alg":"ES256","crv":"{{crv}}","ext":true,"key_ops":["verify"],
        "kid":"{{kid}}","kty":"{{kty}}","use":"sig",
        "x":"{{Base64Url.Encode(p.Q.X!)}}","y":"{{Base64Url.Encode(p.Q.Y!)}}"}]}
        """;
    }

    private static StubHandler Serving(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => (status, body));

    // ── Assertions ──

    private static async Task<ECParameters> RequireKeyAsync(SupabaseJwksProvider provider, string kid = Kid)
    {
        var lookup = await provider.LookupAsync(kid);
        Assert.Equal(JwksLookupStatus.Found, lookup.Status);
        Assert.NotNull(lookup.Key);
        return lookup.Key!.Value;
    }

    private static async Task AssertStatusAsync(
        SupabaseJwksProvider provider, string kid, JwksLookupStatus expected)
    {
        var lookup = await provider.LookupAsync(kid);
        Assert.Equal(expected, lookup.Status);
        if (expected != JwksLookupStatus.Found) Assert.Null(lookup.Key);
    }

    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ReturnedKey_CanVerify_ASignatureFromTheMatchingPrivateKey()
    {
        // The whole point: what comes back must actually verify Supabase's tokens.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)));

        var parameters = await RequireKeyAsync(provider);

        var payload = Encoding.UTF8.GetBytes("header.payload");
        var signature = key.SignData(payload, HashAlgorithmName.SHA256);

        using var publicOnly = ECDsa.Create(parameters);
        Assert.True(publicOnly.VerifyData(payload, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public async Task RepeatedLookups_HitTheCache_RatherThanSupabase()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler);

        for (var i = 0; i < 5; i++)
            await RequireKeyAsync(provider);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RepeatedUnknownKids_AreRateLimited()
    {
        // An unknown kid is what a forged token carries. Refetching on every one
        // would turn token spam into traffic aimed at Supabase.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler);

        for (var i = 0; i < 10; i++)
            await AssertStatusAsync(provider, $"forged-kid-{i}", JwksLookupStatus.UnknownKid);

        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("RSA", "P-256")]  // wrong key type
    [InlineData("EC", "P-384")]   // wrong curve for ES256
    public async Task KeysWeCannotVerifyWith_AreSkipped(string kty, string crv)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key, kty: kty, crv: crv)));

        // Nothing usable came back, so we hold no key set at all — blind, not
        // looking at a bad kid.
        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
    }

    [Fact]
    public async Task TrailingSlashOnSupabaseUrl_DoesNotBreakTheFetch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)), supabaseUrl: SupabaseUrl + "/");

        await RequireKeyAsync(provider);
    }

    // ═══════════════════════════════════════════════════════════
    //  "We hold no keys" is not "your kid is wrong"
    //
    //  Both used to come back as a bare null, so the middleware answered 401 to
    //  each. That is what let a failed fetch present itself to a lawyer as
    //  "your sign-in was refused".
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task WhenSupabaseErrors_TheAnswerIsUnavailable_NotUnknownKid()
    {
        var provider = Create(Serving("{\"error\":\"boom\"}", HttpStatusCode.InternalServerError));

        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
    }

    [Fact]
    public async Task WhenTheRequestThrows_TheAnswerIsUnavailable()
    {
        var provider = Create(new StubHandler(_ => throw new HttpRequestException("no route to host")));

        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
    }

    [Fact]
    public async Task MalformedDocument_IsUnavailable_NotUnknownKid()
    {
        var provider = Create(Serving("not json at all"));

        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
    }

    [Fact]
    public async Task WithoutSupabaseUrl_IsNotConfigured_AndNeverCallsOut()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler, supabaseUrl: null);

        Assert.False(provider.IsConfigured);
        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OnceKeysAreHeld_AnUnknownKidIsUnknown_NotAnOutage()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)));

        await RequireKeyAsync(provider);
        await AssertStatusAsync(provider, "never-published", JwksLookupStatus.UnknownKid);
    }

    // ═══════════════════════════════════════════════════════════
    //  The 60-second dead window
    //
    //  On a fresh process the key set is empty and the first ES256 token pays for
    //  the fetch — measured at 3.6 s on a cold Render container against a 5 s
    //  timeout. Losing that race used to be latched in by the same 60 s floor
    //  that exists to stop forged-kid traffic, so one lost race refused every
    //  token for a full minute and no retry could shorten it.
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AFailedFirstFetch_IsRetriedWithinSeconds_NotAfterAMinute()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var good = JwksFor(key);

        // First attempt fails the way a cold container's fetch does; every later
        // attempt would have succeeded.
        var handler = new StubHandler(call => call == 1
            ? (HttpStatusCode.GatewayTimeout, "{}")
            : (HttpStatusCode.OK, good));

        var provider = Create(handler);

        await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);
        Assert.Equal(1, handler.Calls);

        // Past the cold-cache backoff, well short of the 60 s floor that used to
        // apply here.
        await Task.Delay(TimeSpan.FromMilliseconds(1_300));

        await RequireKeyAsync(provider);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task WhileBlind_RetriesAreStillBounded()
    {
        // Recovering fast must not become a fetch per request: a Supabase outage
        // would then be answered by hammering Supabase.
        var handler = new StubHandler(_ => (HttpStatusCode.ServiceUnavailable, "{}"));
        var provider = Create(handler);

        for (var i = 0; i < 25; i++)
            await AssertStatusAsync(provider, Kid, JwksLookupStatus.Unavailable);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EnsureLoaded_ReportsWhetherThisInstanceCanVerifyAnything()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var working = Create(Serving(JwksFor(key)));
        Assert.True(await working.EnsureLoadedAsync());

        var broken = Create(Serving("{}", HttpStatusCode.InternalServerError));
        Assert.False(await broken.EnsureLoadedAsync());

        var unconfigured = Create(Serving(JwksFor(key)), supabaseUrl: null);
        Assert.False(await unconfigured.EnsureLoadedAsync());
    }

    [Fact]
    public async Task EnsureLoaded_WarmsTheCache_SoTheFirstTokenDoesNotPayForIt()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler);

        Assert.True(await provider.EnsureLoadedAsync());
        Assert.Equal(1, handler.Calls);

        await RequireKeyAsync(provider);
        Assert.Equal(1, handler.Calls);   // the token's lookup went nowhere near the network
    }
}
