namespace Bookify.Infrastructure.Tests.Authentication
{
    using System.Net;
    using System.Net.Http.Headers;
    using Bookify.Infrastructure.Authentication;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class AdminAuthorizationDelegatingHandlerTests
    {
        private readonly RecordingHttpHandler transport = new();

        private readonly KeycloakOptions options = InfrastructureTestData.Keycloak();

        private HttpClient client = null!;

        [TestInitialize]
        public void Initialize()
        {
            AdminAuthorizationDelegatingHandler handler = new(Options.Create(options)) { InnerHandler = transport };

            client = new HttpClient(handler) { BaseAddress = new Uri(options.AdminUrl) };
        }

        [TestCleanup]
        public void Cleanup() => client.Dispose();

        [TestMethod]
        public async Task Send_Should_RequestAdminTokenThenAuthorizeOriginalRequest()
        {
            // Arrange
            transport.Responses.Enqueue(Token());

            transport.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));

            using HttpRequestMessage request = new(HttpMethod.Post, "users");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "old-token");

            // Act
            using HttpResponseMessage response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            transport.Requests.Should().HaveCount(2);

            RecordingHttpHandler.CapturedRequest tokenRequest = transport.Requests[0];

            tokenRequest.Uri.Should().Be(new Uri(options.TokenUrl));

            tokenRequest.Method.Should().Be(HttpMethod.Post);

            tokenRequest.Authorization.Should().BeNull();

            Dictionary<string, StringValues> form = QueryHelpers.ParseQuery(tokenRequest.Body!);

            form.Should().HaveCount(4);

            form["client_id"].ToString().Should().Be(options.AdminClientId);

            form["client_secret"].ToString().Should().Be(options.AdminClientSecret);

            form["grant_type"].ToString().Should().Be("client_credentials");

            form["scope"].ToString().Should().Be("openid email");

            transport.Requests[1].Uri.Should().Be(new Uri(new Uri(options.AdminUrl), "users"));

            transport.Requests[1].Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "admin-token"));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Send_HttpFailure_Should_ThrowAtFailingStage(bool tokenFails)
        {
            // Arrange
            transport.Responses.Enqueue(tokenFails ? new HttpResponseMessage(HttpStatusCode.BadRequest) : Token());

            if (!tokenFails)
            {
                transport.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
            }

            // Act
            Func<Task> act = () => client.GetAsync("users");

            // Assert
            await act.Should().ThrowAsync<HttpRequestException>();

            transport.Requests.Should().HaveCount(tokenFails ? 1 : 2);
        }

        [TestMethod]
        public async Task Send_NullToken_Should_NotSendAdminRequest()
        {
            // Arrange
            transport.Responses.Enqueue(RecordingHttpHandler.Json("null"));

            // Act
            Func<Task> act = () => client.GetAsync("users");

            // Assert
            await act.Should().ThrowAsync<ApplicationException>();

            transport.Requests.Should().ContainSingle();
        }

        [TestMethod]
        public async Task Send_Cancelled_Should_NotCallTransport()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();

            cancellation.Cancel();

            // Act
            Func<Task> act = () => client.GetAsync("users", cancellation.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

            transport.Requests.Should().BeEmpty();
        }

        private static HttpResponseMessage Token() => RecordingHttpHandler.Json("{\"access_token\":\"admin-token\"}");
    }
}
