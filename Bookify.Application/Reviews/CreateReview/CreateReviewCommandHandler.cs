namespace Bookify.Application.Reviews.CreateReview
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;

    internal sealed class CreateReviewCommandHandler : ICommandHandler<CreateReviewCommand, Guid>
    {
        private readonly IBookingRepository bookingRepository;
        private readonly IReviewRepository reviewRepository;
        private readonly IUnitOfWork unitOfWork;
        private readonly IDateTimeProvider dateTimeProvider;

        public CreateReviewCommandHandler(
            IBookingRepository bookingRepository,
            IReviewRepository reviewRepository,
            IUnitOfWork unitOfWork,
            IDateTimeProvider dateTimeProvider)
        {
            this.bookingRepository = bookingRepository;
            this.reviewRepository = reviewRepository;
            this.unitOfWork = unitOfWork;
            this.dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<Guid>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
        {
            Booking? booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

            if (booking is null)
            {
                return Result.Failure<Guid>(BookingErrors.NotFound);
            }

            Result<Rating> rating = Rating.Create(request.Rating);

            if (rating.IsFailure)
            {
                return Result.Failure<Guid>(rating.Error);
            }

            Result<Review> result = Review.Create(
                booking,
                rating.Value,
                new Comment(request.Comment),
                dateTimeProvider.UtcNow);

            if (result.IsFailure)
            {
                return Result.Failure<Guid>(result.Error);
            }

            reviewRepository.Add(result.Value);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return result.Value.Id;
        }
    }
}