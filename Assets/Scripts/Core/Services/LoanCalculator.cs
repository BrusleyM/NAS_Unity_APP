using System;
using NAS.Core.Interfaces;

namespace NAS.Core.Services
{
    public class LoanCalculator : ILoanCalculator
    {
        // The balloon is paid at the end, so it still earns interest for the whole term: the payment is the standard
        // annuity on the financed amount less the balloon's present value.
        public float CalculateMonthlyPayment(float financed, float balloon, int months, float annualRatePercent)
        {
            if (financed <= 0 || months <= 0) return 0;
            balloon = Math.Min(Math.Max(balloon, 0f), financed);

            double monthlyRate = annualRatePercent / 100.0 / 12.0;
            if (monthlyRate == 0) return (float)((financed - balloon) / months);

            double growth = Math.Pow(1 + monthlyRate, months);
            double presentBalloon = balloon / growth;
            return (float)((financed - presentBalloon) * monthlyRate * growth / (growth - 1));
        }
    }
}
