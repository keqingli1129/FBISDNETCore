using System.Security.Claims;

namespace FBISDNETCore.MVC.Extensions
{
    public static class LoginMethods
    {
        public const string Account = "Account";
        public const string Google = "Google";
        public const string EntraID = "EntraID";
        public const string Skyward = "Skyward";
    }

    public static class ClaimsPrincipalExtensions
    {
        // How the current sign-in cookie was issued. SignInManager adds ClaimTypes.AuthenticationMethod = scheme name
        // ("Google", "AzureAD", "Skyward") only for external logins; password sign-ins get "amr" = "pwd" instead, so a missing
        // AuthenticationMethod means a local account. Both claims survive the periodic security-stamp refresh.
        public static string? GetLoginMethod(this ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimTypes.AuthenticationMethod) switch
            {
                null => LoginMethods.Account,
                "Google" => LoginMethods.Google,
                "AzureAD" => LoginMethods.EntraID,
                "Skyward" => LoginMethods.Skyward,
                var other => other,
            };
        }
    }
}
