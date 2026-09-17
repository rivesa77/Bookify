namespace Bookify.Infrastructure.Configurations
{
    using Bookify.Domain.Users;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("role_permissions");

            builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

            RolePermission rolePermission = new()
            {
                RoleId = Role.Registered.Id,
                PermissionId = Permission.UserRead.Id,
            };

            builder.HasData(rolePermission);
        }
    }
}