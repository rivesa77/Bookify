namespace Bookify.Application.Abstractions.Behaviors
{
    using Bookify.Application.Abstractions.Messaging;
    using MediatR;
    using Microsoft.Extensions.Logging;

    public class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IBaseCommand
    {
        private readonly ILogger<TRequest> logger;

        public LoggingBehavior(ILogger<TRequest> logger)
        {
            this.logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            string name = request.GetType().Name;

            try
            {
                logger.LogInformation("Executing command {Command}", name);

                TResponse result = await next(cancellationToken);

                logger.LogInformation("Command {Command} processed successfully", name);

                return result;
            }
            catch (Exception)
            {
                logger.LogInformation("Command {Command} processed failed", name);

                throw;
            }
        }
    }
}