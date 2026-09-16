namespace Bookify.Infrastructure.Authentication
{
    using System.Net.Http.Json;
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Domain.Abstractions;
    using Bookify.Infrastructure.Authentication.Models;

    public sealed class JwtService : IJwtService
    {
        private static readonly Error AuthenticationFailed = new(
            "KeyCloak.AuthenticationFailed",
            "Failed to adquire access token do to authentication failure.");

        private readonly HttpClient httpClient;
        private readonly KeycloakOptions keycloakOptions;

        public JwtService(HttpClient httpClient, KeycloakOptions keycloakOptions)
        {
            this.httpClient = httpClient;
            this.keycloakOptions = keycloakOptions;
        }

        public async Task<Result<string>> GetAccessTokenAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<KeyValuePair<string, string>> authorizationRequestParameters =
                [
                    new("client_id", keycloakOptions.AuthClientId),
                    new("client_secret", keycloakOptions.AuthClientSecret),
                    new("scope", "openid email"),
                    new("grant_type", "password"),
                    new("username", email),
                    new("password", password)
                ];

                FormUrlEncodedContent authorizationRequestContent = new(authorizationRequestParameters);

                HttpResponseMessage result = await httpClient.PostAsync(
                    string.Empty,
                    authorizationRequestContent,
                    cancellationToken);

                result.EnsureSuccessStatusCode();

                AuthorizationToken? authorizationToken = await result.Content.ReadFromJsonAsync<AuthorizationToken>(cancellationToken);

                if (authorizationToken is null)
                {
                    return Result.Failure<string>(AuthenticationFailed);
                }

                return authorizationToken.AccessToken;
            }
            catch (HttpRequestException)
            {
                return Result.Failure<string>(AuthenticationFailed);
            }
        }
    }
}