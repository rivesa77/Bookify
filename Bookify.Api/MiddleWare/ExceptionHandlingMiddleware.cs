namespace Bookify.Api.MiddleWare
{
    using Bookify.Application.Exceptions;
    using Microsoft.AspNetCore.Mvc;

    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate requestDelegate;
        private readonly ILogger<ExceptionHandlingMiddleware> logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate requestDelegate,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            this.requestDelegate = requestDelegate;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await requestDelegate(context);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception ocurred:{message}", exception.Message);

                ExceptionDetails excepctionDetails = GetExceptionDetails(exception);

                ProblemDetails problemDetails = new()
                {
                    Status = excepctionDetails.Status,
                    Title = excepctionDetails.Title,
                    Type = excepctionDetails.Type,
                    Detail = excepctionDetails.Detail,
                };

                if (excepctionDetails.Error is not null)
                {
                    problemDetails.Extensions["errors"] = excepctionDetails.Error;
                }

                context.Response.StatusCode = excepctionDetails.Status;

                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        }

        public static ExceptionDetails GetExceptionDetails(Exception exception)
        {
            return exception switch
            {
                ValidationException validationException => new ExceptionDetails(
                    StatusCodes.Status400BadRequest,
                    "ValidationFailure",
                    "Validation Error",
                    "One or more validation errors has ocurred.",
                    validationException.Errors),

                _ => new ExceptionDetails(
                    StatusCodes.Status500InternalServerError,
                    "ServerError",
                    "Server Error",
                    "An expected error has ocurred",
                    default)
            };
        }
    }
}