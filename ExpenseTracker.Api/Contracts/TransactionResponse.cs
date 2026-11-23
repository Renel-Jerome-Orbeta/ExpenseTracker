using ExpenseTracker.Domain;

namespace ExpenseTracker.Api.Contracts;

// API response shape representing a persisted transaction.
public sealed record TransactionResponse(
    // Unique transaction identifier.
    Guid Id,
    // Cash flow direction (Expense or Income).
    TransactionType Type,
    // Monetary amount rounded to two decimal places.
    decimal Amount,
    // Calendar date of the transaction.
    DateOnly Date,
    // Assigned transaction category.
    string Category,
    // Transaction description.
    string Description
)
{
    // Maps a Domain Transaction entity to an API response DTO.
    public static TransactionResponse FromDomain(Transaction transaction) =>
        new(
            Id: transaction.Id,
            Type: transaction.Type,
            Amount: transaction.Amount,
            Date: transaction.Date,
            Category: transaction.Category,
            Description: transaction.Description);
}