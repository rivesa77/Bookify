namespace Bookify.Application.Users.LogInUser
{
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;

    internal sealed class LogInUserCommandHandler : ICommandHandler<LogInUserCommand, AccessTokenResponse>
    {
        private readonly IJwtService jwtService;

        public LogInUserCommandHandler(IJwtService jwtService)
        {
            this.jwtService = jwtService;
        }

        public async Task<Result<AccessTokenResponse>> Handle(
            LogInUserCommand request,
            CancellationToken cancellationToken)
        {
            Result<string> accessTokenResponse = await jwtService.GetAccessTokenAsync(
                request.Email, request.Password, cancellationToken);

            if (accessTokenResponse.IsFailure)
            {
                return Result.Failure<AccessTokenResponse>(UserErrors.InvalidCredentials);
            }

            return new AccessTokenResponse(accessTokenResponse.Value);
        }
    }
}