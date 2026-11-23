namespace ExpenseTracker.Domain;

// Immutable transaction record using decimal for exact currency precision.
public sealed record Transaction
{
    // Unique ID for this transaction.
    public Guid Id { get; init; } = Guid.NewGuid();

    // Cash flow direction (Expense or Income).
    public TransactionType Type { get; init; }

    // Positive monetary value rounded to two decimal places.
    public decimal Amount { get; init; }

    // Calendar date of the transaction without time-zone shifts.
    public DateOnly Date { get; init; }

    // Category label; defaults to "Uncategorized" if left blank.
    public string Category { get; init; } = "Uncategorized";

    // Short note or merchant name describing the transaction.
    public string Description { get; init; } = string.Empty;

    // Validates inputs, normalizes text and rounding, and returns a new Transaction.
    public static Transaction Create(
        TransactionType type,
        decimal amount,
        DateOnly date,
        string category,
        string description)
    {
        // Ensure amount is positive so direction stays controlled by Type.
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be greater than zero.");
        }

        // Require a non-empty description.
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Transaction description is required.", nameof(description));
        }

        // Fall back to "Uncategorized" when category is empty or whitespace.
        string normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? "Uncategorized"
            : category.Trim();

        // Round currency to two decimals away from zero.
        return new Transaction
        {
            Id = Guid.NewGuid(),
            Type = type,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Date = date,
            Category = normalizedCategory,
            Description = description.Trim()
        };
    }
}