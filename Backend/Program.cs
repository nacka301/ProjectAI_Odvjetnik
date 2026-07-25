using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.Middleware;
using Backend.Models;
using Backend.Services;
using Backend.Validators;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Disable file watchers in production (causes segfault on Render/Docker)
    ContentRootPath = AppContext.BaseDirectory
});

// V20d: Configure Kestrel for long-running SSE connections (deep dive can take 60-120s)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    // Disable minimum data rate — SSE streams have long pauses during deep dive
    options.Limits.MinResponseDataRate = null;
    options.Limits.MinRequestBodyDataRate = null;
});

// Disable config reload — Render doesn't support file watchers
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// --- Services ---

// Controllers are registered as services so ValidateOnBuild (below) also inspects
// their constructors — a missing DI registration then fails at startup instead of
// surfacing as a 500 on the first request that hits the endpoint.
builder.Services.AddControllers()
    .AddControllersAsServices()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// Fail fast on an incomplete DI graph, in every environment (not just Development).
builder.Host.UseDefaultServiceProvider(opts =>
{
    opts.ValidateOnBuild = true;
    opts.ValidateScopes = true;
});

// FluentValidation — auto-register all validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<PropertyInputBaseValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — allow the Next.js frontend
// Supports: FRONTEND_URL env var, multiple comma-separated URLs, and Vercel preview deploys
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
var allowedOrigins = frontendUrl
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(u => u.TrimEnd('/'))
    .ToArray();

Console.WriteLine($"[CORS] Allowed origins: {string.Join(", ", allowedOrigins)}");
Console.WriteLine($"[CORS] Vercel preview deploys also allowed (projectai|odvjetnik|nacka)");

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                // Reject null/empty origins in production (prevents CSRF from curl/scripts)
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                // Always allow configured origins
                if (allowedOrigins.Any(ao => ao.Equals(origin, StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Try parse — reject malformed origins gracefully
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                // Allow Vercel preview deploys ONLY for this project
                if (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                    && (uri.Host.Contains("projectai", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("odvjetnik", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("nacka", StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Allow localhost for development
                if (uri.Host is "localhost" or "127.0.0.1")
                    return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── Rate Limiting — prevent API credit drain ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Valuation endpoints: 5 requests per 10 minutes per IP
    options.AddFixedWindowLimiter("valuation", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;
    });

    // General API: 30 requests per minute per IP
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });

    // Auth endpoints: 5 requests per 10 minutes per IP (anti-brute-force)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Previše zahtjeva. Pokušajte ponovno za nekoliko minuta.\"}",
            cancellationToken);
    };
});

// V51: OpenRouter gateway — single API key for all AI providers
// Priority: env var > appsettings.json (env vars override placeholder values)
var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
    ?? Environment.GetEnvironmentVariable("OpenRouter__ApiKey")
    ?? builder.Configuration["OpenRouter:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenRouter API key is not configured. Set OPENROUTER_API_KEY environment variable.");

// Guard against placeholder values left in appsettings.json
if (openRouterKey.StartsWith("YOUR_") || string.IsNullOrWhiteSpace(openRouterKey))
    throw new InvalidOperationException(
        "OpenRouter API key is a placeholder. Set OPENROUTER_API_KEY environment variable.");

var baseUrl = builder.Configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";
var fastModel = builder.Configuration["OpenRouter:FastModel"] ?? "openai/gpt-4o-mini";
var heavyModel = builder.Configuration["OpenRouter:HeavyModel"] ?? "deepseek/deepseek-chat-v3-0324";
var reasoningModel = builder.Configuration["OpenRouter:ReasoningModel"] ?? "deepseek/deepseek-chat-v3-0324";
// NOTE ON REGISTRATION STYLE
// Every service is registered twice: once by its concrete type, once by its interface,
// with the interface forwarding to the same registration via GetRequiredService.
// Consumers currently inject a mix of both (controllers mostly take concrete types,
// services mostly take interfaces), so registering only one side leaves the other
// unresolvable. The forwarding lambda guarantees both names reach one instance.
builder.Services.AddSingleton(new ChatClientProvider(openRouterKey, baseUrl, fastModel, heavyModel, reasoningModel));

builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<IPdfService>(sp => sp.GetRequiredService<PdfService>());

builder.Services.AddScoped<ValuationService>();
builder.Services.AddScoped<IValuationService>(sp => sp.GetRequiredService<ValuationService>());

builder.Services.AddScoped<TextAnalysisService>();
builder.Services.AddScoped<ITextAnalysisService>(sp => sp.GetRequiredService<TextAnalysisService>());

builder.Services.AddScoped<ValuationAgentService>();
builder.Services.AddScoped<IValuationAgentService>(sp => sp.GetRequiredService<ValuationAgentService>());

builder.Services.AddScoped<ContractAnalysisService>();
builder.Services.AddScoped<IContractAnalysisService>(sp => sp.GetRequiredService<ContractAnalysisService>());

// ChronologyService does not implement IChronologyService (the interface is declared but
// unimplemented and currently has no consumers) — concrete registration only.
builder.Services.AddScoped<ChronologyService>();

builder.Services.AddScoped<ValuationEngine>();
builder.Services.AddScoped<IValuationEngine>(sp => sp.GetRequiredService<ValuationEngine>());

builder.Services.AddScoped<ZkParsingService>();
builder.Services.AddScoped<IZkParsingService>(sp => sp.GetRequiredService<ZkParsingService>());

builder.Services.AddScoped<LegalIntelligenceService>();
builder.Services.AddScoped<ILegalIntelligenceService>(sp => sp.GetRequiredService<LegalIntelligenceService>());

builder.Services.AddSingleton<NormValidityService>();
builder.Services.AddSingleton<INormValidityService>(sp => sp.GetRequiredService<NormValidityService>());

builder.Services.AddScoped<InvoiceService>();

builder.Services.AddScoped<TranslationService>();
builder.Services.AddScoped<ITranslationService>(sp => sp.GetRequiredService<TranslationService>());

// ── Odvjetnička tarifa (stateless calculators) ──
builder.Services.AddSingleton<TroskovnikService>();
builder.Services.AddSingleton<ITroskovnikService>(sp => sp.GetRequiredService<TroskovnikService>());
builder.Services.AddScoped<TroskovnikDocumentService>();
builder.Services.AddScoped<ITroskovnikDocumentService>(sp => sp.GetRequiredService<TroskovnikDocumentService>());

builder.Services.AddHttpClient<EmailService>();

// ── Resend transactional email (MailKit SMTP → smtp.resend.com:465) ──
// Implements two interfaces — both must forward to the single singleton instance.
builder.Services.AddSingleton<ResendEmailService>();
builder.Services.AddSingleton<IResendEmailService>(sp => sp.GetRequiredService<ResendEmailService>());
builder.Services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<ResendEmailService>());

