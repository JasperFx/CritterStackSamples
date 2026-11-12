using Bobcat;
using Bobcat.Runtime;

namespace LoanUnderwriting.Specs;

/// <summary>
/// The whole underwriting rule as one decision table: one specification, one grid, a verdict
/// in every output cell of every row.
/// </summary>
[BobcatFeature("Loan Underwriting")]
public class UnderwritingDecisionTable
{
    [Fact]
    public async Task the_underwriting_rules()
    {
        await TheUnderwritingDecisionsAre("""
            | Credit Score | Debt to Income | Annual Income | Loan Amount | Decision | Rate | Guarantor |
            | 579          | 20             | 90000         | 10000       | Decline  | NULL | false     |
            | 580          | 20             | 90000         | 10000       | Approve  | 9.75 | true      |
            | 640          | 36             | 90000         | 10000       | Approve  | 9.75 | true      |
            | 640          | 37             | 90000         | 10000       | Decline  | NULL | false     |
            | 669          | 30             | 60000         | 30001       | Refer    | NULL | false     |
            | 670          | 30             | 60000         | 30000       | Approve  | 7.50 | false     |
            | 700          | 36             | 60000         | 20000       | Approve  | 7.50 | false     |
            | 700          | 37             | 60000         | 20000       | Refer    | NULL | false     |
            | 700          | 30             | 60000         | 30001       | Refer    | NULL | false     |
            | 739          | 30             | 60000         | 20000       | Approve  | 7.50 | false     |
            | 740          | 30             | 60000         | 20000       | Approve  | 6.25 | false     |
            | 780          | 37             | 60000         | 20000       | Approve  | 7.00 | false     |
            | 780          | 30             | 60000         | 30001       | Approve  | 6.75 | false     |
            | 780          | 43             | 60000         | 45000       | Approve  | 7.50 | false     |
            | 780          | 44             | 60000         | 20000       | Decline  | NULL | false     |
            | 820          | 10             | 250000        | 50000       | Approve  | 6.25 | false     |
            """);
    }

    [Then("the underwriting decisions are")]
    internal async Task TheUnderwritingDecisionsAre(StepTable table)
        => await TableRunner.Run(this, nameof(decide), table);

    // One call per row. The ordinary parameters are the input columns, each out parameter is an
    // output column, and the returned value is checked against the one column nothing else
    // claims, which is Decision.
    private Decision decide(
        [Header("Credit Score")] int creditScore,
        [Header("Debt to Income")] decimal debtToIncome,
        [Header("Annual Income")] decimal annualIncome,
        [Header("Loan Amount")] decimal loanAmount,
        out decimal? rate,
        out bool guarantor)
    {
        var decision = Underwriting.Decide(
            new LoanApplication(creditScore, debtToIncome, annualIncome, loanAmount));

        rate = decision.Rate;
        guarantor = decision.GuarantorRequired;

        return decision.Decision;
    }
}
