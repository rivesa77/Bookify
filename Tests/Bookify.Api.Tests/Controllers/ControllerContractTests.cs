namespace Bookify.Api.Tests.Controllers
{
    using Bookify.Api.Controllers.Apartments;
    using Bookify.Api.Controllers.Reviews;
    using Bookify.Api.Tests.Support;
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using FluentAssertions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;
    using Moq;

    [TestClass]
    [TestCategory("Controller")]
    public sealed class ControllerContractTests
    {
        private readonly Mock<ISender> sender = new(MockBehavior.Strict);

        private readonly CancellationTokenSource cancellation = new();

        [TestCleanup]
        public void Cleanup() => cancellation.Dispose();

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ApartmentCreate_Should_CopyAllFieldsAndMapResult(bool success)
        {
            // Arrange
            CreateApartmentRequest request = ApiTestData.ApartmentRequest();

            CreateApartmentCommand? captured = null;

            Result<Guid> result = success ? Result.Success(ApiTestData.Id) : Result.Failure<Guid>(ApiTestData.Failure);

            sender.Setup(s => s.Send(It.IsAny<CreateApartmentCommand>(), cancellation.Token))
                .Callback<IRequest<Result<Guid>>, CancellationToken>((command, _) => captured = (CreateApartmentCommand)command)
                .ReturnsAsync(result);

            ApartmentsController controller = new(sender.Object);

            // Act
            IActionResult response = await controller.CreateApartment(request, cancellation.Token);

            // Assert
            captured.Should().BeEquivalentTo(request);

            ObjectResult actual = response.Should().BeAssignableTo<ObjectResult>().Subject;

            actual.StatusCode.Should().Be(success ? 201 : 400);

            actual.Value.Should().Be(success ? (object)ApiTestData.Id : ApiTestData.Failure);

            sender.Verify(s => s.Send(It.IsAny<CreateApartmentCommand>(), cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(201)]
        [DataRow(400)]
        [DataRow(404)]
        public async Task ReviewCreate_Should_MapCommandTokenAndStatus(int status)
        {
            // Arrange
            CreateReviewRequest request = new(
                ApiTestData.Id,
                4,
                "Good stay");

            CreateReviewCommand command = new(
                request.BookingId,
                request.Rating,
                request.Comment);

            Error error = status == 404 ? BookingErrors.NotFound : ReviewErrors.NotEligible;

            Result<Guid> result = status == 201 ? Result.Success(ApiTestData.Id) : Result.Failure<Guid>(error);

            sender.Setup(s => s.Send(command, cancellation.Token)).ReturnsAsync(result);

            ReviewsController controller = new(sender.Object);

            // Act
            IActionResult response = await controller.CreateReview(request, cancellation.Token);

            // Assert
            ObjectResult actual = response.Should().BeAssignableTo<ObjectResult>().Subject;

            actual.StatusCode.Should().Be(status);

            actual.Value.Should().Be(status == 201 ? (object)ApiTestData.Id : error);

            sender.Verify(s => s.Send(command, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Search_Should_ReturnEmptyArrayForNoMatches()
        {
            // Arrange
            SearchApartmentsQuery query = new(ApiTestData.StartDate, ApiTestData.StartDate.AddDays(1));

            sender.Setup(s => s.Send(query, cancellation.Token)).ReturnsAsync(Result.Success<IReadOnlyList<ApartmentResponse>>([]));

            ApartmentsController controller = new(sender.Object);

            // Act
            IActionResult result = await controller.SearchApartments(
                query.StartDate,
                query.EndDate,
                cancellation.Token);

            // Assert
            result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IReadOnlyList<ApartmentResponse>>()
                .Which.Should().BeEmpty();

            sender.Verify(s => s.Send(query, cancellation.Token), Times.Once);

            sender.VerifyNoOtherCalls();
        }
    }
}
