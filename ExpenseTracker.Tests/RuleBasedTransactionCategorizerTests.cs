using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Tests;

// Unit tests for deterministic rule-based categorization.
public sealed class RuleBasedTransactionCategorizerTests
{
    private readonly RuleBasedTransactionCategorizer _categorizer = new();

    // Verifies explicit user categories override automatic rule matching.
    [Fact]
    public void Categorize_WhenExplicitCategoryProvided_ReturnsTrimmedExplicitCategory()
    {
        string result = _categorizer.Categorize(
            "Supermarket groceries",
            TransactionType.Expense,
            "  Household Stock  ");

        Assert.Equal("Household Stock", result);
    }

    // Verifies expense keywords map to expected categories.
    [Theory]
    [InlineData("Supermarket groceries buy", "Groceries")]
    [InlineData("Monthly fiber internet bill", "Utilities")]
    [InlineData("Commute fare and parking", "Transport")]
    [InlineData("Evening dinner and coffee", "Dining")]
    [InlineData("Random street stall", "Uncategorized")]
    public void Categorize_ExpenseDescriptions_MatchesExpectedCategory(string description, string expectedCategory)
    {
        string result = _categorizer.Categorize(description, TransactionType.Expense, explicitCategory: null);

        Assert.Equal(expectedCategory, result);
    }

    // Verifies income transactions map to Salary & Income or Other Income.
    [Theory]
    [InlineData("November salary payroll", "Salary & Income")]
    [InlineData("Freelance client invoice", "Salary & Income")]
    [InlineData("Sold old desk lamp", "Other Income")]
    public void Categorize_IncomeDescriptions_MatchesExpectedCategory(string description, string expectedCategory)
    {
        string result = _categorizer.Categorize(description, TransactionType.Income, explicitCategory: null);

        Assert.Equal(expectedCategory, result);
    }
}