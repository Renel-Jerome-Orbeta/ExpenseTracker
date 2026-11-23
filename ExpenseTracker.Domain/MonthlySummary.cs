namespace ExpenseTracker.Domain;

// Immutable monthly report containing cash flow totals, category breakdowns, and budget alerts.
public sealed record MonthlySummary(
    // Calendar year of the report.
    int Year,
    // Calendar month (1-12) of the report.
    int Month,
    // Total incoming funds for the month.
    decimal TotalIncome,
    // Total outgoing expenses for the month.
    decimal TotalExpenses,
    // Net cash flow (TotalIncome minus TotalExpenses).
    decimal NetBalance,
    // Per-category expense breakdown sorted by highest spend first.
    IReadOnlyList<CategorySummary> ExpenseByCategory,
    // Budget status alerts for configured limits.
    IReadOnlyList<BudgetAlert> BudgetAlerts
);