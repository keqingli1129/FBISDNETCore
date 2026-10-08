using System.Security.Claims;
using System.Security.Cryptography;
using System.Xml;
using CoreMVC.Web;
using FBISDNETCore.MVC.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using SamlResponse = FBISD.Response;

namespace FBISDNETCore.MVC.Areas.Identity.Pages.Account
{
    // SAML Assertion Consumer Service: Skyward posts the signed SAML response here. On success the user is signed into
    // Identity's external cookie and sent on to ExternalLogin's callback, exactly like Google/EntraID; on failure the
    // callback gets a remoteError. So every Skyward outcome is processed in ExternalLogin.cshtml.cs.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken] // cross-site POST from Skyward; protected by the one-time SkywardSamlState instead
    public class AcsModel : PageModel
    {
        private readonly SamlOptions _saml;
        private readonly SkywardSamlState _state;
        private readonly ILogger<AcsModel> _logger;

        public AcsModel(SamlOptions saml, SkywardSamlState state, ILogger<AcsModel> logger)
        {
            _saml = saml;
            _state = state;
            _logger = logger;
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public async Task<IActionResult> OnPostAsync()
        {
            // Missing when this browser didn't start the login (unsolicited or replayed response) or took over 10 minutes.
            var state = _state.Take(HttpContext);
            if (state == null)
            {
                return Fail(null, "The Skyward login session was not found or has expired. Please try again.");
            }

            var samlResponseBase64 = Request.Form["SAMLResponse"].ToString();
            if (string.IsNullOrEmpty(samlResponseBase64))
            {
                return Fail(state, "Skyward did not send a SAML response.");
            }

            SamlResponse samlResponse;
            try
            {
                samlResponse = new SamlResponse(_saml.IdpCertificate, samlResponseBase64);
            }
            catch (Exception ex) when (ex is FormatException or XmlException or CryptographicException)
            {
                _logger.LogWarning(ex, "Could not parse the Skyward SAML response.");
                return Fail(state, "The Skyward response could not be read.");
            }

            // Signature (against the Skyward certificate) and expiry.
            if (!samlResponse.IsValid())
            {
                return Fail(state, "The Skyward response signature is invalid or the response has expired.");
            }

            // Saml.cs doesn't check these: the response must answer the request this browser sent, for this app.
            var (inResponseTo, audience) = ReadRequestBinding(samlResponse.Xml);
            if (inResponseTo != state.RequestId)
            {
                return Fail(state, "The Skyward response does not belong to this login request.");
            }
            if (audience != null && audience != _saml.EntityId)
            {
                return Fail(state, $"The Skyward response was issued for '{audience}', not this application.");
            }

            // ProviderKey stored in AspNetUserLogins: must be stable for the user.
            var userKey = samlResponse.GetNameID() ?? samlResponse.GetUserName() ?? samlResponse.GetUpn() ?? samlResponse.GetEmail();
            if (string.IsNullOrEmpty(userKey))
            {
                return Fail(state, "The Skyward response did not identify the user.");
            }

            var email = samlResponse.GetEmail();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userKey),
                new Claim(ClaimTypes.Name, samlResponse.GetUserName() ?? email ?? userKey),
            };
            if (!string.IsNullOrEmpty(email))
            {
                // ExternalLogin pre-fills (and, if unchanged, auto-confirms) the account email from this claim.
                claims.Add(new Claim(ClaimTypes.Email, email));
            }
            if (samlResponse.GetFirstName() is { Length: > 0 } firstName)
            {
                claims.Add(new Claim(ClaimTypes.GivenName, firstName));
            }
            if (samlResponse.GetLastName() is { Length: > 0 } lastName)
            {
                claims.Add(new Claim(ClaimTypes.Surname, lastName));
            }

            // Same shape SignInManager.GetExternalLoginInfoAsync expects from Google/EntraID: principal in the external
            // cookie, provider name in the "LoginProvider" item.
            var properties = new AuthenticationProperties();
            properties.Items["LoginProvider"] = SkywardSamlHandler.SchemeName;
            await HttpContext.SignInAsync(IdentityConstants.ExternalScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, SkywardSamlHandler.SchemeName)), properties);

            _logger.LogInformation("Skyward SAML response accepted; continuing to the external login callback.");
            return LocalRedirect(state.RedirectUri);
        }

        // Sends the failure to the ExternalLogin callback as remoteError, like OnRemoteFailure does for Google/EntraID.
        private IActionResult Fail(SkywardSamlState.Entry? state, string error)
        {
            _logger.LogWarning("Skyward SAML login failed: {Error}", error);
            var callbackUrl = state?.RedirectUri ?? "/Identity/Account/ExternalLogin?handler=Callback";
            return LocalRedirect(QueryHelpers.AddQueryString(callbackUrl, "remoteError", error));
        }

        // InResponseTo (which request this answers) and Audience (which app it is for), read from the signed assertion.
        private static (string? InResponseTo, string? Audience) ReadRequestBinding(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            ns.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");

            var inResponseTo =
                doc.SelectSingleNode("/samlp:Response/saml:Assertion[1]/saml:Subject/saml:SubjectConfirmation/saml:SubjectConfirmationData/@InResponseTo", ns)?.Value
                ?? doc.SelectSingleNode("/samlp:Response/@InResponseTo", ns)?.Value;
            var audience = doc.SelectSingleNode("/samlp:Response/saml:Assertion[1]/saml:Conditions/saml:AudienceRestriction/saml:Audience", ns)?.InnerText.Trim();
            return (inResponseTo, audience);
        }
    }
}
