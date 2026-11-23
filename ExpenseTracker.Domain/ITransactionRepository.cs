namespace ExpenseTracker.Domain;

// Defines storage operations for persisting and querying transactions.
public interface ITransactionRepository
{
    // Persists a new transaction entry.
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    // Returns all stored transactions ordered by date descending.
    Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);

    // Returns transactions matching the specified calendar year and month.
    Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    // Removes a transaction by ID; returns true if a matching record existed.
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}