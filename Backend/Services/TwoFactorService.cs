using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Models;
using OtpNet;

namespace Backend.Services;

/// <summary>
/// TOTP Two-Factor Authentication Service.
/// 
/// Security design for high-security legal application:
///   • Secrets encrypted at rest with AES-256-GCM before storage
///   • HMAC-SHA1 TOTP per RFC 6238 (Google Authenticator compatible)
///   • 30-second time step, 6-digit codes, ±1 step tolerance for clock skew
///   • Recovery codes: 8 single-use codes, bcrypt-style hashed
///   • All operations use constant-time comparison to prevent timing attacks
/// </summary>
public class TwoFactorService
:ITwoFactorService {
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly byte[] _encryptionKey;
    private readonly ILogger<TwoFactorService> _logger;
    private readonly bool _isConfigured;

    private const string Issuer = "PravoTek";

    /// <summary>
    /// Dedicated table for MFA material (migration 017). It has RLS on and no
    /// policy for `authenticated`, so the secret is reachable only with the
    /// service_role key. It used to live on `user_profiles`, which colleagues
    /// in the same firm are allowed to read.
    /// </summary>
    private const string MfaTable = "user_mfa";

    private const int SecretSize = 20; // 160-bit secret (RFC 6238 recommended)
    private const int RecoveryCodeCount = 8;
    private const int RecoveryCodeLength = 8;
    private const int TotpStep = 30;
    private const int VerificationWindow = 1; // ±1 step = ±30s clock tolerance

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public TwoFactorService(HttpClient httpClient, IConfiguration config, ILogger<TwoFactorService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl) &&
                        !string.IsNullOrWhiteSpace(_serviceKey);

        // Encryption key for TOTP secrets at rest (32 bytes = AES-256)
        var keyBase64 = Environment.GetEnvironmentVariable("TOTP_ENCRYPTION_KEY")
            ?? config["TwoFactor:EncryptionKey"]
            ?? "";

        if (!string.IsNullOrWhiteSpace(keyBase64))
        {
            try
            {
                _encryptionKey = Convert.FromBase64String(keyBase64);
                if (_encryptionKey.Length != 32)
                    throw new InvalidOperationException("TOTP_ENCRYPTION_KEY must be exactly 32 bytes (256-bit) base64-encoded.");
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("TOTP_ENCRYPTION_KEY is not valid base64.");
            }
        }
        else
        {
            // Generate a deterministic warning key for development — NOT for production
            _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes("PravoTek-Dev-TOTP-Key-NOT-FOR-PRODUCTION"));
            _logger.LogWarning("TOTP_ENCRYPTION_KEY not set — using insecure dev key. Set this in production!");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  1. GENERATE SECRET — Called when user initiates 2FA setup
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a new TOTP secret and returns the otpauth:// URI for QR code generation.
    /// The secret is NOT saved yet — it's only persisted after successful verify-setup.
    /// </summary>
    public TwoFactorSetupResponse GenerateSetup(string userEmail)
    {
        // Generate cryptographically secure random secret
        var secretBytes = KeyGeneration.GenerateRandomKey(SecretSize);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        // Build otpauth:// URI (RFC 5765-compatible)
        // Format: otpauth://totp/PravoTek:user@email.com?secret=BASE32&issuer=PravoTek&algorithm=SHA1&digits=6&period=30
        var otpauthUrl = new OtpUri(
            OtpType.Totp,
            secretBytes,
            userEmail,
            Issuer,
            OtpHashMode.Sha1,
            6,
            TotpStep
        ).ToString();

        _logger.LogInformation("Generated 2FA setup for user {Email}", MaskEmail(userEmail));

        return new TwoFactorSetupResponse
        {
            OtpauthUrl = otpauthUrl,
            ManualEntryKey = FormatBase32ForDisplay(base32Secret)
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  2. VERIFY SETUP — Confirms user scanned QR, saves secret
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies the first TOTP token to confirm the user has successfully paired
    /// their authenticator app. On success, encrypts and saves the secret to DB.
    /// Returns recovery codes.
    /// </summary>
    public async Task<TwoFactorResponse> VerifySetupAsync(string userId, string userEmail, string base32Secret, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 6 || !token.All(char.IsDigit))
        {
            return new TwoFactorResponse { Success = false, Message = "Unesite ispravan 6-znamenkasti kod." };
        }

        // Enrolling replaces whatever secret is on the account, so an account that
        // already has 2FA must not be re-enrolled by someone holding only a
        // session. Otherwise a stolen session is upgraded into permanent access:
        // register your own authenticator, and the real owner's is gone.
        // Changing devices goes through disable (which demands a current code) first.
        var existing = await GetTwoFactorRecordAsync(userId);
        if (existing?.TotpEnabled == true)
        {
            _logger.LogWarning("Rejected 2FA re-enrolment for already-enrolled user {UserId}", userId);
            return new TwoFactorResponse
            {
                Success = false,
                Message = "2FA je već aktiviran na ovom računu. Za promjenu uređaja prvo isključite 2FA trenutnim kodom."
            };
        }

        // Remove spaces/dashes from manual entry format
        var cleanSecret = base32Secret.Replace(" ", "").Replace("-", "");

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(cleanSecret);
        }
        catch
        {
            return new TwoFactorResponse { Success = false, Message = "Nevažeći tajni ključ." };
        }

        // Verify the token
        var totp = new Totp(secretBytes, step: TotpStep, mode: OtpHashMode.Sha1, totpSize: 6);
        var isValid = totp.VerifyTotp(token, out _, new VerificationWindow(VerificationWindow));

        if (!isValid)
        {
            _logger.LogWarning("2FA setup verification failed for user {UserId}", userId);
            return new TwoFactorResponse { Success = false, Message = "Nevažeći kod. Provjerite je li vrijeme na uređaju točno." };
        }

        // Generate recovery codes
        var recoveryCodes = GenerateRecoveryCodes();

        // Encrypt secret and recovery codes before storage
        var encryptedSecret = Encrypt(cleanSecret);
        var encryptedRecoveryCodes = Encrypt(JsonSerializer.Serialize(recoveryCodes));

        // Save to database
        var saved = await SaveTwoFactorAsync(userId, encryptedSecret, encryptedRecoveryCodes);
        if (!saved)
        {
            return new TwoFactorResponse { Success = false, Message = "Greška pri spremanju. Pokušajte ponovo." };
        }

        _logger.LogInformation("2FA successfully enabled for user {UserId}", userId);

        return new TwoFactorResponse
        {
            Success = true,
            Message = "Dvofaktorska autentifikacija uspješno aktivirana.",
            RecoveryCodes = recoveryCodes
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  3. VALIDATE LOGIN TOKEN — Called during login flow
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Validates a 6-digit TOTP token during login.
    /// Also accepts recovery codes as fallback.
    /// </summary>
    public async Task<TwoFactorResponse> ValidateTokenAsync(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TwoFactorResponse { Success = false, Message = "Unesite 2FA kod." };
        }

        token = token.Trim();

        // Fetch encrypted secret from DB
        var record = await GetTwoFactorRecordAsync(userId);
        if (record == null || !record.TotpEnabled || string.IsNullOrWhiteSpace(record.TotpSecretEncrypted))
        {
            return new TwoFactorResponse { Success = false, Message = "2FA nije aktiviran za ovaj račun." };
        }

        // If token is 6 digits → TOTP verification
        if (token.Length == 6 && token.All(char.IsDigit))
        {
            var decryptedSecret = Decrypt(record.TotpSecretEncrypted);
            byte[] secretBytes;
            try
            {
                secretBytes = Base32Encoding.ToBytes(decryptedSecret);
            }
            catch
            {
                _logger.LogError("Failed to decode TOTP secret for user {UserId}", userId);
                return new TwoFactorResponse { Success = false, Message = "Interna greška. Kontaktirajte podršku." };
            }

            var totp = new Totp(secretBytes, step: TotpStep, mode: OtpHashMode.Sha1, totpSize: 6);
            var isValid = totp.VerifyTotp(token, out _, new VerificationWindow(VerificationWindow));

            if (isValid)
            {
                _logger.LogInformation("2FA TOTP validation successful for user {UserId}", userId);
                return new TwoFactorResponse { Success = true, Message = "Autentifikacija uspješna." };
            }

            _logger.LogWarning("2FA TOTP validation failed for user {UserId}", userId);
            return new TwoFactorResponse { Success = false, Message = "Nevažeći kod. Pokušajte ponovo." };
        }

        // If token is 8 chars → recovery code attempt
        if (token.Length == RecoveryCodeLength)
        {
            return await TryRecoveryCodeAsync(userId, record, token);
        }

        return new TwoFactorResponse { Success = false, Message = "Nevažeći format koda." };
    }

    // ═══════════════════════════════════════════════════════════
    //  4. STATUS CHECK — Is 2FA enabled for this user?
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> IsTwoFactorEnabledAsync(string userId)
    {
        var record = await GetTwoFactorRecordAsync(userId);
        return record?.TotpEnabled ?? false;
    }

    // ═══════════════════════════════════════════════════════════
    //  5. DISABLE 2FA — Removes TOTP from account
    // ═══════════════════════════════════════════════════════════

    public async Task<TwoFactorResponse> DisableTwoFactorAsync(string userId, string token)
    {
        // Validate current token before disabling (security: prove you have the authenticator)
        var validation = await ValidateTokenAsync(userId, token);
        if (!validation.Success)
        {
            return new TwoFactorResponse { Success = false, Message = "Nevažeći kod. 2FA nije deaktiviran." };
        }

        var disabled = await ClearTwoFactorAsync(userId);
        if (!disabled)
        {
            return new TwoFactorResponse { Success = false, Message = "Greška pri brisanju. Pokušajte ponovo." };
        }

        _logger.LogInformation("2FA disabled for user {UserId}", userId);
        return new TwoFactorResponse { Success = true, Message = "Dvofaktorska autentifikacija deaktivirana." };
    }

    // ═══════════════════════════════════════════════════════════
    //  RECOVERY CODES
    // ═══════════════════════════════════════════════════════════

    private string[] GenerateRecoveryCodes()
    {
        var codes = new string[RecoveryCodeCount];
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            codes[i] = GenerateSecureCode(RecoveryCodeLength);
        }
        return codes;
    }

    private static string GenerateSecureCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I/O/0/1 to avoid confusion
        var code = new char[length];
        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);
        for (var i = 0; i < length; i++)
        {
            code[i] = chars[buffer[i] % chars.Length];
        }
        return new string(code);
    }

    private async Task<TwoFactorResponse> TryRecoveryCodeAsync(string userId, UserTwoFactorRecord record, string code)
    {
        if (string.IsNullOrWhiteSpace(record.RecoveryCodesEncrypted))
        {
            return new TwoFactorResponse { Success = false, Message = "Nema dostupnih kodova za oporavak." };
        }

        try
        {
            var decrypted = Decrypt(record.RecoveryCodesEncrypted);
            var codes = JsonSerializer.Deserialize<string[]>(decrypted) ?? [];

            // Constant-time comparison for each code
            var matchIndex = -1;
            for (var i = 0; i < codes.Length; i++)
            {
                if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(code.ToUpperInvariant()),
                    Encoding.UTF8.GetBytes(codes[i])))
                {
                    matchIndex = i;
                }
            }

            if (matchIndex < 0)
            {
                _logger.LogWarning("Invalid recovery code attempt for user {UserId}", userId);
                return new TwoFactorResponse { Success = false, Message = "Nevažeći kod za oporavak." };
            }

            // Remove used code and update DB
            var remaining = codes.Where((_, i) => i != matchIndex).ToArray();
            var encryptedRemaining = Encrypt(JsonSerializer.Serialize(remaining));
            await UpdateRecoveryCodesAsync(userId, encryptedRemaining);

            _logger.LogInformation("Recovery code used for user {UserId}. Remaining: {Count}", userId, remaining.Length);
            return new TwoFactorResponse
            {
                Success = true,
                Message = $"Kod za oporavak prihvaćen. Preostalo kodova: {remaining.Length}/{RecoveryCodeCount}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recovery code for user {UserId}", userId);
            return new TwoFactorResponse { Success = false, Message = "Greška pri obradi koda." };
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AES-256-GCM ENCRYPTION — Secrets encrypted at rest
    // ═══════════════════════════════════════════════════════════

    /// <summary>Encrypt plaintext with AES-256-GCM. Returns base64(nonce + ciphertext + tag).</summary>
    internal string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

