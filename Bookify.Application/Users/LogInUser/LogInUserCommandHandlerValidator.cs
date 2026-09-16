namespace Bookify.Application.Users.LogInUser
{
    using FluentValidation;

    public sealed class LogInUserCommandHandlerValidator : AbstractValidator<LogInUserCommand>
    {
        public LogInUserCommandHandlerValidator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress();
            RuleFor(command => command.Password).NotEmpty().MinimumLength(5).MaximumLength(10);
        }
    }
}