using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Tests;

// Unit tests for JsonTransactionRepository persistence, filtering, and deletion.
public sealed class JsonTransactionRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _filePath;

    // Creates an isolated temporary directory per test run.
    public JsonTransactionRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ExpenseTrackerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _filePath = Path.Combine(_tempDirectory, "transactions.json");
    }

    // Verifies add, month filter, and delete persist cleanly to disk.
    [Fact]
    public async Task AddGetByMonthAndDeleteAsync_PersistsAcrossInstances()
    {
        JsonTransactionRepository repo = new(_filePath);

        Transaction novTx = Transaction.Create(
            TransactionType.Expense, 250m, new DateOnly(2026, 11, 12), "Dining", "Lunch");
        Transaction octTx = Transaction.Create(
            TransactionType.Income, 1500m, new DateOnly(2026, 10, 28), "Salary & Income", "Bonus");

        await repo.AddAsync(novTx);
        await repo.AddAsync(octTx);

        // Create a second repository instance pointing to the same JSON file to verify disk persistence.
        JsonTransactionRepository reloadedRepo = new(_filePath);
        IReadOnlyList<Transaction> all = await reloadedRepo.GetAllAsync();
        IReadOnlyList<Transaction> novOnly = await reloadedRepo.GetByMonthAsync(2026, 11);

        Assert.Equal(2, all.Count);
        Assert.Single(novOnly);
        Assert.Equal(novTx.Id, novOnly[0].Id);

        bool deleted = await reloadedRepo.DeleteAsync(novTx.Id);
        Assert.True(deleted);
        Assert.Single(await reloadedRepo.GetAllAsync());
    }

    // Cleans up the temporary directory after each test.
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}