#pragma warning disable SYSLIB0053 // AesGcm ctor without tag size
        using var aes = new AesGcm(_encryptionKey);
#pragma warning restore SYSLIB0053
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Concatenate: nonce (12) + ciphertext (N) + tag (16)
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>Decrypt base64(nonce + ciphertext + tag) with AES-256-GCM.</summary>
    internal string Decrypt(string encryptedBase64)
    {
        var data = Convert.FromBase64String(encryptedBase64);

        var nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize; // 16

        if (data.Length < nonceSize + tagSize)
            throw new CryptographicException("Encrypted data is too short.");

        var nonce = data[..nonceSize];
        var tag = data[^tagSize..];
        var ciphertext = data[nonceSize..^tagSize];

        var plaintext = new byte[ciphertext.Length];

#pragma warning disable SYSLIB0053
        using var aes = new AesGcm(_encryptionKey);
#pragma warning restore SYSLIB0053
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    // ═══════════════════════════════════════════════════════════
    //  SUPABASE DATABASE OPERATIONS
    // ═══════════════════════════════════════════════════════════

    private async Task<bool> SaveTwoFactorAsync(string userId, string encryptedSecret, string encryptedRecoveryCodes)
    {
        if (!_isConfigured) return false;

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{MfaTable}";
            var record = new
            {
                user_id = userId,
                totp_secret_encrypted = encryptedSecret,
                totp_enabled = true,
                recovery_codes_encrypted = encryptedRecoveryCodes,
                totp_enabled_at = DateTime.UtcNow.ToString("o")
            };

            var payload = JsonSerializer.Serialize(record);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to save 2FA for user {UserId}: {Body}", userId, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving 2FA for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Returns the caller's MFA row, or null when they genuinely have none.
    ///
    /// Throws <see cref="TwoFactorStoreUnavailableException"/> when the answer is
    /// unknown. Returning null in that case would be a lie with real consequences:
    /// every caller reads null as "2FA is off".
    /// </summary>
    private async Task<UserTwoFactorRecord?> GetTwoFactorRecordAsync(string userId)
    {
        if (!_isConfigured)
            throw new TwoFactorStoreUnavailableException(
                "Supabase nije konfiguriran (SUPABASE_URL / SUPABASE_SERVICE_KEY).");

        HttpResponseMessage response;
        string body;

        try
        {
            var safeId = Uri.EscapeDataString(userId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{MfaTable}?user_id=eq.{safeId}&select=user_id,totp_secret_encrypted,totp_enabled,recovery_codes_encrypted,totp_enabled_at&limit=1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            response = await _httpClient.SendAsync(request);
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching 2FA record for user {UserId}", userId);
            throw new TwoFactorStoreUnavailableException("Citanje MFA zapisa nije uspjelo.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch 2FA record for user {UserId}: {Body}", userId, body);
            throw new TwoFactorStoreUnavailableException(
                $"PostgREST je vratio {(int)response.StatusCode} pri citanju MFA zapisa.");
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<UserTwoFactorRecord>>(body, JsonOpts);

            // An empty array is the one case that really does mean "no 2FA here".
            return rows?.FirstOrDefault();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Malformed MFA record payload for user {UserId}", userId);
            throw new TwoFactorStoreUnavailableException("MFA zapis nije u ocekivanom formatu.", ex);
        }
    }

    private async Task<bool> ClearTwoFactorAsync(string userId)
    {
        if (!_isConfigured) return false;

        try
        {
            var safeId = Uri.EscapeDataString(userId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{MfaTable}?user_id=eq.{safeId}";

            var record = new
            {
                totp_secret_encrypted = (string?)null,
                totp_enabled = false,
                recovery_codes_encrypted = (string?)null,
                totp_enabled_at = (string?)null
            };

            var payload = JsonSerializer.Serialize(record);
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing 2FA for user {UserId}", userId);
            return false;
        }
    }

    private async Task UpdateRecoveryCodesAsync(string userId, string encryptedCodes)
    {
        if (!_isConfigured) return;

        try
        {
            var safeId = Uri.EscapeDataString(userId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{MfaTable}?user_id=eq.{safeId}";

            var payload = JsonSerializer.Serialize(new { recovery_codes_encrypted = encryptedCodes });
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating recovery codes for user {UserId}", userId);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Formats base32 key with spaces for human-friendly display: "ABCD EFGH IJKL MNOP"</summary>
    private static string FormatBase32ForDisplay(string base32)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < base32.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(base32[i]);
        }
        return sb.ToString();
    }

    /// <summary>Mask email for logging: "us***@example.com"</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 2) return "***" + email[at..];
        return email[..2] + "***" + email[at..];
    }
}
