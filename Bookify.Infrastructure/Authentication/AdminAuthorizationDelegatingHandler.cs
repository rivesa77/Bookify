namespace Bookify.Infrastructure.Authentication
{
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using Bookify.Infrastructure.Authentication.Models;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.Extensions.Options;

    public sealed class AdminAuthorizationDelegatingHandler : DelegatingHandler
    {
        private readonly KeycloakOptions keycloakOptions;

        public AdminAuthorizationDelegatingHandler(IOptions<KeycloakOptions> keycloakOptions)
        {
            this.keycloakOptions = keycloakOptions.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationToken authorizationToken = await GetAuthorizationToken(cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme,
                authorizationToken.AccessToken);

            HttpResponseMessage httpResponseMessage = await base.SendAsync(request, cancellationToken);

            httpResponseMessage.EnsureSuccessStatusCode();

            return httpResponseMessage;
        }

        private async Task<AuthorizationToken> GetAuthorizationToken(CancellationToken cancellationToken)
        {
            IEnumerable<KeyValuePair<string, string>> authorizationRequestParameters =
            [
                new("client_id", keycloakOptions.AdminClientId),
                new("client_secret", keycloakOptions.AdminClientSecret),
                new("scope", "openid email"),
                new("grant_type", "client_credentials")
            ];

            FormUrlEncodedContent authorizationRequestContent = new(authorizationRequestParameters);

            HttpRequestMessage authorizationRequest = new(
                HttpMethod.Post,
                new Uri(keycloakOptions.TokenUrl))
            {
                Content = authorizationRequestContent
            };

            HttpResponseMessage authorizationResponse = await base.SendAsync(authorizationRequest, cancellationToken);

            authorizationResponse.EnsureSuccessStatusCode();

            return await authorizationResponse.Content.ReadFromJsonAsync<AuthorizationToken>(cancellationToken) ??
                   throw new ApplicationException();
        }
    }
}