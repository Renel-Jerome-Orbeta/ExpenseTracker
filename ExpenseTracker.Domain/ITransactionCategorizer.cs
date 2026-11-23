namespace ExpenseTracker.Domain;

// Determines a category for a transaction using explicit, deterministic rules.
public interface ITransactionCategorizer
{
    // Returns a matching category name or "Uncategorized" when no rule matches.
    string Categorize(string description, TransactionType type, string? explicitCategory = null);
}