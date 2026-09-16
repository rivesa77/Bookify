namespace Bookify.Infrastructure.Authentication
{
    using System.Net.Http.Json;
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication.Models;

    public sealed class AuthenticationService : IAuthenticationService
    {
        private const string PasswordCredentialType = "password";

        private readonly HttpClient httpClient;

        public AuthenticationService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<string> RegisterAsync(
            User user,
            string password,
            CancellationToken cancellationToken)
        {
            UserRepresentationModel userRepresentationModel = UserRepresentationModel.FromUser(user);

            userRepresentationModel.Credentials =
            [
                new()
                {
                    Value = password,
                    Temporary = false,
                    Type = PasswordCredentialType
                }
            ];

            HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
                "users",
                userRepresentationModel,
                cancellationToken);

            return ExtractIdentityIdFromLocationHeader(httpResponseMessage);
        }

        private static string ExtractIdentityIdFromLocationHeader(HttpResponseMessage httpResponseMessage)
        {
            const string userSegmentName = "users/";

            string locationHeader = httpResponseMessage.Headers.Location?.PathAndQuery!;

            if (string.IsNullOrWhiteSpace(locationHeader))
            {
                throw new InvalidOperationException("Location Header can´t be null.");
            }

            int userSegmentValueIndex = locationHeader.IndexOf(
                userSegmentName,
                StringComparison.InvariantCultureIgnoreCase);

            string userIdentityId = locationHeader.Substring(userSegmentValueIndex + userSegmentName.Length);

            return userIdentityId;
        }
    }
}