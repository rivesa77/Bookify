namespace Bookify.Domain.Tests.Commons
{
    using Bookify.Domain.Commons;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class CurrencyTests
    {
        [TestMethod]
        [DataRow("EUR")]
        [DataRow("USD")]
        public void FromCode_SupportedCode_Should_ReturnRegisteredCurrency(string code)
        {
            // Arrange
            Currency expected = code == "EUR" ? Currency.Eur : Currency.Usd;

            // Act
            Currency result = Currency.FromCode(code);

            // Assert
            result.Should().BeSameAs(expected);

            result.Code.Should().Be(code);

            Currency.All.Should().Contain(result);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("eur")]
        [DataRow("GBP")]
        [DataRow(" EUR ")]
        public void FromCode_UnsupportedCode_Should_Throw(string? code)
        {
            // Arrange
            string? input = code;

            // Act
            Func<Currency> act = () => Currency.FromCode(input!);

            // Assert
            act.Should().Throw<ApplicationException>().WithMessage("The currency code is invalid.");
        }

        [TestMethod]
        public void All_Should_ContainOnlySupportedCurrenciesWithoutDuplicates()
        {
            // Arrange
            Currency[] expected = [Currency.Usd, Currency.Eur];

            // Act
            IReadOnlyCollection<Currency> currencies = Currency.All;

            // Assert
            currencies.Should().BeEquivalentTo(expected);

            currencies.Should().OnlyHaveUniqueItems(c => c.Code);
        }
    }
}
