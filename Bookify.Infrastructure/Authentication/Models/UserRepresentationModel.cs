namespace Bookify.Infrastructure.Authentication.Models
{
    using Bookify.Domain.Users;

    public sealed class UserRepresentationModel
    {
        public CredentialRepresentationModel[] Credentials { get; set; } = [];

        public string Email { get; set; } = string.Empty;

        public bool? EmailVerified { get; set; }

        public bool? Enabled { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        internal static UserRepresentationModel FromUser(User user) =>
            new()
            {
                FirstName = user.FirstName.Value,
                LastName = user.LastName.Value,
                Email = user.Email.Value,
                Username = user.Email.Value,
                Enabled = true,
                EmailVerified = true
            };
    }
}
