namespace Bookify.Infrastructure.Configurations
{
    using Bookify.Domain.Users;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("permissions");

            builder.HasKey(permission => permission.Id);

            builder.Property(permission => permission.Name)
                .IsRequired();

            builder.HasData(Permission.UserRead);
        }
    }
}
