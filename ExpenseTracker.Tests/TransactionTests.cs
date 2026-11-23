using ExpenseTracker.Domain;

namespace ExpenseTracker.Tests;

// Unit tests for Transaction creation, validation rules, and decimal rounding.
public sealed class TransactionTests
{
    // Verifies valid inputs create a normalized transaction with rounded currency.
    [Fact]
    public void Create_WithValidInputs_NormalizesFieldsAndRoundsAmount()
    {
        DateOnly date = new(2026, 11, 23);

        Transaction tx = Transaction.Create(
            TransactionType.Expense,
            123.456m,
            date,
            "  Groceries  ",
            "  Weekly market run  ");

        Assert.NotEqual(Guid.Empty, tx.Id);
        Assert.Equal(TransactionType.Expense, tx.Type);
        Assert.Equal(123.46m, tx.Amount);
        Assert.Equal(date, tx.Date);
        Assert.Equal("Groceries", tx.Category);
        Assert.Equal("Weekly market run", tx.Description);
    }

    // Verifies zero or negative amounts are rejected.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNonPositiveAmount_ThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        DateOnly date = new(2026, 11, 23);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transaction.Create(TransactionType.Expense, invalidAmount, date, "Dining", "Lunch"));
    }

    // Verifies blank or whitespace-only descriptions are rejected.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDescription_ThrowsArgumentException(string invalidDescription)
    {
        DateOnly date = new(2026, 11, 23);

        Assert.Throws<ArgumentException>(() =>
            Transaction.Create(TransactionType.Income, 1000m, date, "Salary", invalidDescription));
    }

    // Verifies blank categories fall back to "Uncategorized".
    [Fact]
    public void Create_WithBlankCategory_DefaultsToUncategorized()
    {
        DateOnly date = new(2026, 11, 23);

        Transaction tx = Transaction.Create(TransactionType.Expense, 50m, date, "   ", "Misc item");

        Assert.Equal("Uncategorized", tx.Category);
    }
}