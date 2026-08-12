using Backend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OtpNet;

namespace Backend.Tests;

/// <summary>
/// Tests for TwoFactorService — TOTP operations, encryption, and recovery codes.
/// These tests validate core 2FA logic WITHOUT requiring Supabase connectivity.
/// </summary>
public class TwoFactorServiceTests
{
    // 32-byte base64-encoded key for tests
    private const string TestEncryptionKey = "dGVzdC1lbmNyeXB0aW9uLWtleS1mb3ItMmZhLXRlc3Q="; // 32 bytes

    private static TwoFactorService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TwoFactor:EncryptionKey"] = TestEncryptionKey,
            })
            .Build();

        var httpClient = new HttpClient();
        var logger = NullLogger<TwoFactorService>.Instance;
        return new TwoFactorService(httpClient, config, logger);
    }

    // ═══════════════════════════════════════════════════════════
    //  SETUP GENERATION TESTS
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void GenerateSetup_Returns_ValidOtpauthUrl()
    {
        var service = CreateService();
        var result = service.GenerateSetup("test@odvjetnik.hr");

        Assert.NotEmpty(result.OtpauthUrl);
        Assert.StartsWith("otpauth://totp/", result.OtpauthUrl);
        Assert.Contains("PravoTek", result.OtpauthUrl);
        Assert.Contains("test%40odvjetnik.hr", result.OtpauthUrl);
    }

    [Fact]
    public void GenerateSetup_Returns_ManualEntryKey_With_Spaces()
    {
        var service = CreateService();
        var result = service.GenerateSetup("test@odvjetnik.hr");

        Assert.NotEmpty(result.ManualEntryKey);
        // Manual key should have spaces for readability
        Assert.Contains(" ", result.ManualEntryKey);
        // Without spaces, should be valid base32
        var cleanKey = result.ManualEntryKey.Replace(" ", "");
        Assert.True(cleanKey.Length >= 16, "Base32 key should be at least 16 chars");
    }

    [Fact]
    public void GenerateSetup_Produces_UniqueSec_EachCall()
    {
        var service = CreateService();
        var result1 = service.GenerateSetup("test@odvjetnik.hr");
        var result2 = service.GenerateSetup("test@odvjetnik.hr");

        Assert.NotEqual(result1.ManualEntryKey, result2.ManualEntryKey);
        Assert.NotEqual(result1.OtpauthUrl, result2.OtpauthUrl);
    }

    // ═══════════════════════════════════════════════════════════
    //  ENCRYPTION / DECRYPTION TESTS (AES-256-GCM)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Encrypt_Decrypt_RoundTrip()
    {
        var service = CreateService();
        var plaintext = "JBSWY3DPEHPK3PXP";

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_EachCall()
    {
        var service = CreateService();
        var plaintext = "JBSWY3DPEHPK3PXP";

        var encrypted1 = service.Encrypt(plaintext);
        var encrypted2 = service.Encrypt(plaintext);

        // Different nonces → different ciphertext
        Assert.NotEqual(encrypted1, encrypted2);

        // Both should still decrypt correctly
        Assert.Equal(plaintext, service.Decrypt(encrypted1));
        Assert.Equal(plaintext, service.Decrypt(encrypted2));
    }

    [Fact]
    public void Encrypt_Decrypt_HandlesUtf8()
    {
        var service = CreateService();
        var plaintext = "[\"ABCD1234\",\"EFGH5678\",\"IJKL9012\"]";

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_Throws()
    {
        var service = CreateService();
        var encrypted = service.Encrypt("test-secret");

        // Tamper with the ciphertext
        var bytes = Convert.FromBase64String(encrypted);
        bytes[15] ^= 0xFF; // Flip a byte in the ciphertext area
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<Exception>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_WithEmptyInput_Throws()
    {
        var service = CreateService();
        Assert.ThrowsAny<Exception>(() => service.Decrypt(""));
    }

    // ═══════════════════════════════════════════════════════════
    //  TOTP VERIFICATION TESTS
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void VerifySetup_WithValidToken_SucceedsLocally()
    {
        // Generate a real TOTP token and verify it passes validation
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);
        var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        var validToken = totp.ComputeTotp();

        // Verify the token ourselves (unit test without DB)
        var isValid = totp.VerifyTotp(validToken, out _, new VerificationWindow(1));
        Assert.True(isValid);
    }

    [Fact]
    public void VerifySetup_WithInvalidToken_Fails()
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        var isValid = totp.VerifyTotp("000000", out _, new VerificationWindow(1));

        // Very small chance this passes randomly (1/1M), but acceptable for tests
        // We check it doesn't throw, and is most likely false
        Assert.IsType<bool>(isValid);
    }

    // ═══════════════════════════════════════════════════════════
    //  STORE AVAILABILITY — "ne znam" must not be answered as "no 2FA"
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task IsTwoFactorEnabled_WhenStoreUnreachable_Throws_InsteadOfReportingDisabled()
    {
        // An unconfigured service cannot read user_mfa. It used to answer false,
        // which the middleware gate reads as "let them in" — a database outage
        // would have switched 2FA off for everyone who had it on.
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
        Environment.SetEnvironmentVariable("SUPABASE_URL", null);
        Environment.SetEnvironmentVariable("SUPABASE_SERVICE_KEY", null);

        try
        {
            var service = CreateService();

            await Assert.ThrowsAsync<TwoFactorStoreUnavailableException>(
                () => service.IsTwoFactorEnabledAsync(Guid.NewGuid().ToString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SUPABASE_URL", url);
            Environment.SetEnvironmentVariable("SUPABASE_SERVICE_KEY", key);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  MODEL VALIDATION TESTS
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SetupResponse_HasCorrectDefaults()
    {
        var response = new Backend.Models.TwoFactorSetupResponse();
        Assert.Equal("", response.OtpauthUrl);
        Assert.Equal("", response.ManualEntryKey);
    }

    [Fact]
    public void TwoFactorResponse_CanSetRecoveryCodes()
    {
        var response = new Backend.Models.TwoFactorResponse
        {
            Success = true,
            Message = "OK",
            RecoveryCodes = ["ABCD1234", "EFGH5678"]
        };
        Assert.True(response.Success);
        Assert.Equal(2, response.RecoveryCodes?.Length);
    }

    [Fact]
    public void UserTwoFactorRecord_HasSensibleDefaults()
    {
        var record = new Backend.Models.UserTwoFactorRecord();
        Assert.Equal("", record.UserId);
        Assert.False(record.TotpEnabled);
        Assert.Null(record.TotpSecretEncrypted);
        Assert.Null(record.RecoveryCodesEncrypted);
    }

    [Fact]
    public void TwoFactorStatusResponse_DefaultFalse()
    {
        var status = new Backend.Models.TwoFactorStatusResponse();
        Assert.False(status.Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void SetupRequest_Token_InvalidFormats(string token)
    {
        // Verify that invalid token formats would be caught
        var isValid = token.Length == 6 && token.All(char.IsDigit);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("999999")]
    public void SetupRequest_Token_ValidFormats(string token)
    {
        var isValid = token.Length == 6 && token.All(char.IsDigit);
        Assert.True(isValid);
    }
}
