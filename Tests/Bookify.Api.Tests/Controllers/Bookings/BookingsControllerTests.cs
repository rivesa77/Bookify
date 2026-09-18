namespace Bookify.Api.Tests.Controllers.Bookings
{
    using Bookify.Api.Controllers.Bookings;
    using Bookify.Api.Tests.Support;
    using Bookify.Application.Bookings.GetBooking;
    using Bookify.Application.Bookings.ReserveBooking;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public sealed class BookingsControllerTests
    {
        private readonly Mock<ISender> sender = new(MockBehavior.Strict);

        private readonly CancellationTokenSource cancellation = new();

        private BookingsController controller = null!;

        [TestInitialize]
        public void Initialize() => controller = new BookingsController(sender.Object);

        [TestCleanup]
        public void Cleanup() => cancellation.Dispose();

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task GetBooking_Should_MapResultAndForwardIdentifierAndToken(bool found)
        {
            // Arrange
            BookingResponse expected = new() { Id = ApiTestData.Id };

            Result<BookingResponse> result = found ? Result.Success(expected) : Result.Failure<BookingResponse>(BookingErrors.NotFound);

            GetBookingQuery query = new(ApiTestData.Id);

            sender.Setup(s => s.Send(query, cancellation.Token)).ReturnsAsync(result);

            // Act
            IActionResult response = await controller.GetBooking(ApiTestData.Id, cancellation.Token);

            // Assert
            if (found)
            {
                response.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
            }
            else
            {
                response.Should().BeOfType<NotFoundResult>();
            }

            sender.Verify(s => s.Send(query, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Reserve_Should_MapAllFieldsAndReturnCreatedOrBadRequest(bool success)
        {
            // Arrange
            ReserveBookingRequest request = new(
                ApiTestData.Id,
                ApiTestData.UserId,
                ApiTestData.StartDate,
                ApiTestData.StartDate.AddDays(4));

            ReserveBookingCommand command = new(
                request.ApartmentId,
                request.UserId,
                request.StartDate,
                request.EndDate);

            Result<Guid> result = success ? Result.Success(ApiTestData.Id) : Result.Failure<Guid>(BookingErrors.Overlap);

            sender.Setup(s => s.Send(command, cancellation.Token)).ReturnsAsync(result);

            // Act
            IActionResult response = await controller.ReserveBooking(request, cancellation.Token);

            // Assert
            if (success)
            {
                CreatedAtActionResult created = response.Should().BeOfType<CreatedAtActionResult>().Subject;

                created.StatusCode.Should().Be(201);

                created.ActionName.Should().Be(nameof(BookingsController.GetBooking));

                created.RouteValues!["id"].Should().Be(ApiTestData.Id);

                created.Value.Should().Be(ApiTestData.Id);
            }
            else
            {
                response.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(BookingErrors.Overlap);
            }

            sender.Verify(s => s.Send(command, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }
    }
}
