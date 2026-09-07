namespace Bookify.Application.Abstractions.Behaviors
{
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Application.Exceptions;
    using FluentValidation;
    using MediatR;

    public class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IBaseCommand
    {
        private readonly IEnumerable<IValidator<TRequest>> validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            this.validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // If it does not contain any validator, it is using a command and we return to the PipelineBehavior.
            if (!validators.Any())
            {
                return await next(cancellationToken);
            }

            ValidationContext<TRequest> context = new(request);

            IEnumerable<ValidationError> validatorErrors = [.. validators
                .Select(validator => validator.Validate(context))
                .Where(validatorResult => validatorResult.Errors.Count>0)
                .SelectMany(validatorResult => validatorResult.Errors)
                .Select(validationFailure => new ValidationError(
                    validationFailure.PropertyName,
                    validationFailure.ErrorMessage))];

            if (validatorErrors.Any())
            {
                throw new Exceptions.ValidationException(validatorErrors);
            }

            return await next(cancellationToken);
        }
    }
}