namespace Backend.Services;

/// <summary>
/// Loads the Supabase signing keys once, at startup, instead of on the first
/// request that needs them.
///
/// Why this exists: on a freshly booted container the key set is empty, so the
/// first ES256 token pays for the JWKS fetch — measured at 3.6 s on Render
/// against a 5 s fetch timeout, on a host whose cold start took 12.3 s to serve
/// a health check. Losing that race refuses tokens that are perfectly valid, and
/// the user sees it as "your sign-in was rejected".
///
/// It deliberately does NOT block startup and does NOT throw. Failing to warm up
/// is not a reason to refuse to boot — <see cref="SupabaseJwksProvider"/> retries
/// on demand, and a backend that is up and briefly blind is strictly better than
/// one that will not start. The log line is the point: an operator can see, at
/// boot, whether this instance can verify tokens at all.
/// </summary>
public sealed class SupabaseJwksWarmUp(
    ISupabaseJwksProvider jwks,
    ILogger<SupabaseJwksWarmUp> logger) : IHostedService
{
    /// <summary>Bounded so a hanging Supabase cannot hold the startup task open.</summary>
    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromSeconds(20);

    private Task? _warmUp;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!jwks.IsConfigured)
        {
            logger.LogWarning(
                "SUPABASE_URL nije postavljen — ES256 tokeni se ne mogu verificirati. "
                + "Ako projekt koristi asimetricne kljuceve, svaka prijava ce dobiti 503.");
            return Task.CompletedTask;
        }

        _warmUp = WarmUpAsync();
        return Task.CompletedTask;
    }

    private async Task WarmUpAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(WarmUpTimeout);
            var loaded = await jwks.EnsureLoadedAsync(cts.Token);

            if (loaded)
                logger.LogInformation("JWKS warm-up OK — instanca moze verificirati ES256 tokene.");
            else
                logger.LogError(
                    "JWKS warm-up NIJE uspio — do prvog uspjesnog dohvata ES256 tokeni dobivaju 503.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JWKS warm-up je pukao — nastavljam, dohvat ce se ponoviti na zahtjev.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Awaited on shutdown only so the task does not outlive the host unobserved.
        if (_warmUp != null)
        {
            try { await _warmUp.WaitAsync(cancellationToken); }
            catch { /* shutdown is not the place to care */ }
        }
    }
}
