namespace Bookify.Application.Apartments.CreateApartment
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using FluentValidation;

    public sealed class CreateApartmentCommandValidator : AbstractValidator<CreateApartmentCommand>
    {
        public CreateApartmentCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(Name.ExactLength);
            RuleFor(command => command.Description).NotEmpty().MaximumLength(2000);
            RuleFor(command => command.Country).NotEmpty();
            RuleFor(command => command.State).NotEmpty();
            RuleFor(command => command.ZipCode).NotEmpty();
            RuleFor(command => command.City).NotEmpty();
            RuleFor(command => command.Street).NotEmpty();
            RuleFor(command => command.PriceAmount).GreaterThan(0);
            RuleFor(command => command.CleaningFeeAmount).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Currency)
                .Must(code => Currency.All.Any(currency => currency.Code == code))
                .WithMessage("The currency code must be EUR or USD.");
            RuleFor(command => command.Amenities).NotNull();
            RuleForEach(command => command.Amenities).IsInEnum();
            RuleFor(command => command.Amenities)
                .Must(amenities => amenities is null || amenities.Distinct().Count() == amenities.Count)
                .WithMessage("Amenities must not contain duplicates.");
        }
    }
}