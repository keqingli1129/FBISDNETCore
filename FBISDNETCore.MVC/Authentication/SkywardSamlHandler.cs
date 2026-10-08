using System.Text.Encodings.Web;
using CoreMVC.Web;
using FBISD;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FBISDNETCore.MVC.Authentication
{
    // Makes Skyward a regular external login scheme, so it shows up in SignInManager's external logins and the
    // ExternalLogin page can Challenge it like Google/EntraID. Challenge sends the browser to Skyward with a SAML
    // AuthnRequest (built by Saml.cs); Skyward posts the response to Areas/Identity/Pages/Account/Acs, which
    // validates it and hands off to the ExternalLogin callback.
    public class SkywardSamlHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Skyward";
        public const string DisplayName = "Skyward Login";

        private readonly SamlOptions _saml;
        private readonly SkywardSamlState _state;

        public SkywardSamlHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
            SamlOptions saml, SkywardSamlState state)
            : base(options, logger, encoder)
        {
            _saml = saml;
            _state = state;
        }

        // Nothing to authenticate per request; the ACS page signs the result into Identity's external cookie.
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var acsUrl = string.IsNullOrEmpty(_saml.AssertionConsumerServiceUrl)
                ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Identity/Account/Acs"
                : _saml.AssertionConsumerServiceUrl;

            var authRequest = new AuthRequest(_saml.EntityId, acsUrl);
            // properties.RedirectUri is the ExternalLogin callback URL (with returnUrl) set by the ExternalLogin page.
            _state.Save(Context, authRequest._id, properties.RedirectUri ?? "/Identity/Account/ExternalLogin?handler=Callback");
            Response.Redirect(authRequest.GetRedirectUrl(_saml.IdpSsoUrl));
            return Task.CompletedTask;
        }
    }
}
