namespace Bookify.Infrastructure.Configurations
{
    using Bookify.Domain.Users;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(user => user.Id);

            builder.Property(user => user.FirstName)
                .HasMaxLength(200)
                .HasConversion(firstName => firstName.Value, value => new FirstName(value));

            builder.Property(user => user.LastName)
                .HasMaxLength(200)
                .HasConversion(lastName => lastName.Value, value => new LastName(value));

            builder.Property(user => user.Email)
                .HasMaxLength(400)
                .HasConversion(email => email.Value, value => new Email(value));

            builder.HasIndex(user => user.Email).IsUnique();
        }
    }
}