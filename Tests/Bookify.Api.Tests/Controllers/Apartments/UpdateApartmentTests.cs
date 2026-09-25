namespace Bookify.Api.Tests.Controllers.Apartments
{
    using System.Net;
    using System.Net.Http.Json;
    using Bookify.Api.Controllers.Apartments;
    using Bookify.Api.Tests.Support;
    using Bookify.Application.Apartments.UpdateApartment;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class UpdateApartmentTests
    {
        private readonly ApiFactory factory = new();

        private static readonly string Path = $"/api/apartments/{ApiTestData.Id}";

        [TestCleanup]
        public void Cleanup() => factory.Dispose();

        [TestMethod]
        [TestCategory("Http")]
        [DataRow("success", HttpStatusCode.NoContent)]
        [DataRow("missing", HttpStatusCode.NotFound)]
        [DataRow("conflict", HttpStatusCode.Conflict)]
        [DataRow("invalid", HttpStatusCode.BadRequest)]
        public async Task Put_Should_MapCommandAndReturnExpectedStatus(string outcome, HttpStatusCode expected)
        {
            // Arrange

            using HttpClient client = factory.CreateApiClient(authenticated: true);

            UpdateApartmentRequest request = Request();

            UpdateApartmentCommand? received = null;

            Result result = outcome switch
            {
                "missing" => Result.Failure(ApartmentErrors.NotFound),
                "conflict" => Result.Failure(ApartmentErrors.Conflict),
                "invalid" => Result.Failure(NameErrors.Empty),
                _ => Result.Success()
            };

            factory.Sender.Setup(sender => sender.Send(It.IsAny<UpdateApartmentCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<Result>, CancellationToken>((command, _) => received = (UpdateApartmentCommand)command)
                .ReturnsAsync(result);

            // Act

            using HttpResponseMessage response = await client.PutAsJsonAsync(Path, request);

            // Assert

            response.StatusCode.Should().Be(expected);

            received.Should().NotBeNull();

            received!.ApartmentId.Should().Be(ApiTestData.Id);

            received.Should().BeEquivalentTo(request);

            if (result.IsFailure)
            {
                (await response.Content.ReadFromJsonAsync<Error>()).Should().Be(result.Error);
            }
            else
            {
                (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
            }

            factory.Sender.Verify(sender => sender.Send(It.IsAny<UpdateApartmentCommand>(), It.IsAny<CancellationToken>()), Times.Once);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [TestCategory("Http")]
        public async Task Put_Anonymous_Should_Return401WithoutDispatch()
        {
            // Arrange

            using HttpClient client = factory.CreateApiClient();

            // Act

            using HttpResponseMessage response = await client.PutAsJsonAsync(Path, Request());

            // Assert

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [TestCategory("Http")]
        public async Task Put_InvalidRouteId_Should_Return404WithoutDispatch()
        {
            // Arrange

            using HttpClient client = factory.CreateApiClient(authenticated: true);

            // Act

            using HttpResponseMessage response = await client.PutAsJsonAsync("/api/apartments/invalid", Request());

            // Assert

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            factory.Sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [TestCategory("Controller")]
        public async Task Controller_Should_ForwardCancellationToken()
        {
            // Arrange

            using CancellationTokenSource cancellation = new();

            ApartmentsController controller = new(factory.Sender.Object);

            factory.Sender.Setup(sender => sender.Send(It.IsAny<UpdateApartmentCommand>(), cancellation.Token))
                .ReturnsAsync(Result.Success());

            // Act

            IActionResult result = await controller.UpdateApartment(
                ApiTestData.Id,
                Request(),
                cancellation.Token);

            // Assert

            result.Should().BeOfType<NoContentResult>();

            factory.Sender.Verify(sender => sender.Send(It.IsAny<UpdateApartmentCommand>(), cancellation.Token), Times.Once);

            factory.Sender.VerifyNoOtherCalls();
        }

        private static UpdateApartmentRequest Request() => new(
            "Updated apartment",
            "Updated description",
            "France",
            "Ile-de-France",
            "75001",
            "Paris",
            "Street 2",
            150m,
            30m,
            "USD",
            [Amenity.Wifi]);
    }
}
