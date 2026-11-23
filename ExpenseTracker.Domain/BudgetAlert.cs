namespace ExpenseTracker.Domain;

// Represents budget status for a single spending category or the overall monthly budget.
public sealed record BudgetAlert(
    // Category name or "Monthly Total" for overall spending.
    string Scope,
    // Configured spending limit for the period.
    decimal BudgetLimit,
    // Actual spent amount for the period.
    decimal ActualSpent,
    // Remaining allowance (negative when over budget).
    decimal RemainingAmount,
    // True when actual spending exceeds the configured limit.
    bool IsOverBudget
);