using Bobcat;
using Bobcat.Xunit;

namespace LoanUnderwriting.Specs;

/// <summary>
/// The same kind of table, but on an xUnit theory: every row is its own specification with its
/// own verdict and its own identity, so one row can fail, be tracked or be rerun on its own.
/// </summary>
[BobcatFeature("Loan Underwriting Examples")]
public class UnderwritingExamples
{
    [BobcatTheory]
    [BobcatExamples("""
        | creditScore | debtToIncome | annualIncome | loanAmount | decision | rate | guarantor |
        | 579         | 20           | 90000        | 10000      | Decline  | NULL | false     |
        | 640         | 36           | 90000        | 10000      | Approve  | 9.75 | true      |
        | 700         | 37           | 60000        | 20000      | Refer    | NULL | false     |
        | 740         | 30           | 60000        | 20000      | Approve  | 6.25 | false     |
        | 780         | 43           | 60000        | 45000      | Approve  | 7.50 | false     |
        """)]
    public void an_application_is_underwritten(
        int creditScore, decimal debtToIncome, decimal annualIncome, decimal loanAmount,
        Decision decision, decimal? rate, bool guarantor)
    {
        var actual = Underwriting.Decide(
            new LoanApplication(creditScore, debtToIncome, annualIncome, loanAmount));

        TheDecisionIs(actual, decision, rate, guarantor);
    }

    [Then("the decision is {decision}")]
    internal void TheDecisionIs(UnderwritingDecision actual, Decision decision, decimal? rate, bool guarantor)
    {
        // SpecAssert gathers: all three are checked and reported even when the first is wrong
        SpecAssert.Check("decision", actual.Decision, decision);
        SpecAssert.Check("rate", actual.Rate, rate);
        SpecAssert.Check("guarantor", actual.GuarantorRequired, guarantor);
    }
}
