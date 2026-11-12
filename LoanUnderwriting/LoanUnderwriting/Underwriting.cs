namespace LoanUnderwriting;

public enum Decision
{
    Approve,
    Refer,
    Decline
}

public enum CreditTier
{
    Subprime,
    Standard,
    Prime
}

/// <summary>
/// What a personal loan applicant hands us.
/// </summary>
/// <param name="CreditScore">FICO-style score, 300 to 850</param>
/// <param name="DebtToIncome">Monthly debt payments as a percentage of monthly income</param>
/// <param name="AnnualIncome">Verified annual income</param>
/// <param name="LoanAmount">The amount being asked for</param>
public record LoanApplication(int CreditScore, decimal DebtToIncome, decimal AnnualIncome, decimal LoanAmount);

/// <summary>
/// What underwriting says back. The rate is only present on an approval.
/// </summary>
public record UnderwritingDecision(Decision Decision, decimal? Rate, bool GuarantorRequired, string Reason);

/// <summary>
/// The underwriting rules for an unsecured personal loan, as one pure function. No database, no
/// messaging, no host: an application goes in and a decision comes out.
/// </summary>
public static class Underwriting
{
    public const int MinimumCreditScore = 580;
    public const int StandardCreditScore = 670;
    public const int PrimeCreditScore = 740;

    public const decimal MaximumDebtToIncome = 43m;
    public const decimal StretchedDebtToIncome = 36m;

    public const decimal PrimeRate = 6.25m;
    public const decimal StandardRate = 7.50m;
    public const decimal SubprimeRate = 9.75m;

    public const decimal StretchedSurcharge = 0.75m;
    public const decimal LargeLoanSurcharge = 0.50m;

    public static CreditTier TierFor(int creditScore) => creditScore switch
    {
        >= PrimeCreditScore => CreditTier.Prime,
        >= StandardCreditScore => CreditTier.Standard,
        _ => CreditTier.Subprime
    };

    public static UnderwritingDecision Decide(LoanApplication application)
    {
        // Two knock-out rules come first, and nothing else about the application can save it
        if (application.CreditScore < MinimumCreditScore)
        {
            return Declined($"Credit score is below {MinimumCreditScore}");
        }

        if (application.DebtToIncome > MaximumDebtToIncome)
        {
            return Declined($"Debt to income is above {MaximumDebtToIncome}%");
        }

        var tier = TierFor(application.CreditScore);
        var stretched = application.DebtToIncome > StretchedDebtToIncome;
        var largeLoan = application.LoanAmount > application.AnnualIncome / 2;

        // Only a prime borrower gets a loan bigger than half their income without a person looking at it
        if (largeLoan && tier != CreditTier.Prime)
        {
            return Referred("Loan is more than half of annual income");
        }

        switch (tier)
        {
            case CreditTier.Prime:
                var rate = PrimeRate;
                if (stretched) rate += StretchedSurcharge;
                if (largeLoan) rate += LargeLoanSurcharge;
                return Approved(rate);

            case CreditTier.Standard:
                return stretched
                    ? Referred($"Debt to income is above {StretchedDebtToIncome}%")
                    : Approved(StandardRate);

            default:
                return stretched
                    ? Declined($"Debt to income is above {StretchedDebtToIncome}% for a subprime score")
                    : Approved(SubprimeRate, guarantorRequired: true);
        }
    }

    private static UnderwritingDecision Approved(decimal rate, bool guarantorRequired = false)
        => new(Decision.Approve, rate, guarantorRequired, "Approved");

    private static UnderwritingDecision Referred(string reason)
        => new(Decision.Refer, null, false, reason);

    private static UnderwritingDecision Declined(string reason)
        => new(Decision.Decline, null, false, reason);
}
