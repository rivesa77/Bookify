namespace Bookify.Application.Users.CreateUser
{
    using FluentValidation;

    public sealed class CreateUserCommandHandlerValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserCommandHandlerValidator()
        {
            RuleFor(command => command.FirtsName).NotEmpty().MaximumLength(100);
            RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
            RuleFor(command => command.Email).NotEmpty().EmailAddress();
            RuleFor(command => command.Password).NotEmpty().MinimumLength(5).MaximumLength(10);
        }
    }
}