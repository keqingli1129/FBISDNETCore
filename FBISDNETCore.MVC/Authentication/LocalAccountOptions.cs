namespace FBISDNETCore.MVC.Authentication
{
    // The "Authentication:LocalAccounts" config section. Enabled = false switches off email/password accounts
    // (the Login form, password sign-in, registration, password reset) so users sign in only through the
    // external providers; accounts created through those providers keep working.
    public class LocalAccountOptions
    {
        public const string SectionName = "Authentication:LocalAccounts";

        public bool Enabled { get; set; } = true;
    }
}
