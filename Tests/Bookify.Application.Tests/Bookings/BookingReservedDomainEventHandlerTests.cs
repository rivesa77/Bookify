namespace Bookify.Application.Tests.Bookings
{
    using Bookify.Application.Bookings.ReserveBooking;
    using Bookify.Application.Tests.Support;
    using Bookify.Domain.Bookings.Events;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class BookingReservedDomainEventHandlerTests
    {
        private const string EmailSubject = "Booking Reserved!";

        private const string EmailBody = "You have 10 minutes to confirm this booking.";

        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        private readonly Domain.Users.User user = ApplicationTestContext.CreateUser();

        private Domain.Bookings.Booking booking = null!;

        private BookingReservedDomainEventHandler handler = null!;

        [TestInitialize]
        public void Initialize()
        {
            booking = ApplicationTestContext.CreateBooking(user.Id);

            handler = new BookingReservedDomainEventHandler(
                context.Bookings.Object,
                context.Users.Object,
                context.Email.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        [TestMethod]
        [DataRow("booking")]
        [DataRow("user")]
        [DataRow("none")]
        public async Task Handle_Should_SendOnlyWhenBookingAndUserExist(string missing)
        {
            // Arrange

            using CancellationTokenSource cancellation = new CancellationTokenSource();

            context.Bookings.Setup(r => r.GetByIdAsync(booking.Id, cancellation.Token)).ReturnsAsync(missing == "booking" ? null : booking);

            if (missing != "booking")
            {
                context.Users.Setup(r => r.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(missing == "user" ? null : user);
            }

            if (missing == "none")
            {
                context.Email.Setup(e => e.SendAsync(
                    user.Email,
                    EmailSubject,
                    EmailBody))
                    .Returns(Task.CompletedTask);
            }

            // Act

            await handler.Handle(new BookingReservedDomainEvent(booking.Id), cancellation.Token);

            // Assert

            context.Bookings.Verify(r => r.GetByIdAsync(booking.Id, cancellation.Token), Times.Once);

            if (missing != "booking")
            {
                context.Users.Verify(r => r.GetByIdAsync(user.Id, cancellation.Token), Times.Once);
            }

            if (missing == "none")
            {
                context.Email.Verify(e => e.SendAsync(
                    user.Email,
                    EmailSubject,
                    EmailBody), Times.Once);
            }

            context.Bookings.VerifyNoOtherCalls();

            context.Users.VerifyNoOtherCalls();

            context.Email.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Handle_EmailFailure_Should_Propagate()
        {
            // Arrange

            InvalidOperationException failure = new InvalidOperationException("Email unavailable");

            context.Bookings.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

            context.Users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            context.Email.Setup(e => e.SendAsync(
                user.Email,
                It.IsAny<string>(),
                It.IsAny<string>())).ThrowsAsync(failure);

            // Act

            Func<Task> act = () => handler.Handle(new BookingReservedDomainEvent(booking.Id), default);

            // Assert

            (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        }
    }
}
