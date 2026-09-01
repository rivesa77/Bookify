namespace Bookify.Domain.Apartments
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Commons;

    public sealed class Apartment : Entity
    {
        public Apartment(
            Guid id,
            Name name,
            Description description,
            Address address,
            Money price,
            Money cleaningFeeAmount,
            List<Amenity> amenities)
            : base(id)
        {
            Name = name;
            Description = description;
            Address = address;
            Price = price;
            CleaningFeeAmount = cleaningFeeAmount;
            Amenities = amenities;
        }

        public Name Name { get; private set; }

        public Description Description { get; private set; }

        public Address Address { get; private set; }

        public Money Price { get; private set; }

        public Money CleaningFeeAmount { get; private set; }

        public DateTime? LastBookedOnUTC { get; private set; }

        public List<Amenity> Amenities { get; private set; } = [];
    }
}