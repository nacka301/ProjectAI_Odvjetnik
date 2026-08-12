using System.Security.Cryptography;
using System.Text;
using Backend.Middleware;
using Backend.Services;
using Backend.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Token verification in <see cref="SupabaseJwtMiddleware"/>.
///
/// The ES256 cases exist because Supabase migrated projects from a shared HS256
/// secret to asymmetric signing keys. A HS256-only verifier answers 401 to every
/// token the project now issues, which locked the whole application out.
/// </summary>
public class SupabaseJwtMiddlewareTests
{
    private const string TestSecret = "test-jwt-secret-at-least-32-bytes-long!";
    private const string TestKid = "test-kid-1";
    private const string ProtectedPath = "/api/predmeti";

    // ── Fake key source ──

    private sealed class FakeJwks(string? kid, ECParameters? key) : ISupabaseJwksProvider
    {
        public bool IsConfigured => kid != null;

        public Task<ECParameters?> GetEcKeyAsync(string requestedKid, CancellationToken ct = default)
            => Task.FromResult(requestedKid == kid ? key : null);
    }

    private static FakeJwks NoJwks() => new(null, null);

    // ── Token builders ──

    private static string PayloadJson() =>
        $"{{\"sub\":\"{Guid.NewGuid()}\",\"email\":\"test@odvjetnik.hr\",\"role\":\"authenticated\"," +
        $"\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}";

    private static string Encode(string raw) => Base64Url.Encode(Encoding.UTF8.GetBytes(raw));

    private static string HmacSign(string signingInput, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Base64Url.Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));
    }

    private static string Hs256Token(string secret, string? payload = null)
    {
        var header = Encode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var body = Encode(payload ?? PayloadJson());
        return $"{header}.{body}.{HmacSign($"{header}.{body}", secret)}";
    }

    private static string Es256Token(ECDsa key, string kid, string? payload = null, string alg = "ES256")
    {
        var header = Encode($"{{\"alg\":\"{alg}\",\"typ\":\"JWT\",\"kid\":\"{kid}\"}}");
        var body = Encode(payload ?? PayloadJson());

        // SignData defaults to IEEE P1363 (raw r||s) — exactly what JWS expects.
        var signature = key.SignData(
            Encoding.UTF8.GetBytes($"{header}.{body}"), HashAlgorithmName.SHA256);

        return $"{header}.{body}.{Base64Url.Encode(signature)}";
    }

    // ── Harness ──

    /// <summary>Returns true when the request reached the next middleware, i.e. the token was accepted.</summary>
    private static async Task<bool> AcceptsAsync(ISupabaseJwksProvider jwks, string? secret, string token)
    {
        var middleware = Build(jwks, secret, out var reachedNext);

        var context = new DefaultHttpContext();
        context.Request.Path = ProtectedPath;
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return reachedNext();
    }

    private static SupabaseJwtMiddleware Build(
        ISupabaseJwksProvider jwks, string? secret, out Func<bool> reachedNext)
    {
        // The env var wins over configuration inside the middleware, so it is set
        // explicitly here rather than left to whatever the shell happens to hold.
        Environment.SetEnvironmentVariable("SUPABASE_JWT_SECRET", secret);

        var called = false;
        reachedNext = () => called;

        return new SupabaseJwtMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            new ConfigurationBuilder().Build(),
            jwks,
            NullLogger<SupabaseJwtMiddleware>.Instance);
    }

    private static (ECDsa Key, FakeJwks Jwks) EcKeyPair(string kid = TestKid)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key, new FakeJwks(kid, key.ExportParameters(false)));
    }

    // ═══════════════════════════════════════════════════════════
    //  ES256 — the algorithm Supabase actually issues now
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Es256_WithMatchingJwksKey_IsAccepted()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.True(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid)));
    }

    [Fact]
    public async Task Es256_IsAccepted_EvenWithoutLegacySecret()
    {
        // A project fully migrated off the shared secret must still authenticate.
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.True(await AcceptsAsync(jwks, null, Es256Token(key, TestKid)));
    }

    [Fact]
    public async Task Es256_WithUnknownKid_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, "some-other-kid")));
    }

    [Fact]
    public async Task Es256_SignedByADifferentKey_IsRejected()
    {
        var (_, jwks) = EcKeyPair();
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(attacker, TestKid)));
    }

    [Fact]
    public async Task Es256_WithoutJwksConfigured_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.False(await AcceptsAsync(NoJwks(), TestSecret, Es256Token(key, TestKid)));
    }

    [Fact]
    public async Task Es256_ExpiredToken_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var expired = $"{{\"sub\":\"{Guid.NewGuid()}\",\"email\":\"a@b.hr\"," +
                      $"\"exp\":{DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()}}}";

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid, expired)));
    }

    // ═══════════════════════════════════════════════════════════
    //  HS256 — legacy tokens keep working through a rotation
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Hs256_WithCorrectSecret_IsAccepted()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.True(await AcceptsAsync(jwks, TestSecret, Hs256Token(TestSecret)));
    }

    [Fact]
    public async Task Hs256_WithWrongSecret_IsRejected()
    {
        Assert.False(await AcceptsAsync(NoJwks(), TestSecret, Hs256Token("a-completely-different-secret")));
    }

    [Fact]
    public async Task Hs256_WhenOnlyJwksIsConfigured_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.False(await AcceptsAsync(jwks, null, Hs256Token(TestSecret)));
    }

    // ═══════════════════════════════════════════════════════════
    //  Algorithm confusion — the token does not get to pick its verifier
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AlgNone_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var header = Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var body = Encode(PayloadJson());

        Assert.False(await AcceptsAsync(jwks, TestSecret, $"{header}.{body}."));
    }

    [Fact]
    public async Task EcSignature_LabelledHs256_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid, alg: "HS256")));
    }

    [Fact]
    public async Task HmacSignature_LabelledEs256_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var header = Encode($"{{\"alg\":\"ES256\",\"typ\":\"JWT\",\"kid\":\"{TestKid}\"}}");
        var body = Encode(PayloadJson());

        Assert.False(await AcceptsAsync(
            jwks, TestSecret, $"{header}.{body}.{HmacSign($"{header}.{body}", TestSecret)}"));
    }

    [Fact]
    public async Task UnsupportedAlgorithm_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid, alg: "RS256")));
    }

    // ═══════════════════════════════════════════════════════════
    //  Malformed input and the fail-closed startup guard
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!.???.***")]
    public async Task MalformedToken_IsRejected(string token)
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        Assert.False(await AcceptsAsync(jwks, TestSecret, token));
    }

    [Fact]
    public async Task MissingSubClaim_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var noSub = $"{{\"email\":\"a@b.hr\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}";

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid, noSub)));
    }

    [Fact]
    public async Task NonGuidSubClaim_IsRejected()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var badSub = $"{{\"sub\":\"admin\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}";

        Assert.False(await AcceptsAsync(jwks, TestSecret, Es256Token(key, TestKid, badSub)));
    }

    [Fact]
    public void WithNeitherSecretNorJwks_HostRefusesToStart()
    {
        Environment.SetEnvironmentVariable("SUPABASE_JWT_SECRET", null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new SupabaseJwtMiddleware(
                _ => Task.CompletedTask,
                new ConfigurationBuilder().Build(),
                NoJwks(),
                NullLogger<SupabaseJwtMiddleware>.Instance));

        Assert.Contains("SUPABASE_URL", exception.Message);
    }
}
