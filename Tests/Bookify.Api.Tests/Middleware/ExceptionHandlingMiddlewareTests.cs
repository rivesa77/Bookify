namespace Bookify.Api.Tests.Middleware
{
    using System.Text.Json;
    using Bookify.Api.Extensions;
    using Bookify.Api.MiddleWare;
    using Bookify.Application.Exceptions;
    using FluentAssertions;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;

    [TestClass]
    [TestCategory("Middleware")]
    public sealed class ExceptionHandlingMiddlewareTests
    {
        private readonly Mock<ILogger<ExceptionHandlingMiddleware>> logger = new();

        private readonly ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();

        private readonly MemoryStream body = new();

        private static readonly ValidationError[] Errors = [new("Email", "Invalid email"), new("Password", "Invalid password")];

        private DefaultHttpContext context = null!;

        [TestInitialize]
        public void Initialize()
        {
            context = new DefaultHttpContext { RequestServices = services };

            context.Response.Body = body;
        }

        [TestCleanup]
        public void Cleanup()
        {
            body.Dispose();

            services.Dispose();
        }

        [TestMethod]
        public async Task Invoke_NoException_Should_PreserveResponseAndCallNextOnce()
        {
            // Arrange
            int calls = 0;

            ExceptionHandlingMiddleware middleware = new(
                httpContext =>
                {
                    calls++;

                    httpContext.Response.StatusCode = StatusCodes.Status202Accepted;

                    return httpContext.Response.WriteAsync("accepted");
                },
                logger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            calls.Should().Be(1);

            context.Response.StatusCode.Should().Be(StatusCodes.Status202Accepted);

            System.Text.Encoding.UTF8.GetString(body.ToArray()).Should().Be("accepted");

            logger.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(true, 400, "ValidationFailure")]
        [DataRow(false, 500, "ServerError")]
        public async Task Invoke_Exception_Should_WriteProblemDetailsAndLog(
            bool validation,
            int status,
            string type)
        {
            // Arrange
            Exception error = CreateException(validation);

            ExceptionHandlingMiddleware middleware = new(_ => throw error, logger.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.Should().Be(status);

            context.Response.ContentType.Should().Contain("json");

            using JsonDocument document = JsonDocument.Parse(body.ToArray());

            JsonElement root = document.RootElement;

            root.GetProperty("status").GetInt32().Should().Be(status);

            root.GetProperty("type").GetString().Should().Be(type);

            root.GetProperty("title").GetString().Should().Be(validation ? "Validation Error" : "Server Error");

            root.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();

            root.TryGetProperty("errors", out JsonElement errors).Should().Be(validation);

            root.GetRawText().Should().NotContain("Sensitive connection string");

            if (validation)
            {
                errors.GetArrayLength().Should().Be(2);

                errors[0].GetProperty("propertyName").GetString().Should().Be("Email");

                errors[0].GetProperty("errorMessage").GetString().Should().Be("Invalid email");
            }

            logger.Verify(log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                error,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

            logger.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void GetExceptionDetails_Should_MapOnlyValidationTo400(bool validation)
        {
            // Arrange
            Exception error = CreateException(validation);

            // Act
            ExceptionDetails details = ExceptionHandlingMiddleware.GetExceptionDetails(error);

            // Assert
            details.Status.Should().Be(validation ? 400 : 500);

            details.Type.Should().Be(validation ? "ValidationFailure" : "ServerError");

            if (validation)
            {
                details.Error.Should().BeEquivalentTo(Errors);
            }
            else
            {
                details.Error.Should().BeNull();
            }
        }

        [TestMethod]
        public async Task UseCustomExceptionHandler_Should_WrapFollowingPipeline()
        {
            // Arrange
            ApplicationBuilder app = new(services);

            app.UseCustomExceptionHandler();

            app.Run(_ => throw new InvalidOperationException("Sensitive connection string"));

            RequestDelegate pipeline = app.Build();

            // Act
            await pipeline(context);

            // Assert
            context.Response.StatusCode.Should().Be(500);

            using JsonDocument document = JsonDocument.Parse(body.ToArray());

            document.RootElement.GetProperty("type").GetString().Should().Be("ServerError");
        }

        private static Exception CreateException(bool validation) => validation
            ? new ValidationException(Errors)
            : new InvalidOperationException("Sensitive connection string");
    }
}
