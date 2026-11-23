using ExpenseTracker.Domain;

namespace ExpenseTracker.Api.Contracts;

// Request payload for creating a new transaction via the Minimal API.
public sealed record CreateTransactionRequest(
    // Cash flow direction (Expense or Income).
    TransactionType Type,
    // Positive monetary amount.
    decimal Amount,
    // Calendar date of the transaction.
    DateOnly Date,
    // Non-empty description or merchant note.
    string Description,
    // Optional explicit category; falls back to rule-based categorization when omitted.
    string? Category = null
);