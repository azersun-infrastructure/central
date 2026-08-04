using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using OpsCentral.BackgroundServices;
using OpsCentral.Components;
using OpsCentral.Data;
using OpsCentral.Models.Entities;
using OpsCentral.Options;
using OpsCentral.Services;
using OpsCentral.Services.Auth;
using OpsCentral.Services.Dispatch;
using OpsCentral.Services.Dispatch.AzureAutomation;
using OpsCentral.Services.Dispatch.Jenkins;
using OpsCentral.Services.Dispatch.Mock;
using OpsCentral.Services.Graph;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---------------------------------------------------------
var useDatabase = builder.Configuration["UseDatabase"] ?? "Sqlite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(useDatabase, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection"));
    }
});

// --- Options -----------------------------------------------------------
builder.Services.Configure<JenkinsOptions>(builder.Configuration.GetSection(JenkinsOptions.SectionName));
builder.Services.Configure<AzureAutomationOptions>(builder.Configuration.GetSection(AzureAutomationOptions.SectionName));
builder.Services.Configure<AdActionRoutingOptions>(builder.Configuration.GetSection(AdActionRoutingOptions.SectionName));
builder.Services.Configure<ReconciliationOptions>(builder.Configuration.GetSection(ReconciliationOptions.SectionName));
builder.Services.Configure<GraphAppOnlyOptions>(builder.Configuration.GetSection(GraphAppOnlyOptions.SectionName));
builder.Services.Configure<LocalAdminFallbackOptions>(builder.Configuration.GetSection(LocalAdminFallbackOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<DispatchOptions>(builder.Configuration.GetSection(DispatchOptions.SectionName));

// --- Auth: Entra ID SSO + local fallback admin -------------------------
// DefaultScheme must be the cookie scheme (not OpenIdConnect) so that both an Entra-issued
// session AND a manually-issued local-admin cookie (see LocalLoginModel) are recognized on
// every request. OpenIdConnect is only the challenge scheme, used when there's no session yet.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(["https://graph.microsoft.com/User.Read"])
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddCascadingAuthenticationState();

// Plain Razor Pages/Controllers coexist with Blazor components: cookie sign-in (local admin
// login, Entra sign-in/out pages) can't happen from inside a live Blazor Server circuit.
builder.Services.AddRazorPages(options =>
{
    // Must stay reachable without an existing session, otherwise the FallbackPolicy locks
    // everyone out before they can ever reach the login form.
    options.Conventions.AllowAnonymousToPage("/Account/LocalLogin");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
})
    .AddMicrosoftIdentityUI();

builder.Services.AddControllers();

// --- Blazor Server -------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- HTTP clients --------------------------------------------------------
builder.Services.AddHttpClient();

// --- Microsoft Graph (app-only, separate app registration from SSO) ------
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<GraphAppOnlyOptions>>().Value;
    var credential = new ClientSecretCredential(o.TenantId, o.ClientId, o.ClientSecret);
    return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
});
builder.Services.AddScoped<IGraphAppOnlyService, GraphAppOnlyService>();

// --- AD action dispatch ---------------------------------------------------
var dispatchOptions = builder.Configuration.GetSection(DispatchOptions.SectionName).Get<DispatchOptions>()
                       ?? new DispatchOptions();

if (dispatchOptions.UseMock)
{
    builder.Services.AddKeyedScoped<IAdActionDispatcher>(DispatchTarget.Jenkins, (sp, _) =>
        new MockAdActionDispatcher(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<MockAdActionDispatcher>>(),
            sp.GetRequiredService<IOptions<JenkinsOptions>>(),
            sp.GetRequiredService<IOptions<AzureAutomationOptions>>(),
            DispatchTarget.Jenkins));

    builder.Services.AddKeyedScoped<IAdActionDispatcher>(DispatchTarget.AzureAutomation, (sp, _) =>
        new MockAdActionDispatcher(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<MockAdActionDispatcher>>(),
            sp.GetRequiredService<IOptions<JenkinsOptions>>(),
            sp.GetRequiredService<IOptions<AzureAutomationOptions>>(),
            DispatchTarget.AzureAutomation));
}
else
{
    builder.Services.AddKeyedScoped<IAdActionDispatcher, JenkinsDispatcher>(DispatchTarget.Jenkins);
    builder.Services.AddKeyedScoped<IAdActionDispatcher, AzureAutomationDispatcher>(DispatchTarget.AzureAutomation);
}

builder.Services.AddScoped<IAdActionDispatchRouter, AdActionDispatchRouter>();
builder.Services.AddScoped<IAdActionRequestService, AdActionRequestService>();

builder.Services.AddHostedService<JobReconciliationHostedService>();

var app = builder.Build();

// --- Startup: apply migrations, seed local fallback admin ----------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await LocalAdminSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

// --- Middleware pipeline ---------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/Account/Logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

app.MapControllers();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
