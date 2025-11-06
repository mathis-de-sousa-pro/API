using API.Controllers.InterfacesManagers;
using API.DAO;
using API.Errors;
using API.Helpers;
using API.Managers;
using Api.Managers.InterfacesDao;
using Api.Managers.InterfacesHelpers;
using API.Managers.InterfacesHelpers;
using Api.Managers.InterfacesServices;
using API.Managers.InterfacesServices;
using API.Middleware;
using API.Services;
using API.Services.Audit;
using API.Services.Masking;
using Microsoft.AspNetCore.HttpOverrides;
// si tu as un dossier Helpers dans le namespace API.Services, ajuste l’using

// -----------------------------
// Program.cs (.NET 8, top-level)
// -----------------------------

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// CORS (au besoin pour tests; adapte la policy)
builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "Default",
            policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        );
    }
);

// HttpClient pour Spotify OAuth
builder.Services.AddHttpClient("spotify-oauth", client => { client.Timeout = TimeSpan.FromSeconds(15); });

// Configuration forte (singleton)
IConfiguration cfg = builder.Configuration;
IConfigService configService = new ConfigService(
    spotifyBaseUrl: cfg.GetValue<string>("Spotify:BaseUrl", "https://api.spotify.com/v1") ??
                    throw new ArgumentNullException("Spotify:BaseUrl configuration is missing."),
    spotifyPlaylistPageSize: cfg.GetValue("Spotify:PlaylistsPageSize", 20) switch
    {
        <= 0 => throw new ArgumentOutOfRangeException("Spotify:PlaylistsPageSize must be positive."),
        > 50 => throw new ArgumentOutOfRangeException("Spotify:PlaylistsPageSize cannot exceed 50."),
        var v => v
    },
    spotifyCacheTtlMinutes: cfg.GetValue("Spotify:CacheTtlMinutes", 60),
    spotifyClientId: cfg["Spotify:ClientId"] ?? throw new ArgumentNullException("Spotify:ClientId configuration is missing."),
    spotifyRedirectUri: cfg["Spotify:RedirectUri"] ??
                        throw new ArgumentNullException("Spotify:RedirectUri configuration is missing."),
    spotifyAuthorizeEndpoint: cfg.GetValue<string>("Spotify:AuthorizeEndpoint", "https://accounts.spotify.com/authorize") ??
                              throw new ArgumentNullException("Spotify:AuthorizeEndpoint configuration is missing."),
    spotifyTokenEndpoint: cfg.GetValue<string>("Spotify:TokenEndpoint", "https://accounts.spotify.com/api/token") ??
                          throw new ArgumentNullException("Spotify:TokenEndpoint configuration is missing."),
    deeplinkSchemeHost: cfg.GetValue<string>("Deeplink:SchemeHost", "swipez://oauth-callback/spotify") ??
                        throw new ArgumentNullException("Deeplink:SchemeHost configuration is missing."),
    pkceTtlMinutes: cfg.GetValue("Security:PkceTtlMinutes", 10),
    sessionTtlMinutes: cfg.GetValue("Security:SessionTtlMinutes", 60)
);

builder.Services.AddSingleton(configService);

// Horloge / Audit / Ids
builder.Services.AddSingleton<IClockService, ClockService>();
builder.Services.AddSingleton<IMaskingHelper, MaskingHelper>();
builder.Services.AddSingleton<AuditWriter>();
builder.Services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<AuditWriter>());
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<IIdGenerator, IdGenerator>();
builder.Services.AddHostedService<ObservabilityBackgroundWorker>();

// MySQL connection factory
string connectionString = cfg.GetConnectionString("Default");
builder.Services.AddSingleton<ISqlConnectionFactory>(sp =>
    new SqlConnectionFactory(connectionString)
);

// DAO
builder.Services.AddScoped<IPkceDao, PkceDao>();
builder.Services.AddScoped<ITokenDao, TokenDao>();
builder.Services.AddScoped<ISessionDao, SessionDao>();
builder.Services.AddScoped<IAccessTokenDao, AccessTokenDao>();
builder.Services.AddScoped<IPlaylistSelectionDao, PlaylistSelectionDao>();
builder.Services.AddScoped<IPlaylistCacheDao, PlaylistCacheDao>();
builder.Services.AddScoped<IUserProfileCacheDao, UserProfileCacheDao>();
builder.Services.AddScoped<IDenylistedRefreshDao, DenylistedRefreshDao>();
builder.Services.AddScoped<IRequestLogDao, RequestLogDao>();
builder.Services.AddScoped<IErrorEventDao, ErrorEventDao>();
builder.Services.AddScoped<IAuditEventDao, AuditEventDao>();


// Services métier
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ITokenDenyListService, TokenDenyListService>();
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<ITransactionRunner, MySqlTransactionRunner>();
builder.Services.AddScoped<ITokenService, TokenService>();


// Helpers
builder.Services.AddScoped<ICryptoHelper, CryptoHelper>();
builder.Services.AddScoped<IUrlBuilderHelper, UrlBuilderHelper>();
builder.Services.AddScoped<IDeeplinkHelper, DeeplinkHelper>();
builder.Services.AddScoped<ISpotifyOAuthHelper, SpotifyOAuthHelper>();

// HttpClient pour l'API Spotify (api.spotify.com/v1)
builder.Services.AddHttpClient<ISpotifyApiHelper, SpotifyApiHelper>(client =>
{
    string baseUrl = configService.GetSpotifyApiBaseUrl();        // ex: https://api.spotify.com/v1
    if (!baseUrl.EndsWith("/")) baseUrl += "/";                   // <<-- IMPORTANT
    client.BaseAddress = new Uri(baseUrl);                        // => https://api.spotify.com/v1/
    client.Timeout = TimeSpan.FromSeconds(15);
});


// Error mapping
builder.Services.AddSingleton<IErrorMapper, DefaultErrorMapper>();

// Managers
builder.Services.AddScoped<IAuthManager, AuthManager>();
builder.Services.AddScoped<IUserDataManager, UserDataManager>();
builder.Services.AddScoped<IPlaylistManager, PlaylistManager>();
builder.Services.AddScoped<IPreferencesManager, PreferencesManager>();

builder.Services.AddMemoryCache();

WebApplication app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


app.UseSwagger(); 
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Global error handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));


app.UseRouting();
app.UseCors("Default");
app.MapControllers();

app.Run();