namespace Bookify.Infrastructure.Authentication.Models
{
    using System.Text.Json.Serialization;

    public sealed class AuthorizationToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}