namespace Bookify.Infrastructure.Authentication.Models
{
    public sealed class CredentialRepresentationModel
    {
        public bool Temporary { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}
