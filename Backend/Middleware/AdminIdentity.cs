namespace Backend.Middleware;

/// <summary>
/// Who counts as the administrator.
///
/// The answer is one e-mail address in ADMIN_EMAIL, compared against the "email"
/// claim of a verified JWT. Not a database role, not a claim the token can set
/// about itself — an address configured on the host.
///
/// This used to live inside AdminGuardMiddleware. It moved out when a second
/// consumer needed the same answer: two copies of "am I talking to the admin"
/// is exactly the kind of pair that drifts, and the half that drifts silently
/// is the one that stops letting the admin in.
///
/// FAIL CLOSED: with no ADMIN_EMAIL configured, nobody is the admin. That shuts
/// admin tooling and in-development modules for everyone, which is the safe
/// direction — the unsafe one would open them to everyone.
/// </summary>
public sealed class AdminIdentity
{
    private readonly string? _adminEmail;

    public AdminIdentity(IConfiguration config)
    {
        _adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")
            ?? config["Admin:Email"];
    }

    /// <summary>Adresa administratora — kamo idu obavijesti poput prijava grešaka. Null kad nije postavljena.</summary>
    public string? Email => string.IsNullOrWhiteSpace(_adminEmail) ? null : _adminEmail;

    /// <summary>True when an admin address is configured at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_adminEmail);

    /// <summary>True when this address is the administrator's.</summary>
    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(_adminEmail)
        && !string.IsNullOrWhiteSpace(email)
        && string.Equals(email, _adminEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the caller on this request is the administrator.</summary>
    public bool IsAdminRequest(HttpContext context) =>
        IsAdmin(context.Items["AuthEmail"] as string);
}
