namespace ExpenseTracker.Domain;

// Specifies whether a transaction is an outgoing expense or incoming income.
public enum TransactionType
{
    // Outgoing money spent.
    Expense = 0,

    // Incoming money received.
    Income = 1
}