using Backend.Services;

namespace Backend.Extensions;

/// <summary>
/// Registracije svih domenskih servisa, izdvojene iz Program.cs kako bi ih
/// <c>Backend.Tests.DependencyInjectionTests</c> mogao provjeriti bez pokretanja hosta.
/// </summary>
public static class DomainServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // ── Supabase pristupni kontekst ──
        // Odlucuje ide li PostgREST poziv s korisnikovim tokenom (RLS vrijedi)
        // ili sa service_role kljucem (RLS se zaobilazi). Scoped je jer token
        // pripada zahtjevu.
        services.AddHttpContextAccessor();
        services.AddScoped<ISupabaseRequestContext, SupabaseRequestContext>();

        // ── Supabase JWKS (javni kljucevi za ES256 tokene) ──
        // Singleton jer je cijela poanta kes: bez njega bi svaki zahtjev povlacio
        // kljuceve s Supabasea. Kratak timeout — visi ispod provjere tokena.
        services.AddHttpClient(SupabaseJwksProvider.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<ISupabaseJwksProvider, SupabaseJwksProvider>();

        // Dohvaca kljuceve pri startu umjesto na prvom zahtjevu. Na svjeze
        // pokrenutom Render kontejneru taj prvi dohvat traje ~3,6 s uz timeout od
        // 5 s — izgubljena utrka znaci da prva prijava dobiva odbijenicu.
        services.AddHostedService<SupabaseJwksWarmUp>();

        // Konkretni tip + sučelje pokazuju na istu registraciju: dio kontrolera
        // još uvijek injektira konkretne klase, a dio sučelja uvedena commitom c04aeeb.
        services.AddScoped<PdfService>();
        services.AddScoped<IPdfService>(sp => sp.GetRequiredService<PdfService>());

        services.AddScoped<ValuationService>();
        services.AddScoped<IValuationService>(sp => sp.GetRequiredService<ValuationService>());

        services.AddScoped<TextAnalysisService>();
        services.AddScoped<ITextAnalysisService>(sp => sp.GetRequiredService<TextAnalysisService>());

        services.AddScoped<ValuationAgentService>();
        services.AddScoped<IValuationAgentService>(sp => sp.GetRequiredService<ValuationAgentService>());

        services.AddScoped<ContractAnalysisService>();
        services.AddScoped<IContractAnalysisService>(sp => sp.GetRequiredService<ContractAnalysisService>());

        services.AddScoped<ChronologyService>();

        services.AddScoped<ValuationEngine>();
        services.AddScoped<IValuationEngine>(sp => sp.GetRequiredService<ValuationEngine>());

        services.AddScoped<ZkParsingService>();
        services.AddScoped<IZkParsingService>(sp => sp.GetRequiredService<ZkParsingService>());

        services.AddScoped<LegalIntelligenceService>();
        services.AddScoped<ILegalIntelligenceService>(sp => sp.GetRequiredService<LegalIntelligenceService>());

        services.AddSingleton<NormValidityService>();
        services.AddSingleton<INormValidityService>(sp => sp.GetRequiredService<NormValidityService>());

        services.AddScoped<InvoiceService>();

        services.AddScoped<TranslationService>();
        services.AddScoped<ITranslationService>(sp => sp.GetRequiredService<TranslationService>());

        services.AddHttpClient<EmailService>();

        // ── Troškovnik (odvjetnička tarifa + sudske pristojbe) ──
        services.AddScoped<ITroskovnikService, TroskovnikService>();
        services.AddScoped<ITroskovnikDocumentService, TroskovnikDocumentService>();

        // ── Resend transactional email (MailKit SMTP → smtp.resend.com:465) ──
        // Jedna instanca iza oba sučelja koja ResendEmailService implementira.
        services.AddSingleton<ResendEmailService>();
        services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<ResendEmailService>());
        services.AddSingleton<IResendEmailService>(sp => sp.GetRequiredService<ResendEmailService>());

        // REMOVED: the AuthService registration trio.
        //
        // It backed POST /api/auth/register and /api/auth/verify — a second
        // registration flow, with its own SMTP sender and its own use of the
        // Supabase Admin API, that this application stopped using when signup
        // moved to supabase.auth.signUp in the browser and profile creation moved
        // to the `handle_new_user` trigger (migration 015). Nothing called it:
        // not the web app, not the e2e suite. Registering it kept a second way to
        // create accounts alive behind an endpoint no feature needed.

        // ── Audit logging (GDPR-safe, fire-and-forget) ──
        services.AddHttpClient<AuditLogService>();
        services.AddScoped<IAuditLogService>(sp => sp.GetRequiredService<AuditLogService>());

        // ── Brisanje racuna i izvoz podataka (GDPR cl. 17 i cl. 20) ──
        services.AddHttpClient<AccountService>();
        services.AddScoped<IAccountService>(sp => sp.GetRequiredService<AccountService>());

        // ── Tenant resolution (user → law_firm, cached 5 min) ──
        services.AddHttpClient<TenantResolver>();
        services.AddScoped<ITenantResolver>(sp => sp.GetRequiredService<TenantResolver>());

        services.AddHttpClient<EmbeddingService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<EmbeddingService>());

        services.AddHttpClient<LawRAGService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddScoped<ILawRAGService>(sp => sp.GetRequiredService<LawRAGService>());

        services.AddHttpClient<MarketResearchService>(client =>
        {
            // V20d: Increased timeout — deep dive of 20+ listings can take 60-120s total
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddScoped<IMarketResearchService>(sp => sp.GetRequiredService<MarketResearchService>());

        services.AddHttpClient<SupabaseService>();
        services.AddScoped<ISupabaseService>(sp => sp.GetRequiredService<SupabaseService>());

        services.AddHttpClient<TwoFactorService>();
        services.AddScoped<ITwoFactorService>(sp => sp.GetRequiredService<TwoFactorService>());

        services.AddHttpClient<SettingsService>();
        services.AddScoped<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());

        services.AddHttpClient<CreditService>();
        services.AddScoped<ICreditService>(sp => sp.GetRequiredService<CreditService>());

        services.AddHttpClient<UsageService>();
        services.AddScoped<IUsageService>(sp => sp.GetRequiredService<UsageService>());

        services.AddHttpClient<ClientCaseService>();
        services.AddScoped<IClientCaseService>(sp => sp.GetRequiredService<ClientCaseService>());
        // Usko sučelje za troškovnik — ista instanca, samo manji pogled na nju (ISP).
        services.AddScoped<ITroskovnikSpremiste>(sp => sp.GetRequiredService<ClientCaseService>());

        // ── DataSeeder (admin-only, uses ClientCaseService) ──
        services.AddScoped<DataSeederService>();

        return services;
    }
}
