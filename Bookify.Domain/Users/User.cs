namespace Bookify.Domain.Users
{
    using System;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users.Events;

    public sealed class User : Entity
    {
        private readonly List<Role> roles = [];

        public FirstName FirstName { get; private set; }

        public LastName LastName { get; private set; }

        public Email Email { get; private set; }

        public string Identity { get; private set; } = string.Empty;

        public IReadOnlyCollection<Role> Roles => roles;

        private User()
        {
        }

        private User(
            Guid id,
            FirstName firstName,
            LastName lastName,
            Email email) : base(id)
        {
            FirstName = firstName;
            LastName = lastName;
            Email = email;
        }

        public static User Create(
            FirstName firstName,
            LastName lastName,
            Email email)
        {
            User user = new(
                Guid.NewGuid(),
                firstName,
                lastName,
                email);

            user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

            user.roles.Add(Role.Registered);

            return user;
        }

        public void SetIdentityId(string identityId)
        {
            Identity = identityId;
        }
    }
}