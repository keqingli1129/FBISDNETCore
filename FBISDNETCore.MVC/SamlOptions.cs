namespace CoreMVC.Web;

public class SamlOptions
{
    public string EntityId { get; set; } = string.Empty;
    public string AssertionConsumerServiceUrl { get; set; } = string.Empty;
    public string IdpSsoUrl { get; set; } = string.Empty;
    public string IdpCertificate { get; set; } = string.Empty;
    public string IdpCertificatePath { get; set; } = string.Empty;
}
