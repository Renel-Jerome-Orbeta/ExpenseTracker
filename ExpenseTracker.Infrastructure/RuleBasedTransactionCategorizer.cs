using System.Text.RegularExpressions;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

// Categorizes transactions with deterministic keyword/regex rules and C# pattern matching.
public sealed partial class RuleBasedTransactionCategorizer : ITransactionCategorizer
{
    // Matches whole words or clear tokens for grocery stores and supermarkets.
    [GeneratedRegex(@"\b(grocery|groceries|supermarket|mart|market|produce|bakery)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GroceryRegex();

    // Matches utility bills such as electricity, water, internet, and mobile plans.
    [GeneratedRegex(@"\b(electric|electricity|meralco|water|primewater|internet|fiber|broadband|utility|utilities|bill)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UtilitiesRegex();

    // Matches transit, fuel, ride-hailing, parking, and commuting expenses.
    [GeneratedRegex(@"\b(fuel|gasoline|petrol|diesel|grab|taxi|uber|bus|train|lrt|mrt|toll|parking|fare|commute)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransportRegex();

    // Matches cafes, restaurants, takeout, and food delivery.
    [GeneratedRegex(@"\b(coffee|cafe|restaurant|dining|lunch|dinner|breakfast|foodpanda|grabfood|burger|pizza)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiningRegex();

    // Matches common income sources such as salary, payroll, freelance work, and refunds.
    [GeneratedRegex(@"\b(salary|payroll|wage|wages|freelance|client|invoice|dividend|interest|refund|bonus)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncomeRegex();

    // Evaluates explicit category first, then falls back to pattern matching on type and description.
    public string Categorize(string description, TransactionType type, string? explicitCategory = null)
    {
        // Honor a user-supplied category whenever one is explicitly provided.
        if (!string.IsNullOrWhiteSpace(explicitCategory))
        {
            return explicitCategory.Trim();
        }

        string normalizedText = description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return "Uncategorized";
        }

        // Match transaction type and text patterns in priority order.
        return (type, normalizedText) switch
        {
            (TransactionType.Income, var text) when IncomeRegex().IsMatch(text) => "Salary & Income",
            (TransactionType.Income, _) => "Other Income",
            (TransactionType.Expense, var text) when GroceryRegex().IsMatch(text) => "Groceries",
            (TransactionType.Expense, var text) when UtilitiesRegex().IsMatch(text) => "Utilities",
            (TransactionType.Expense, var text) when TransportRegex().IsMatch(text) => "Transport",
            (TransactionType.Expense, var text) when DiningRegex().IsMatch(text) => "Dining",
            _ => "Uncategorized"
        };
    }
}