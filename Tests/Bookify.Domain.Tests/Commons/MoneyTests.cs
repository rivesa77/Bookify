namespace Bookify.Domain.Tests.Commons
{
    using Bookify.Domain.Commons;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class MoneyTests
    {
        private static readonly Currency Currency = Currency.Eur;

        [TestMethod]
        public void Addition_SameCurrency_Should_AddAmountsWithoutChangingOperands()
        {
            // Arrange
            Money first = new(10.25m, Currency);

            Money second = new(5.75m, Currency);

            // Act
            Money result = first + second;

            // Assert
            result.Should().Be(new Money(16m, Currency));

            first.Amount.Should().Be(10.25m);

            second.Amount.Should().Be(5.75m);
        }

        [TestMethod]
        public void Addition_DifferentCurrencies_Should_Throw()
        {
            // Arrange
            Money first = new(10m, Currency);

            Money second = new(10m, Currency.Usd);

            // Act
            Func<Money> act = () => first + second;

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("Currencies have to be equal.");
        }

        [TestMethod]
        public void Zero_Should_CreateZeroWithSpecifiedOrEmptyCurrency()
        {
            // Arrange
            Currency expected = Currency;

            // Act
            Money specified = Money.Zero(expected);

            Money unspecified = Money.Zero();

            // Assert
            specified.Amount.Should().Be(0m);

            specified.Currency.Should().Be(expected);

            specified.IsZero().Should().BeTrue();

            unspecified.Amount.Should().Be(0m);

            unspecified.Currency.Code.Should().BeEmpty();

            unspecified.IsZero().Should().BeTrue();
        }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(0, true)]
        [DataRow(1, false)]
        public void IsZero_Should_InspectAmount(int amount, bool expected)
        {
            // Arrange
            Money money = new(amount, Currency);

            // Act
            bool result = money.IsZero();

            // Assert
            result.Should().Be(expected);
        }

        [TestMethod]
        public void Equality_Should_CompareAmountAndCurrency()
        {
            // Arrange
            Money money = new(10m, Currency);

            // Act
            Money same = new(10m, Currency.FromCode("EUR"));

            // Assert
            money.Should().Be(same);

            money.Should().NotBe(new Money(11m, Currency));

            money.Should().NotBe(new Money(10m, Currency.Usd));
        }
    }
}
