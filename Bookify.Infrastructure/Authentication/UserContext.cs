namespace Bookify.Infrastructure.Authentication
{
    using System;
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Infrastructure.Authentication.Extensions;
    using Microsoft.AspNetCore.Http;

    internal sealed class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public string IdentityId =>
            httpContextAccessor
                .HttpContext?
                .User
                .GetIdentityId() ??
            throw new ApplicationException("User context is unavailable");
    }
}