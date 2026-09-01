namespace Bookify.Domain.Apartments
{
    internal record Currency
    {
        internal static readonly Currency None = new("");
        public static readonly Currency Usd = new("USD");
        public static readonly Currency Eur = new("EUR");

        private Currency(string code) => Code = code;

        public string Code { get; init; }

        public static readonly IReadOnlyCollection<Currency> All =
        [
            Usd, Eur,
        ];

        public static Currency FromCode(string code)
        {
            return All.FirstOrDefault(x => x.Code == code) ??
                throw new ApplicationException("The currency code is invalid.");
        }
    }
}