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

        public Task<JwksLookup> LookupAsync(string requestedKid, CancellationToken ct = default) =>
            Task.FromResult(
                kid == null ? JwksLookup.Unavailable()
                : requestedKid == kid && key != null ? JwksLookup.Found(key.Value)
                : JwksLookup.UnknownKid());

        public Task<bool> EnsureLoadedAsync(CancellationToken ct = default)
            => Task.FromResult(kid != null);
    }

    /// <summary>
    /// Configured, but the key set could never be fetched — the backend that
    /// cannot reach Supabase. Its answers must not look like the answers given to
    /// a forged token.
    /// </summary>
    private sealed class UnreachableJwks : ISupabaseJwksProvider
    {
        public bool IsConfigured => true;

        public Task<JwksLookup> LookupAsync(string kid, CancellationToken ct = default)
            => Task.FromResult(JwksLookup.Unavailable());

        public Task<bool> EnsureLoadedAsync(CancellationToken ct = default)
            => Task.FromResult(false);
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
        => (await CallAsync(jwks, secret, token)).Accepted;

    /// <summary>
    /// Both halves of the answer. The status matters on its own: refusing is not
    /// enough if "your token is bad" and "we cannot check right now" leave through
    /// the same door.
    /// </summary>
    private static async Task<(bool Accepted, int Status)> CallAsync(
        ISupabaseJwksProvider jwks, string? secret, string token)
    {
        var middleware = Build(jwks, secret, out var reachedNext);

        var context = new DefaultHttpContext();
        context.Request.Path = ProtectedPath;
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return (reachedNext(), context.Response.StatusCode);
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
    //  "Cannot verify" is not "invalid" — 503, never 401
    //
    //  This is the split the outage turned on. A backend that could not fetch
    //  the JWKS answered 401, identically to a backend looking at a forgery, so
    //  the gate told users their sign-in had been refused and offered them a
    //  retry that could not help. From the outside the two were the same event.
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task WhenJwksIsUnreachable_AValidTokenGets503_Not401()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var (accepted, status) = await CallAsync(
            new UnreachableJwks(), TestSecret, Es256Token(key, TestKid));

        Assert.False(accepted);          // still fail-closed: nobody gets in
        Assert.Equal(503, status);       // but this is our outage, not their token
    }

    [Fact]
    public async Task WhenSupabaseUrlIsMissing_Es256Gets503_Not401()
    {
        // The exact production misconfiguration: no SUPABASE_URL, so no key can
        // ever be fetched, so every ES256 token — i.e. every token this project
        // issues — is refused. That has to be legible as a deployment fault.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var (accepted, status) = await CallAsync(NoJwks(), TestSecret, Es256Token(key, TestKid));

        Assert.False(accepted);
        Assert.Equal(503, status);
    }

    [Fact]
    public async Task UnknownKid_Gets401_NotAnOutage()
    {
        // We hold the key set and this kid is not in it. That is a claim about the
        // token, and it must not be reported as the backend being down — otherwise
        // a forged kid would be enough to make the API look broken.
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var (accepted, status) = await CallAsync(jwks, TestSecret, Es256Token(key, "forged-kid"));

        Assert.False(accepted);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task ForgedSignature_Gets401_NotAnOutage()
    {
        var (_, jwks) = EcKeyPair();
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var (accepted, status) = await CallAsync(jwks, TestSecret, Es256Token(attacker, TestKid));

        Assert.False(accepted);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task ExpiredToken_Gets401_NotAnOutage()
    {
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var expired = $"{{\"sub\":\"{Guid.NewGuid()}\",\"email\":\"a@b.hr\"," +
                      $"\"exp\":{DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()}}}";

        var (accepted, status) = await CallAsync(jwks, TestSecret, Es256Token(key, TestKid, expired));

        Assert.False(accepted);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task AnHs256TokenWithNoSecretConfigured_Gets401_NotAnOutage()
    {
        // A deployment with no secret has chosen not to accept HS256. Answering
        // 503 here would let any anonymous caller make the API report itself down.
        var (key, jwks) = EcKeyPair();
        using var _ = key;

        var (accepted, status) = await CallAsync(jwks, null, Hs256Token("whatever-secret"));

        Assert.False(accepted);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task AnOutageNeverGrantsAccess()
    {
        // The whole point of 503 is that it changes the diagnosis, not the answer.
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwks = new UnreachableJwks();

        foreach (var token in new[]
                 {
                     Es256Token(attacker, TestKid),
                     Es256Token(attacker, "any-kid"),
                     "not.a.jwt",
                 })
        {
            Assert.False(await AcceptsAsync(jwks, TestSecret, token));
        }
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
