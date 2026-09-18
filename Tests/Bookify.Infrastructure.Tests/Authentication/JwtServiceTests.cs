namespace Bookify.Infrastructure.Tests.Authentication
{
    using System.Net;
    using System.Text.Json;
    using Bookify.Domain.Abstractions;
    using Bookify.Infrastructure.Authentication;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class JwtServiceTests
    {
        private readonly RecordingHttpHandler handler = new();

        private readonly KeycloakOptions options = InfrastructureTestData.Keycloak();

        private HttpClient client = null!;

        private JwtService service = null!;

        [TestInitialize]
        public void Initialize()
        {
            client = new HttpClient(handler) { BaseAddress = new Uri(options.TokenUrl) };

            service = new JwtService(client, Options.Create(options));
        }

        [TestCleanup]
        public void Cleanup() => client.Dispose();

        [TestMethod]
        public async Task GetAccessToken_Should_PostEncodedCredentialsAndReturnToken()
        {
            // Arrange
            handler.Responses.Enqueue(RecordingHttpHandler.Json("{\"access_token\":\"token-value\"}"));

            using CancellationTokenSource cancellation = new();

            // Act
            Result<string> result = await GetToken(cancellation.Token);

            // Assert
            result.IsSuccess.Should().BeTrue();

            result.Value.Should().Be("token-value");

            RecordingHttpHandler.CapturedRequest request = handler.Requests.Should().ContainSingle().Subject;

            request.Method.Should().Be(HttpMethod.Post);

            request.Uri.Should().Be(new Uri(options.TokenUrl));

            request.ContentType.Should().Be("application/x-www-form-urlencoded");

            request.CancellationToken.CanBeCanceled.Should().BeTrue();

            Dictionary<string, StringValues> form = QueryHelpers.ParseQuery(request.Body!);

            form.Should().HaveCount(6);

            form["client_id"].ToString().Should().Be(options.AuthClientId);

            form["client_secret"].ToString().Should().Be(options.AuthClientSecret);

            form["scope"].ToString().Should().Be("openid email");

            form["grant_type"].ToString().Should().Be("password");

            form["username"].ToString().Should().Be(InfrastructureTestData.Email);

            form["password"].ToString().Should().Be(InfrastructureTestData.Password);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.InternalServerError)]
        public async Task GetAccessToken_HttpFailure_Should_ReturnDomainError(HttpStatusCode status)
        {
            // Arrange
            handler.Responses.Enqueue(RecordingHttpHandler.Json("{}", status));

            // Act
            Result<string> result = await GetToken();

            // Assert
            AssertAuthenticationFailure(result);
        }

        [TestMethod]
        public async Task GetAccessToken_NetworkFailure_Should_ReturnDomainError()
        {
            // Arrange
            handler.Failure = new HttpRequestException("Offline");

            // Act
            Result<string> result = await GetToken();

            // Assert
            AssertAuthenticationFailure(result);
        }

        [TestMethod]
        public async Task GetAccessToken_NullResponse_Should_ReturnDomainError()
        {
            // Arrange
            handler.Responses.Enqueue(RecordingHttpHandler.Json("null"));

            // Act
            Result<string> result = await GetToken();

            // Assert
            AssertAuthenticationFailure(result);
        }

        [TestMethod]
        public async Task GetAccessToken_InvalidJson_Should_PropagateParsingError()
        {
            // Arrange
            handler.Responses.Enqueue(RecordingHttpHandler.Json("invalid-json"));

            // Act
            Func<Task> act = () => GetToken();

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }

        [TestMethod]
        public async Task GetAccessToken_Cancelled_Should_NotReturnAuthenticationFailure()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            cancellation.Cancel();

            // Act
            Func<Task> act = () => GetToken(cancellation.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            handler.Requests.Should().BeEmpty();
        }

        private Task<Result<string>> GetToken(CancellationToken token = default) => service.GetAccessTokenAsync(
            InfrastructureTestData.Email,
            InfrastructureTestData.Password,
            token);

        private static void AssertAuthenticationFailure(Result<string> result)
        {
            result.IsFailure.Should().BeTrue();

            result.Error.Code.Should().Be("KeyCloak.AuthenticationFailed");
        }
    }
}
