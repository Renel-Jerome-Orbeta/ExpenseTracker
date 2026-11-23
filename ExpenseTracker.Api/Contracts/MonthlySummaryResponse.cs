using ExpenseTracker.Domain;

namespace ExpenseTracker.Api.Contracts;

// API response payload representing a monthly financial summary and budget alerts.
public sealed record MonthlySummaryResponse(
    // Calendar year of the report.
    int Year,
    // Calendar month (1-12) of the report.
    int Month,
    // Total incoming funds for the month.
    decimal TotalIncome,
    // Total outgoing expenses for the month.
    decimal TotalExpenses,
    // Net balance (TotalIncome minus TotalExpenses).
    decimal NetBalance,
    // Per-category expense breakdown ordered by highest spend first.
    IReadOnlyList<CategorySummary> ExpenseByCategory,
    // Budget status and remaining allowance for requested limits.
    IReadOnlyList<BudgetAlert> BudgetAlerts
)
{
    // Maps a Domain MonthlySummary into an API response contract.
    public static MonthlySummaryResponse FromDomain(MonthlySummary summary) =>
        new(
            Year: summary.Year,
            Month: summary.Month,
            TotalIncome: summary.TotalIncome,
            TotalExpenses: summary.TotalExpenses,
            NetBalance: summary.NetBalance,
            ExpenseByCategory: summary.ExpenseByCategory,
            BudgetAlerts: summary.BudgetAlerts);
}