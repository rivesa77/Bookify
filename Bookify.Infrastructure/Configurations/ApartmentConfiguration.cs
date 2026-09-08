namespace Bookify.Infrastructure.Configurations
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Commons;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
    {
        public void Configure(EntityTypeBuilder<Apartment> builder)
        {
            builder.ToTable("Apartments");

            builder.HasKey(apartment => apartment.Id);

            builder.OwnsOne(apartment => apartment.Address);

            builder.Property(apartment => apartment.Name)
                .HasMaxLength(200)
                .HasConversion(name => name.Value, value => Name.Create(value).Value);

            builder.Property(apartment => apartment.Description)
                .HasMaxLength(2000)
                .HasConversion(description => description.Value, value => new Description(value));

            builder.OwnsOne(apartment => apartment.Price, priceBuilder =>
            {
                priceBuilder.Property(money => money.Currency)
                    .HasConversion(money => money.Code, code => Currency.FromCode(code));
            });

            builder.OwnsOne(apartment => apartment.CleaningFeeAmount, priceBuilder =>
            {
                priceBuilder.Property(money => money.Currency)
                    .HasConversion(money => money.Code, code => Currency.FromCode(code));
            });
        }
    }
}