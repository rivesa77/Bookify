namespace Bookify.Domain.Apartments
{
    internal record Address(
        string Country,
        string State,
        string ZipCode,
        string City,
        string Street)
    {
    }
}