namespace Bookify.Infrastructure.Configurations
{
    using Bookify.Domain.Users;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");

            builder.HasKey(role => role.Id);

            builder
                .HasMany(role => role.Users)
                .WithMany(user => user.Roles);

            builder.HasData(Role.Registered);
        }
    }
}