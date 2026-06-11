using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Secure registration service that proxies Supabase GoTrue Admin API.
/// 
/// Security guarantees:
///   • PREVENTS user enumeration: identical response for existing/new emails
///   • Generates 256-bit cryptographically secure verification tokens
///   • Sends verification email via IAuthEmailService (MailKit)
///   • Rate-limited at controller level
///   • All passwords hashed by Supabase (bcrypt) — never stored in our DB
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _frontendUrl;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Strict email regex — RFC 5322 simplified, rejects obvious garbage
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    // Password policy: min 8 chars, at least 1 uppercase, 1 lowercase, 1 digit
    private static readonly Regex PasswordPolicy = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    public AuthService(
        HttpClient httpClient,
        IAuthEmailService emailService,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _emailService = emailService;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];
        _frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? config["FrontendUrl"]
            ?? "http://localhost:3000";

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl) &&
                        !string.IsNullOrWhiteSpace(_serviceKey);

        if (!_isConfigured)
            _logger.LogWarning("AuthService: Supabase not configured. Registration disabled.");
    }

    // ═══════════════════════════════════════════════════════════
    //  Registration — Anti-Enumeration Design
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Register a new user. ALWAYS returns a generic success response,
    /// regardless of whether the email already exists.
    /// This prevents user enumeration attacks.
    /// </summary>
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var genericSuccess = new RegisterResponse
        {
            Success = true,
            Message = "Ako je email ispravan, primit ćete link za potvrdu. Provjerite i spam folder."
        };

        // ── 1. Validate email format ──
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        if (!IsValidEmail(email))
        {
            // Return generic response even for invalid emails to prevent probing
            _logger.LogInformation("AuthService: Invalid email format attempted: {EmailHash}",
                ComputeEmailHash(email));
            return genericSuccess;
        }

        // ── 2. Validate password strength ──
        if (string.IsNullOrWhiteSpace(request.Password) || !PasswordPolicy.IsMatch(request.Password))
        {
            // Leak this info — it's about input quality, not about account existence
            return new RegisterResponse
            {
                Success = false,
                Message = "Lozinka mora imati najmanje 8 znakova, jedno veliko slovo, jedno malo slovo i jednu znamenku."
            };
        }

        // ── 3. Validate name ──
        var fullName = request.FullName?.Trim() ?? "";
        if (fullName.Length < 2 || fullName.Length > 200)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Ime i prezime je obavezno (2–200 znakova)."
            };
        }

        if (!_isConfigured)
        {
            _logger.LogWarning("AuthService: Supabase not configured — cannot register.");
            return genericSuccess; // Don't leak infrastructure state
        }

        try
        {
            // ── 4. Check if user already exists (Supabase Admin API) ──
            var existingUser = await FindUserByEmail(email);
            if (existingUser != null)
            {
                // SECURITY: Log the attempt but return SAME response as successful registration
                _logger.LogInformation("AuthService: Registration attempt for existing email {EmailHash}",
                    ComputeEmailHash(email));

                // Optionally: send a "someone tried to register with your email" notification
                // but NEVER reveal this to the caller
                return genericSuccess;
            }

            // ── 5. Create user in Supabase Auth ──
            var userId = await CreateSupabaseUser(email, request.Password, fullName);
            if (userId == null)
            {
                _logger.LogError("AuthService: Supabase user creation failed for {EmailHash}",
                    ComputeEmailHash(email));
                return genericSuccess; // Don't leak internal failure
            }

            // ── 6. Generate cryptographic verification token ──
            var verificationToken = GenerateSecureToken();

            // Store token in user metadata for verification (via Supabase Admin API)
            await StoreVerificationToken(userId, verificationToken);

            // ── 7. Send verification email ──
            var verificationLink = $"{_frontendUrl!.TrimEnd('/')}/auth/verify?token={verificationToken}&email={Uri.EscapeDataString(email)}";
            var emailSent = await _emailService.SendVerificationEmailAsync(email, verificationLink);

            if (!emailSent)
                _logger.LogWarning("AuthService: Verification email failed to send for user {UserId}", userId);

            _logger.LogInformation("AuthService: User registered successfully {UserId}", userId);
            return genericSuccess;
        }
        catch (Exception ex)
        {
            // NEVER leak internal errors to the client
            _logger.LogError(ex, "AuthService: Registration failed for {EmailHash}",
                ComputeEmailHash(request.Email ?? ""));
            return genericSuccess;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Supabase Admin API helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Look up user by email via Supabase Admin API.
    /// Returns user data if found, null if not.
    /// </summary>
    private async Task<JsonElement?> FindUserByEmail(string email)
    {
        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users?page=1&per_page=1";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Supabase Admin API returns { users: [...] }
            if (doc.RootElement.TryGetProperty("users", out var users))
            {
                foreach (var user in users.EnumerateArray())
                {
                    if (user.TryGetProperty("email", out var emailProp) &&
                        emailProp.GetString()?.Equals(email, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return user;
                    }
                }
            }

            // Fallback: direct email filter (newer Supabase versions)
            // GET /auth/v1/admin/users?filter=email:eq:user@example.com
            var filterUrl = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users" +
                $"?page=1&per_page=1&filter=email%3Aeq%3A{Uri.EscapeDataString(email)}";
            var filterReq = new HttpRequestMessage(HttpMethod.Get, filterUrl);
            filterReq.Headers.Add("apikey", _serviceKey);
            filterReq.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var filterResp = await _httpClient.SendAsync(filterReq);
            if (!filterResp.IsSuccessStatusCode) return null;

            var filterBody = await filterResp.Content.ReadAsStringAsync();
            using var filterDoc = JsonDocument.Parse(filterBody);

            if (filterDoc.RootElement.TryGetProperty("users", out var filteredUsers)
                && filteredUsers.GetArrayLength() > 0)
            {
                return filteredUsers[0];
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthService: Failed to query Supabase for user existence");
            return null;
        }
    }

    /// <summary>
    /// Create user via Supabase Admin API (service_role).
    /// Sets email_confirm = false so user must verify via our token.
    /// </summary>
    private async Task<string?> CreateSupabaseUser(string email, string password, string fullName)
    {
        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users";
            var payload = new
            {
                email,
                password,
                email_confirm = false, // Don't auto-confirm — require verification
                user_metadata = new { full_name = fullName }
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AuthService: Supabase CreateUser failed ({Status}): {Body}",
                    response.StatusCode, body.Length > 500 ? body[..500] : body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
                return idProp.GetString();

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthService: Exception creating Supabase user");
            return null;
        }
    }

    /// <summary>
    /// Store verification token in user's app_metadata via Admin API.
    /// Token is hashed (SHA-256) before storage — only the hash is stored.
    /// </summary>
    private async Task StoreVerificationToken(string userId, string token)
    {
        try
        {
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
            var expiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();

            var url = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users/{userId}";
            var payload = new
            {
                app_metadata = new
                {
                    verification_token_hash = tokenHash,
                    verification_expires_at = expiresAt
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("AuthService: Failed to store verification token for {UserId}: {Body}",
                    userId, body.Length > 500 ? body[..500] : body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthService: Exception storing verification token for {UserId}", userId);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Email Verification
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Verify email using the token from the verification link.
    /// Compares SHA-256(token) against the stored hash.
    /// </summary>
    public async Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var token = request.Token?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return new VerifyEmailResponse { Success = false, Message = "Nevažeći verifikacijski link." };
        }

        try
        {
            var user = await FindUserByEmail(email);
            if (user == null)
            {
                return new VerifyEmailResponse { Success = false, Message = "Nevažeći verifikacijski link." };
            }

            var userId = user.Value.GetProperty("id").GetString()!;

            // Get stored token hash from app_metadata
            if (!user.Value.TryGetProperty("app_metadata", out var metadata))
            {
                return new VerifyEmailResponse { Success = false, Message = "Verifikacijski token istekao." };
            }

            var storedHash = metadata.TryGetProperty("verification_token_hash", out var hashProp)
                ? hashProp.GetString() : null;
            var expiresAt = metadata.TryGetProperty("verification_expires_at", out var expProp)
                ? expProp.GetInt64() : 0L;

            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return new VerifyEmailResponse { Success = false, Message = "Email je već potvrđen ili link nije valjan." };
            }

            // Check expiry
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
            {
                return new VerifyEmailResponse { Success = false, Message = "Verifikacijski link je istekao. Zatražite novi." };
            }

            // Constant-time comparison of token hashes
            var providedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedHash),
                Encoding.UTF8.GetBytes(storedHash)))
            {
                return new VerifyEmailResponse { Success = false, Message = "Nevažeći verifikacijski token." };
            }

            // Mark email as confirmed + clear the token
            await ConfirmUserEmail(userId);

            _logger.LogInformation("AuthService: Email verified for user {UserId}", userId);
            return new VerifyEmailResponse
            {
                Success = true,
                Message = "Email adresa je uspješno potvrđena! Možete se prijaviti."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthService: Email verification failed");
            return new VerifyEmailResponse { Success = false, Message = "Greška pri verifikaciji. Pokušajte ponovo." };
        }
    }

    /// <summary>
    /// Confirm user email via Supabase Admin API and clear verification token.
    /// </summary>
    private async Task ConfirmUserEmail(string userId)
    {
        var url = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users/{userId}";
        var payload = new
        {
            email_confirm = true,
            app_metadata = new
            {
                verification_token_hash = (string?)null,
                verification_expires_at = (long?)null
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("apikey", _serviceKey);
        request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("AuthService: Failed to confirm email for {UserId}: {Body}", userId, body);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Validate email format with both regex and .NET MailAddress parser.
    /// Double validation to catch edge cases.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;

        // .NET built-in check
        if (!MailAddress.TryCreate(email, out _))
            return false;

        // Regex for stricter format validation
        try
        {
            return EmailRegex.IsMatch(email);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Generate a 256-bit (32 byte) cryptographically secure token, URL-safe base64 encoded.
    /// </summary>
    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Hash email for logging (never log raw emails in production).
    /// </summary>
    private static string ComputeEmailHash(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}
