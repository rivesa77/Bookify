namespace Bookify.Api.Tests.Http
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using Bookify.Api.Controllers.Bookings;
    using Bookify.Api.Controllers.Constants;
    using Bookify.Api.Controllers.Reviews;
    using Bookify.Api.Controllers.Users;
    using Bookify.Api.Tests.Support;
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Application.Bookings.GetBooking;
    using Bookify.Application.Bookings.ReserveBooking;
    using Bookify.Application.Exceptions;
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Application.Users.GetLoggedInUser;
    using Bookify.Application.Users.LogInUser;
    using Bookify.Domain.Abstractions;
    using FluentAssertions;
    using Moq;

    [TestClass]
    [TestCategory("Http")]
    public sealed class ApiHttpTests
    {
        private readonly ApiFactory factory = new();

        [TestCleanup]
        public void Cleanup() => factory.Dispose();

        [TestMethod]
        [DataRow("/api/apartments")]
        [DataRow("/api/users/LogInUser")]
        public async Task ProtectedEndpoint_Anonymous_Should_Return401WithoutDispatch(string path)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            // Act
            using HttpResponseMessage response = await client.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(false, false, HttpStatusCode.Forbidden)]
        [DataRow(true, false, HttpStatusCode.Forbidden)]
        [DataRow(false, true, HttpStatusCode.Forbidden)]
        [DataRow(true, true, HttpStatusCode.OK)]
        public async Task Profile_Should_RequireBothRoleAndPermission(
            bool role,
            bool permission,
            HttpStatusCode expected)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            if (role)
            {
                client.DefaultRequestHeaders.Add("X-Test-Role", RolesConstants.Registered);
            }

            if (permission)
            {
                client.DefaultRequestHeaders.Add("X-Test-Permission", PermissionsConstants.UsersRead);
            }

            if (expected == HttpStatusCode.OK)
            {
                factory.Sender.Setup(s => s.Send(It.IsAny<GetLoggedInUserQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result.Success(new UserResponse { Id = ApiTestData.UserId }));
            }

            // Act
            using HttpResponseMessage response = await client.GetAsync("/api/users/LogInUser");

            // Assert
            response.StatusCode.Should().Be(expected);

            if (expected == HttpStatusCode.OK)
            {
                (await response.Content.ReadFromJsonAsync<UserResponse>())!.Id.Should().Be(ApiTestData.UserId);

                factory.Sender.Verify(s => s.Send(It.IsAny<GetLoggedInUserQuery>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Register_Anonymous_Should_BindJsonAndReturnId()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            CreateUserRequest request = ApiTestData.UserRequest();

            CreateUserCommand command = new(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password);

            factory.Sender.Setup(s => s.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(ApiTestData.Id));

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/users/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            (await response.Content.ReadFromJsonAsync<Guid>()).Should().Be(ApiTestData.Id);

            factory.Sender.Verify(s => s.Send(command, It.IsAny<CancellationToken>()), Times.Once);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Login_Anonymous_Should_ReturnCurrentTokenJsonContract()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            CreateUserRequest user = ApiTestData.UserRequest();

            LogInUserCommand command = new(user.Email, user.Password);

            factory.Sender.Setup(s => s.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(new AccessTokenResponse("token")));

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/users/login", new LoginUserRequest(user.Email, user.Password));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            document.RootElement.GetProperty("accessToke").GetString().Should().Be("token");
        }

        [TestMethod]
        public async Task ApartmentSearch_Should_BindQueryDates()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            SearchApartmentsQuery query = new(ApiTestData.StartDate, ApiTestData.StartDate.AddDays(4));

            factory.Sender.Setup(s => s.Send(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<IReadOnlyList<ApartmentResponse>>([]));

            // Act
            using HttpResponseMessage response = await client.GetAsync("/api/apartments?startDate=2026-10-01&endDate=2026-10-05");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            (await response.Content.ReadFromJsonAsync<ApartmentResponse[]>()).Should().BeEmpty();

            factory.Sender.Verify(s => s.Send(query, It.IsAny<CancellationToken>()), Times.Once);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task ApartmentCreate_Should_Return201OverHttp()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            factory.Sender.Setup(s => s.Send(It.IsAny<CreateApartmentCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(ApiTestData.Id));

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/apartments", ApiTestData.ApartmentRequest());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            (await response.Content.ReadFromJsonAsync<Guid>()).Should().Be(ApiTestData.Id);
        }

        [TestMethod]
        public async Task BookingCreate_Should_GenerateResolvableLocation()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            ReserveBookingRequest request = new(
                ApiTestData.Id,
                ApiTestData.UserId,
                ApiTestData.StartDate,
                ApiTestData.StartDate.AddDays(4));

            factory.Sender.Setup(s => s.Send(It.IsAny<ReserveBookingCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(ApiTestData.Id));

            factory.Sender.Setup(s => s.Send(new GetBookingQuery(ApiTestData.Id), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(new BookingResponse { Id = ApiTestData.Id }));

            // Act
            using HttpResponseMessage created = await client.PostAsJsonAsync("/api/bookings", request);

            // Assert
            created.StatusCode.Should().Be(HttpStatusCode.Created);

            created.Headers.Location.Should().NotBeNull();

            created.Headers.Location!.AbsolutePath.Should().Be("/api/bookings/" + ApiTestData.Id);

            using HttpResponseMessage found = await client.GetAsync(created.Headers.Location);

            found.StatusCode.Should().Be(HttpStatusCode.OK);

            (await found.Content.ReadFromJsonAsync<BookingResponse>())!.Id.Should().Be(ApiTestData.Id);
        }

        [TestMethod]
        public async Task ReviewCreate_Should_BindRequestAndReturn201()
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            CreateReviewRequest request = new(
                ApiTestData.Id,
                4,
                "Good stay");

            CreateReviewCommand command = new(
                request.BookingId,
                request.Rating,
                request.Comment);

            factory.Sender.Setup(s => s.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(ApiTestData.Id));

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/reviews", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            (await response.Content.ReadFromJsonAsync<Guid>()).Should().Be(ApiTestData.Id);

            factory.Sender.Verify(s => s.Send(command, It.IsAny<CancellationToken>()), Times.Once);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("/api/users/register", "{", HttpStatusCode.BadRequest)]
        [DataRow("/api/users/register", "{}", HttpStatusCode.BadRequest)]
        [DataRow("/api/users/login", "{\"email\":null,\"password\":null}", HttpStatusCode.BadRequest)]
        [DataRow("/api/reviews", "{\"bookingId\":\"invalid\",\"rating\":4,\"comment\":\"ok\"}", HttpStatusCode.BadRequest)]
        public async Task InvalidBody_Should_Return400BeforeDispatch(
            string path,
            string json,
            HttpStatusCode expected)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            using StringContent content = new(
                json,
                Encoding.UTF8,
                "application/json");

            // Act
            using HttpResponseMessage response = await client.PostAsync(path, content);

            // Assert
            response.StatusCode.Should().Be(expected);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("/api/bookings/invalid-guid")]
        [DataRow("/api/apartments?startDate=invalid&endDate=2026-10-05")]
        public async Task InvalidRouteOrQuery_Should_Return400BeforeDispatch(string path)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient(authenticated: true);

            // Act
            using HttpResponseMessage response = await client.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(true, HttpStatusCode.BadRequest)]
        [DataRow(false, HttpStatusCode.InternalServerError)]
        public async Task DispatchException_Should_PassThroughRealMiddleware(bool validation, HttpStatusCode status)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            Exception error = validation
                ? new ValidationException([new ValidationError("Email", "Invalid email")])
                : new InvalidOperationException("Private database detail");

            factory.Sender.Setup(s => s.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);

            // Act
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/users/register", ApiTestData.UserRequest());

            // Assert
            response.StatusCode.Should().Be(status);

            string body = await response.Content.ReadAsStringAsync();

            body.Should().NotContain("Private database detail");

            using JsonDocument document = JsonDocument.Parse(body);

            document.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);

            document.RootElement.TryGetProperty("errors", out JsonElement errors).Should().Be(validation);
        }

        [TestMethod]
        [DataRow("/openapi/v1.json", "application/json")]
        [DataRow("/swagger/index.html", "text/html")]
        public async Task Development_Should_ExposeDocumentationAndInvokeStartupExtensions(string path, string mediaType)
        {
            // Arrange
            using ApiFactory development = new(development: true);

            using HttpClient client = development.CreateApiClient();

            // Act
            using HttpResponseMessage response = await client.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            response.Content.Headers.ContentType!.MediaType.Should().Be(mediaType);

            development.Migrator.Verify(m => m.Migrate(null), Times.Once);

            development.Seed!.Transaction.Verify(t => t.Commit(), Times.Once);

            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                JsonElement paths = document.RootElement.GetProperty("paths");

                paths.TryGetProperty("/api/apartments", out JsonElement apartments).Should().BeTrue();

                paths.TryGetProperty("/api/bookings/{id}", out JsonElement booking).Should().BeTrue();

                paths.TryGetProperty("/api/reviews", out JsonElement reviews).Should().BeTrue();

                paths.TryGetProperty("/api/users/login", out JsonElement login).Should().BeTrue();
            }
        }

        [TestMethod]
        [DataRow("/openapi/v1.json")]
        [DataRow("/swagger/index.html")]
        public async Task NonDevelopment_Should_NotExposeDevelopmentDocumentation(string path)
        {
            // Arrange
            using HttpClient client = factory.CreateApiClient();

            // Act
            using HttpResponseMessage response = await client.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
