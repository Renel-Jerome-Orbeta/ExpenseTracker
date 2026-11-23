namespace ExpenseTracker.Domain;

// Immutable summary of totals and counts for a single category.
public sealed record CategorySummary(
    // Category name.
    string Category,
    // Combined amount for the category in the selected period.
    decimal TotalAmount,
    // Number of transactions in this category.
    int TransactionCount,
    // Share of the overall period total as a percentage.
    decimal PercentageOfTotal
);