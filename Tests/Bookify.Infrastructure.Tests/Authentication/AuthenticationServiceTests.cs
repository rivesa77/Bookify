namespace Bookify.Infrastructure.Tests.Authentication
{
    using System.Net;
    using System.Text.Json;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication;
    using Bookify.Infrastructure.Authentication.Models;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class AuthenticationServiceTests
    {
        private readonly RecordingHttpHandler handler = new();

        private readonly User user = InfrastructureTestData.CreateUser();

        private HttpClient client = null!;

        private AuthenticationService service = null!;

        [TestInitialize]
        public void Initialize()
        {
            client = new HttpClient(handler) { BaseAddress = new Uri(InfrastructureTestData.Keycloak().AdminUrl) };

            service = new AuthenticationService(client);
        }

        [TestCleanup]
        public void Cleanup() => client.Dispose();

        [TestMethod]
        [DataRow("users/")]
        [DataRow("USERS/")]
        public async Task Register_Should_MapUserAndReturnLocationIdentifier(string userSegment)
        {
            // Arrange
            HttpResponseMessage response = new(HttpStatusCode.Created);

            response.Headers.Location = new Uri(client.BaseAddress!, userSegment + InfrastructureTestData.IdentityId);

            handler.Responses.Enqueue(response);

            // Act
            string identity = await Register();

            // Assert
            identity.Should().Be(InfrastructureTestData.IdentityId);

            RecordingHttpHandler.CapturedRequest request = handler.Requests.Should().ContainSingle().Subject;

            request.Uri.Should().Be(new Uri(client.BaseAddress!, "users"));

            request.Method.Should().Be(HttpMethod.Post);

            request.ContentType.Should().Be("application/json");

            using JsonDocument document = JsonDocument.Parse(request.Body!);

            JsonElement root = document.RootElement;

            root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                "firstName",
                "lastName",
                "email",
                "username",
                "enabled",
                "emailVerified",
                "credentials");

            root.GetProperty("firstName").GetString().Should().Be(user.FirstName.Value);

            root.GetProperty("lastName").GetString().Should().Be(user.LastName.Value);

            root.GetProperty("email").GetString().Should().Be(user.Email.Value);

            root.GetProperty("username").GetString().Should().Be(user.Email.Value);

            root.GetProperty("enabled").GetBoolean().Should().BeTrue();

            root.GetProperty("emailVerified").GetBoolean().Should().BeTrue();

            root.GetProperty("credentials").GetArrayLength().Should().Be(1);

            JsonElement credential = root.GetProperty("credentials")[0];

            credential.GetProperty("type").GetString().Should().Be("password");

            credential.GetProperty("value").GetString().Should().Be(InfrastructureTestData.Password);

            credential.GetProperty("temporary").GetBoolean().Should().BeFalse();
        }

        [TestMethod]
        public async Task Register_MissingLocation_Should_Throw()
        {
            // Arrange
            handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

            // Act
            Func<Task> act = () => Register();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [TestMethod]
        public async Task Register_NetworkFailure_Should_Propagate()
        {
            // Arrange
            HttpRequestException failure = new("Offline");

            handler.Failure = failure;

            // Act
            Func<Task> act = () => Register();

            // Assert
            (await act.Should().ThrowAsync<HttpRequestException>()).Which.Should().BeSameAs(failure);
        }

        [TestMethod]
        public void Models_Should_HaveSafeEmptyDefaultsAndDeserializeTokenName()
        {
            // Arrange
            const string json = "{\"access_token\":\"token\"}";

            // Act
            AuthorizationToken token = JsonSerializer.Deserialize<AuthorizationToken>(json)!;

            UserRepresentationModel representation = new();

            CredentialRepresentationModel credential = new();

            // Assert
            token.AccessToken.Should().Be("token");

            representation.Credentials.Should().BeEmpty();

            representation.Email.Should().BeEmpty();

            representation.FirstName.Should().BeEmpty();

            representation.LastName.Should().BeEmpty();

            representation.Username.Should().BeEmpty();

            representation.Enabled.Should().BeNull();

            representation.EmailVerified.Should().BeNull();

            credential.Type.Should().BeEmpty();

            credential.Value.Should().BeEmpty();

            credential.Temporary.Should().BeFalse();
        }

        private Task<string> Register() => service.RegisterAsync(
            user,
            InfrastructureTestData.Password,
            CancellationToken.None);
    }
}
