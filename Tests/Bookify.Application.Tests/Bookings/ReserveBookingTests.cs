namespace Bookify.Application.Tests.Bookings
{
    using Bookify.Application.Bookings.ReserveBooking;
    using Bookify.Application.Exceptions;
    using Bookify.Application.Tests.Support;
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Bookings.Events;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class ReserveBookingTests
    {
        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        private readonly User user = ApplicationTestContext.CreateUser();

        private readonly Apartment apartment = ApplicationTestContext.CreateApartment();

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();
        }

        private static ReserveBookingCommand Command(Guid userId, Guid apartmentId) => new(
            apartmentId,
            userId,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 5));

        [TestMethod]
        public async Task Send_Should_ReserveWithCalculatedPriceAndSaveOnce()
        {
            // Arrange

            using CancellationTokenSource cancellation = new();

            ReserveBookingCommand command = Command(user.Id, apartment.Id);

            Booking? saved = null;

            context.Users.Setup(r => r.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(user);

            context.Apartments.Setup(r => r.GetByIdAsync(apartment.Id, cancellation.Token)).ReturnsAsync(apartment);

            context.Bookings.Setup(r => r.IsOverlappingAsync(
                apartment,
                It.Is<DateRange>(d => d.Start == command.StartDate && d.End == command.EndDate),
                cancellation.Token))
                .ReturnsAsync(false);

            context.Bookings.Setup(r => r.Add(It.IsAny<Booking>())).Callback<Booking>(b => saved = b);

            context.UnitOfWork.Setup(u => u.SaveChangesAsync(cancellation.Token)).ReturnsAsync(1);

            // Act

            Domain.Abstractions.Result<Guid> result = await context.Sender.Send(command, cancellation.Token);

            // Assert

            result.Value.Should().Be(saved!.Id).And.NotBeEmpty();

            saved.UserId.Should().Be(user.Id);

            saved.ApartmentId.Should().Be(apartment.Id);

            saved.Status.Should().Be(BookingStatus.Reserved);

            saved.CreatedOnUtc.Should().Be(ApplicationTestContext.UtcNow);

            saved.PriceForPeriod.Amount.Should().Be(400);

            saved.TotalPrice.Amount.Should().Be(420);

            apartment.LastBookedOnUTC.Should().Be(ApplicationTestContext.UtcNow);

            saved.GetDomainEvents().Should().ContainSingle().Which.Should().Be(new BookingReservedDomainEvent(saved.Id));

            context.Users.VerifyAll();

            context.Apartments.VerifyAll();

            context.Bookings.VerifyAll();

            context.UnitOfWork.Verify(u => u.SaveChangesAsync(cancellation.Token), Times.Once);

            context.Users.VerifyNoOtherCalls();

            context.Apartments.VerifyNoOtherCalls();

            context.Bookings.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("user")]
        [DataRow("apartment")]
        [DataRow("overlap")]
        public async Task Send_UnavailableResource_Should_ReturnErrorWithoutSaving(string failure)
        {
            // Arrange

            context.Users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(failure == "user" ? null : user);

            if (failure != "user")
            {
                context.Apartments.Setup(r => r.GetByIdAsync(apartment.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(failure == "apartment" ? null : apartment);
            }

            if (failure == "overlap")
            {
                context.Bookings.Setup(r => r.IsOverlappingAsync(
                    apartment,
                    It.IsAny<DateRange>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }

            // Act

            Domain.Abstractions.Result<Guid> result = await context.Sender.Send(Command(user.Id, apartment.Id));

            // Assert

            result.IsFailure.Should().BeTrue();

            result.Error.Should().Be(failure switch
            {
                "user" => UserErrors.NotFound,
                "apartment" => ApartmentErrors.NotFound,
                _ => BookingErrors.Overlap
            });

            context.UnitOfWork.VerifyNoOtherCalls();

            context.Bookings.Verify(r => r.Add(It.IsAny<Booking>()), Times.Never);

            context.Clock.VerifyGet(c => c.UtcNow, Times.Never);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task Send_SaveFailure_Should_OnlyTranslateConcurrency(bool concurrency)
        {
            // Arrange

            context.Users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            context.Apartments.Setup(r => r.GetByIdAsync(apartment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(apartment);

            context.Bookings.Setup(r => r.IsOverlappingAsync(
                apartment,
                It.IsAny<DateRange>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(false);

            context.Bookings.Setup(r => r.Add(It.IsAny<Booking>()));

            Exception error = concurrency ? new ConcurrencyException("Conflict", new Exception()) : new InvalidOperationException("Offline");

            context.UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);

            // Act

            Func<Task<Domain.Abstractions.Result<Guid>>> act = () => context.Sender.Send(Command(user.Id, apartment.Id));

            // Assert

            if (concurrency)
            {
                Domain.Abstractions.Result<Guid> result = await act();

                result.IsFailure.Should().BeTrue();

                result.Error.Should().Be(BookingErrors.Overlap);
            }
            else
            {
                (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(error);
            }

            context.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Send_InvalidDates_Should_NotAccessRepositories()
        {
            // Arrange

            ReserveBookingCommand command = Command(Guid.NewGuid(), Guid.NewGuid());

            // Act

            Func<Task> act = () => context.Sender.Send(command with { EndDate = command.StartDate });

            // Assert

            await act.Should().ThrowAsync<ValidationException>();

            context.Users.VerifyNoOtherCalls();

            context.Apartments.VerifyNoOtherCalls();

            context.Bookings.VerifyNoOtherCalls();

            context.UnitOfWork.VerifyNoOtherCalls();
        }
    }
}