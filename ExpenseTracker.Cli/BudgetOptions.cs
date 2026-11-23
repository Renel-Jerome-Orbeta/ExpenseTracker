namespace ExpenseTracker.Cli;

// Strongly typed configuration options for default monthly and per-category budgets.
public sealed class BudgetOptions
{
    // Configuration section name bound from appsettings.json.
    public const string SectionName = "Budget";

    // Optional overall monthly expense cap.
    public decimal? MonthlyExpenseLimit { get; set; }

    // Optional per-category spending caps keyed by category name.
    public Dictionary<string, decimal> CategoryLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Optional custom path for the JSON transactions file.
    public string? DataFilePath { get; set; }
}