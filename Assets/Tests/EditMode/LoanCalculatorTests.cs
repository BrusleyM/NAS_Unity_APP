using NAS.Core.Services;
using NUnit.Framework;

namespace NAS.Tests
{
    public class LoanCalculatorTests
    {
        private readonly LoanCalculator _calculator = new LoanCalculator();

        [Test]
        public void WithoutABalloonItIsTheStandardLoanPayment()
        {
            // R100 000 over 60 months at 9% a year.
            Assert.AreEqual(2075.84f, _calculator.CalculateMonthlyPayment(100000f, 0f, 60, 9f), 0.5f);
        }

        [Test]
        public void ABalloonStillPaysInterestOnTheBalloonSoThePaymentIsNotJustTheReducedLoan()
        {
            // R100 000 with a R30 000 balloon over 60 months at 9%: R1 678 a month, not the R1 453 of a R70 000 loan.
            var withBalloon = _calculator.CalculateMonthlyPayment(100000f, 30000f, 60, 9f);

            Assert.AreEqual(1678f, withBalloon, 1f);
            Assert.Greater(withBalloon, _calculator.CalculateMonthlyPayment(70000f, 0f, 60, 9f));
        }

        [Test]
        public void ABiggerBalloonMeansASmallerPayment()
        {
            var small = _calculator.CalculateMonthlyPayment(100000f, 10000f, 48, 11f);
            var big = _calculator.CalculateMonthlyPayment(100000f, 40000f, 48, 11f);

            Assert.Less(big, small);
        }

        [Test]
        public void AtZeroInterestTheBalloonIsSimplyLeftOut()
        {
            Assert.AreEqual(1000f, _calculator.CalculateMonthlyPayment(100000f, 40000f, 60, 0f), 0.01f);
        }

        [Test]
        public void ABalloonThatCoversTheWholeLoanLeavesInterestOnly()
        {
            // 12% a year is 1% a month on R100 000.
            Assert.AreEqual(1000f, _calculator.CalculateMonthlyPayment(100000f, 100000f, 36, 12f), 0.5f);
        }

        [Test]
        public void ABalloonLargerThanTheLoanIsCappedAtTheLoan()
        {
            Assert.AreEqual(
                _calculator.CalculateMonthlyPayment(100000f, 100000f, 36, 12f),
                _calculator.CalculateMonthlyPayment(100000f, 250000f, 36, 12f), 0.01f);
        }

        [Test]
        public void NothingFinancedOrNoTermMeansNoPayment()
        {
            Assert.AreEqual(0f, _calculator.CalculateMonthlyPayment(0f, 0f, 60, 9f));
            Assert.AreEqual(0f, _calculator.CalculateMonthlyPayment(-500f, 0f, 60, 9f));
            Assert.AreEqual(0f, _calculator.CalculateMonthlyPayment(100000f, 0f, 0, 9f));
        }
    }
}
