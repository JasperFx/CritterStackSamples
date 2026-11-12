# Loan Underwriting: a decision table with Bobcat

A small sample that shows how to use [Bobcat](https://bobcat.jasperfx.net/) to test a business rule
with a **table**: several inputs per row, several expected outputs per row, and a verdict for every
output cell.

There is no database, no messaging and no host here on purpose. The rule is one pure function, and
the whole sample runs with nothing but the .NET SDK.

## What it is meant to show

- **A decision table as one specification.** `UnderwritingDecisionTable` runs sixteen loan
  applications through the rule with `TableRunner.Run`. Ordinary parameters are the input columns,
  `out` parameters are output columns, and the returned value is the last output column. The run
  renders as one grid.
- **The same idea as one specification per row.** `UnderwritingExamples` puts the rows on an xUnit
  theory with `[BobcatTheory]` and `[BobcatExamples]`. Each row is its own test with its own
  verdict and identity, and the table is checked at build time.
- **Failures that say which cell was wrong.** A wrong row doesn't stop the table. Every output of
  every row is judged, and a failed cell reads `expected 'Approve', got 'Decline'`.
- **Testing a business rule at the lowest layer that can prove it.** The rule is a static method, so
  the specifications call it directly.

## The rule

`Underwriting.Decide()` in `LoanUnderwriting/Underwriting.cs` takes a credit score, a debt to income
percentage, an annual income and a loan amount, and answers with a decision (`Approve`, `Refer` or
`Decline`), an interest rate and whether a guarantor is required.

1. A credit score under 580 is declined.
2. A debt to income over 43% is declined.
3. A loan bigger than half of annual income is referred to a person, unless the score is prime (740+).
4. Prime (740+) is approved at 6.25%, plus 0.75 when debt to income is over 36%, plus 0.50 for a
   large loan.
5. Standard (670 to 739) is approved at 7.50%, or referred when debt to income is over 36%.
6. Subprime (580 to 669) is approved at 9.75% with a guarantor, or declined when debt to income is
   over 36%.

## Running it

You need the .NET 10 SDK.

```bash
dotnet test --project LoanUnderwriting.Specs
```

To see the rendered specifications, install the Bobcat tool and run through it:

```bash
dotnet tool install -g Bobcat.Console
bobcat run LoanUnderwriting.Specs --verbose
bobcat run LoanUnderwriting.Specs --html results.html    # one self-contained page
```

`--verbose` prints passing specifications too. Without it you only see the ones that failed.

## Seeing it fail

Change one comparison in `Underwriting.Decide()` from `>` to `>=`:

```csharp
var stretched = application.DebtToIncome >= StretchedDebtToIncome;
```

Rerun, and the two rows that sit exactly on the 36% boundary go red in the decision table, with
every wrong output named. In `UnderwritingExamples` exactly one of the five rows fails and the other
four still pass.

## Layout

| | |
|---|---|
| `LoanUnderwriting/Underwriting.cs` | the rule, as a pure function |
| `LoanUnderwriting.Specs/UnderwritingDecisionTable.cs` | one specification, one table |
| `LoanUnderwriting.Specs/UnderwritingExamples.cs` | one specification per row |
| `global.json` | puts `dotnet test` on Microsoft.Testing.Platform, which xUnit v3 4.x needs |

See the [Data Intensive Specifications](https://bobcat.jasperfx.net/tutorials/data-intensive-specifications)
tutorial in the Bobcat documentation for everything else a table can do.
