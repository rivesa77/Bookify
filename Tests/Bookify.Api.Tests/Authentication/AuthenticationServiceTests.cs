namespace Bookify.Api.Tests.Authentication
{
    using System.Net;
    using System.Text.Json;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Authentication;
    using FluentAssertions;

    [TestClass]
    [TestCategory("Authentication")]
    public sealed class AuthenticationServiceTests
    {
        [TestMethod]
        public async Task RegisterAsync_Should_SendOnlyRegistrationFieldsAndReturnIdentity()
        {
            // Arrange
            using RegistrationHandler handler = new();
            using HttpClient client = new(handler)
            {
                BaseAddress = new Uri("https://identity.example/admin/realms/bookify/")
            };
            AuthenticationService service = new(client);

            User user = User.Create(
                new FirstName("Ana"),
                new LastName("Garcia"),
                new Email("ana@example.com"));

            // Act
            string identity = await service.RegisterAsync(user, "TestOnly1!", CancellationToken.None);

            // Assert
            identity.Should().Be("external-user-id");
            handler.RequestUri.Should().Be(new Uri("https://identity.example/admin/realms/bookify/users"));
            handler.Method.Should().Be(HttpMethod.Post);
            using JsonDocument document = JsonDocument.Parse(handler.Body!);
            JsonElement root = document.RootElement;
            root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                "firstName", "lastName", "email", "username", "enabled", "emailVerified", "credentials");
            root.GetProperty("firstName").GetString().Should().Be("Ana");
            root.GetProperty("lastName").GetString().Should().Be("Garcia");
            root.GetProperty("email").GetString().Should().Be("ana@example.com");
            root.GetProperty("username").GetString().Should().Be("ana@example.com");
            root.GetProperty("enabled").GetBoolean().Should().BeTrue();
            root.GetProperty("credentials").GetArrayLength().Should().Be(1);
            JsonElement credential = root.GetProperty("credentials")[0];
            credential.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("type", "value", "temporary");
            credential.GetProperty("type").GetString().Should().Be("password");
            credential.GetProperty("value").GetString().Should().Be("TestOnly1!");
            credential.GetProperty("temporary").GetBoolean().Should().BeFalse();
        }

        private sealed class RegistrationHandler : HttpMessageHandler
        {
            public Uri? RequestUri { get; private set; }
            public HttpMethod? Method { get; private set; }
            public string? Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUri = request.RequestUri;
                Method = request.Method;
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);
                HttpResponseMessage response = new(HttpStatusCode.Created);
                response.Headers.Location = new Uri("https://identity.example/admin/realms/bookify/users/external-user-id");
                return response;
            }
        }
    }
}