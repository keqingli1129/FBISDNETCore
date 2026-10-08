using CoreMVC.Web;
using FBISDNETCore.MVC.Authentication;
using FBISDNETCore.MVC.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FBISDNETCore.MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Maps the existing AspNet* Identity tables in the AdventureWorks database (no migrations).
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Database-first context scaffolded by EF Core Power Tools.
            builder.Services.AddDbContext<AWDBContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.UseHierarchyId().UseNetTopologySuite()));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // AddRoles must come before AddEntityFrameworkStores so the role store is registered too.
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            // External providers are registered only when credentials are configured (user secrets in development):
            // the handlers throw on every request if ClientId is missing. The Login/Register buttons light up when the scheme exists.
            var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
            {
                builder.Services.AddAuthentication()
                    .AddGoogle(options =>
                    {
                        options.ClientId = googleClientId;
                        options.ClientSecret = googleClientSecret;
                        options.Events.OnRemoteFailure = RedirectRemoteFailureToCallback;
                    });
            }

            // EntraID (Azure AD) as an external OpenID Connect provider, same opt-in rule as Google.
            // Scheme name "AzureAD" is what gets stored in AspNetUserLogins.LoginProvider.
            var azureTenantId = builder.Configuration["Authentication:AzureAd:TenantId"];
            var azureClientId = builder.Configuration["Authentication:AzureAd:ClientId"];
            var azureClientSecret = builder.Configuration["Authentication:AzureAd:ClientSecret"];
            var azureInstance = builder.Configuration["Authentication:AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
            if (!string.IsNullOrEmpty(azureTenantId) && !string.IsNullOrEmpty(azureClientId))
            {
                builder.Services.AddAuthentication()
                    .AddOpenIdConnect("AzureAD", "EntraID", options =>
                    {
                        options.SignInScheme = IdentityConstants.ExternalScheme;
                        options.Authority = $"{azureInstance.TrimEnd('/')}/{azureTenantId}/v2.0";
                        options.ClientId = azureClientId;
                        if (string.IsNullOrEmpty(azureClientSecret))
                        {
                            // Sign-in only (no downstream API calls): the id_token is posted straight back to /signin-oidc,
                            // so no client secret is needed. Requires "ID tokens" to be enabled under Authentication
                            // in the app registration.
                            options.ResponseType = OpenIdConnectResponseType.IdToken;
                        }
                        else
                        {
                            options.ClientSecret = azureClientSecret;
                            options.ResponseType = OpenIdConnectResponseType.Code;
                        }
                        // The ExternalLogin page pre-fills the email from this claim.
                        options.Scope.Add("email");
                        options.Events.OnRemoteFailure = RedirectRemoteFailureToCallback;
                    });
            }

            // Skyward via SAML 2.0 (Saml.cs = request/response utility, SamlOptions = the "Saml" config section).
            // Registered, like the others, only when configured: EntityId, IdpSsoUrl and the Skyward signing certificate
            // (IdpCertificatePath, relative to the content root, or the PEM inline in IdpCertificate).
            var saml = builder.Configuration.GetSection("Saml").Get<SamlOptions>() ?? new SamlOptions();
            if (!string.IsNullOrEmpty(saml.IdpCertificatePath))
            {
                var certificatePath = Path.Combine(builder.Environment.ContentRootPath, saml.IdpCertificatePath);
                saml.IdpCertificate = File.Exists(certificatePath) ? File.ReadAllText(certificatePath) : string.Empty;
            }
            builder.Services.AddSingleton(saml);
            builder.Services.AddSingleton<SkywardSamlState>();
            if (!string.IsNullOrEmpty(saml.EntityId) && !string.IsNullOrEmpty(saml.IdpSsoUrl) && !string.IsNullOrEmpty(saml.IdpCertificate))
            {
                builder.Services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, SkywardSamlHandler>(SkywardSamlHandler.SchemeName, SkywardSamlHandler.DisplayName, _ => { });
            }

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.MapRazorPages()
                .WithStaticAssets();

            app.Run();
        }

        // Without this, a failure reported by the provider (user cancelled, invalid client, redirect URI mismatch, ...)
        // throws and shows the error page. Instead, send it to the ExternalLogin callback as remoteError so success
        // and failure are both handled in Areas/Identity/Pages/Account/ExternalLogin.cshtml.cs.
        private static Task RedirectRemoteFailureToCallback(RemoteFailureContext context)
        {
            // RedirectUri is the callback URL (including returnUrl) stored in the protected state when the login started;
            // it's missing if the state itself couldn't be read.
            var callbackUrl = context.Properties?.RedirectUri ?? "/Identity/Account/ExternalLogin?handler=Callback";
            var error = context.Failure?.Message ?? "Unknown error.";
            context.Response.Redirect(QueryHelpers.AddQueryString(callbackUrl, "remoteError", error));
            context.HandleResponse();
            return Task.CompletedTask;
        }
    }
}