// ── Auth registration (MailKit email + Supabase Admin API) ──
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.AddSingleton<AuthEmailService>();
builder.Services.AddSingleton<IAuthEmailService>(sp => sp.GetRequiredService<AuthEmailService>());
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());

// ── Audit logging (GDPR-safe, fire-and-forget) ──
builder.Services.AddHttpClient<AuditLogService>();
builder.Services.AddScoped<IAuditLogService>(sp => sp.GetRequiredService<AuditLogService>());

// ── Tenant resolution (user → law_firm, cached 5 min) ──
builder.Services.AddHttpClient<TenantResolver>();
builder.Services.AddScoped<ITenantResolver>(sp => sp.GetRequiredService<TenantResolver>());
builder.Services.AddHttpClient<EmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<EmbeddingService>());

builder.Services.AddHttpClient<LawRAGService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddScoped<ILawRAGService>(sp => sp.GetRequiredService<LawRAGService>());

builder.Services.AddHttpClient<MarketResearchService>(client =>
{
    // V20d: Increased timeout — deep dive of 20+ listings can take 60-120s total
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<IMarketResearchService>(sp => sp.GetRequiredService<MarketResearchService>());

builder.Services.AddHttpClient<SupabaseService>();
builder.Services.AddScoped<ISupabaseService>(sp => sp.GetRequiredService<SupabaseService>());

builder.Services.AddHttpClient<TwoFactorService>();
builder.Services.AddScoped<ITwoFactorService>(sp => sp.GetRequiredService<TwoFactorService>());

builder.Services.AddHttpClient<SettingsService>();
builder.Services.AddScoped<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());

builder.Services.AddHttpClient<CreditService>();
builder.Services.AddScoped<ICreditService>(sp => sp.GetRequiredService<CreditService>());

builder.Services.AddHttpClient<UsageService>();
builder.Services.AddScoped<IUsageService>(sp => sp.GetRequiredService<UsageService>());

builder.Services.AddHttpClient<ClientCaseService>();
builder.Services.AddScoped<IClientCaseService>(sp => sp.GetRequiredService<ClientCaseService>());

// ── DataSeeder (admin-only, uses ClientCaseService) ──
builder.Services.AddScoped<DataSeederService>();
builder.Services.AddHttpClient<PerplexityAuditService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<IPerplexityAuditService>(sp => sp.GetRequiredService<PerplexityAuditService>());

builder.Services.AddHttpClient<PerplexityTestValidationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddScoped<IPerplexityTestValidationService>(sp => sp.GetRequiredService<PerplexityTestValidationService>());

var app = builder.Build();

// --- Middleware ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

// ── Supabase JWT Authentication Middleware ──
// Verifies Bearer token, extracts user_id/email from JWT claims.
// Resolves TenantId via ITenantResolver (cached).
// In dev (no SUPABASE_JWT_SECRET): falls back to x-user-id header.
app.UseMiddleware<SupabaseJwtMiddleware>();

// ── Admin Guard Middleware — blocks non-admin users from /api/rag/, /api/admin/, etc. ──
// MUST run after SupabaseJwtMiddleware (needs AuthEmail in HttpContext.Items).
app.UseMiddleware<AdminGuardMiddleware>();

// ── Audit Logging Middleware ──
// Logs: userId, tenantId, endpoint, HTTP method, status code, duration.
// NEVER logs request/response bodies (GDPR + attorney-client privilege).
app.UseMiddleware<AuditLoggingMiddleware>();

// ── API Key Authentication Middleware (optional, activate via API_KEY env var) ──
var apiKey = Environment.GetEnvironmentVariable("API_KEY");
if (!string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("[AUTH] API_KEY middleware is ACTIVE — all requests (except health/OPTIONS) require X-API-Key header");
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for health check and CORS preflight
        if (path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Method == "OPTIONS")
        {
            await next();
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedKey.ToString()),
                Encoding.UTF8.GetBytes(apiKey)))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Nevaljan ili nedostaje API ključ.\"}");
            return;
        }

        await next();
    });
}

app.UseRateLimiter();

// Only redirect to HTTPS in development; Render handles TLS at the proxy level
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

// In production (Docker/Render), bind to PORT env var.
// In development, launchSettings.json controls the URL.
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}

