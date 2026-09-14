namespace Bookify.Application.Reviews.CreateReview
{
    using FluentValidation;

    public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
    {
        public CreateReviewCommandValidator()
        {
            RuleFor(command => command.BookingId).NotEmpty();
            RuleFor(command => command.Rating).InclusiveBetween(1, 5);
            RuleFor(command => command.Comment).NotEmpty().MaximumLength(200);
        }
    }
}
