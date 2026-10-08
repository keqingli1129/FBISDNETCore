using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace FBISDNETCore.MVC.Authentication
{
    // Remembers, between the redirect to Skyward and Skyward's POST back to /Identity/Account/Acs, which SAML request
    // this browser sent and where to continue afterwards. Kept in a short-lived, encrypted, one-time cookie so the ACS
    // only accepts a response to a login this browser actually started (Saml.cs checks signature and expiry, not that).
    public sealed class SkywardSamlState
    {
        private const string CookieName = ".FBISD.SkywardSaml";
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private readonly ITimeLimitedDataProtector _protector;

        public SkywardSamlState(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("FBISDNETCore.MVC.SkywardSaml").ToTimeLimitedDataProtector();
        }

        public sealed record Entry(string RequestId, string RedirectUri);

        public void Save(HttpContext context, string requestId, string redirectUri)
        {
            var value = _protector.Protect($"{requestId}\n{redirectUri}", Lifetime);
            // SameSite=None (which requires Secure): Skyward posts the response back cross-site.
            context.Response.Cookies.Append(CookieName, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                IsEssential = true,
                MaxAge = Lifetime,
            });
        }

        // Returns the saved entry and deletes the cookie, so a response can only be used once.
        public Entry? Take(HttpContext context)
        {
            if (!context.Request.Cookies.TryGetValue(CookieName, out var value))
            {
                return null;
            }

            context.Response.Cookies.Delete(CookieName, new CookieOptions { Secure = true, SameSite = SameSiteMode.None });
            try
            {
                var parts = _protector.Unprotect(value).Split('\n', 2);
                return parts.Length == 2 ? new Entry(parts[0], parts[1]) : null;
            }
            catch (CryptographicException)
            {
                // Tampered or older than Lifetime.
                return null;
            }
        }
    }
}
