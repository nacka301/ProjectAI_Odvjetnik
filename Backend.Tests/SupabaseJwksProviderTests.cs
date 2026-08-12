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

    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ReturnedKey_CanVerify_ASignatureFromTheMatchingPrivateKey()
    {
        // The whole point: what comes back must actually verify Supabase's tokens.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)));

        var parameters = await provider.GetEcKeyAsync(Kid);
        Assert.NotNull(parameters);

        var payload = Encoding.UTF8.GetBytes("header.payload");
        var signature = key.SignData(payload, HashAlgorithmName.SHA256);

        using var publicOnly = ECDsa.Create(parameters!.Value);
        Assert.True(publicOnly.VerifyData(payload, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public async Task UnknownKid_ReturnsNull()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)));

        Assert.Null(await provider.GetEcKeyAsync("a-kid-that-was-never-published"));
    }

    [Fact]
    public async Task RepeatedLookups_HitTheCache_RatherThanSupabase()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler);

        for (var i = 0; i < 5; i++)
            Assert.NotNull(await provider.GetEcKeyAsync(Kid));

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
            Assert.Null(await provider.GetEcKeyAsync($"forged-kid-{i}"));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task WhenSupabaseErrors_ReturnsNull_RatherThanAnUnverifiedKey()
    {
        var provider = Create(Serving("{\"error\":\"boom\"}", HttpStatusCode.InternalServerError));

        Assert.Null(await provider.GetEcKeyAsync(Kid));
    }

    [Fact]
    public async Task WhenTheRequestThrows_ReturnsNull()
    {
        var provider = Create(new StubHandler(_ => throw new HttpRequestException("no route to host")));

        Assert.Null(await provider.GetEcKeyAsync(Kid));
    }

    [Fact]
    public async Task MalformedDocument_ReturnsNull()
    {
        var provider = Create(Serving("not json at all"));

        Assert.Null(await provider.GetEcKeyAsync(Kid));
    }

    [Theory]
    [InlineData("RSA", "P-256")]  // wrong key type
    [InlineData("EC", "P-384")]   // wrong curve for ES256
    public async Task KeysWeCannotVerifyWith_AreSkipped(string kty, string crv)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key, kty: kty, crv: crv)));

        Assert.Null(await provider.GetEcKeyAsync(Kid));
    }

    [Fact]
    public async Task WithoutSupabaseUrl_IsNotConfigured_AndNeverCallsOut()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = Serving(JwksFor(key));
        var provider = Create(handler, supabaseUrl: null);

        Assert.False(provider.IsConfigured);
        Assert.Null(await provider.GetEcKeyAsync(Kid));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TrailingSlashOnSupabaseUrl_DoesNotBreakTheFetch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Create(Serving(JwksFor(key)), supabaseUrl: SupabaseUrl + "/");

        Assert.NotNull(await provider.GetEcKeyAsync(Kid));
    }
}
