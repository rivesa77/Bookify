namespace Bookify.Infrastructure.Authorization
{
    using System;
    using System.Collections.Generic;
    using Bookify.Domain.Users;

    internal sealed class UserRolesResponse
    {
        public Guid Id { get; init; }

        public List<Role> Roles { get; init; } = [];
    }
}