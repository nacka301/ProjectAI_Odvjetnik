namespace Backend.Services;

/// <summary>
/// The MFA store could not be read — not configured, PostgREST refused, or the
/// call threw.
///
/// This exists to keep "this account has no 2FA" and "we could not find out"
/// apart. They used to collapse into the same <c>false</c>, which meant a
/// database blip reported two-factor as switched off for everyone who had it on,
/// and the frontend gate — which trusts that answer to decide whether to let a
/// user in — had no way to tell the difference.
/// </summary>
public sealed class TwoFactorStoreUnavailableException : Exception
{
    public TwoFactorStoreUnavailableException(string message) : base(message) { }

    public TwoFactorStoreUnavailableException(string message, Exception inner)
        : base(message, inner) { }
}
