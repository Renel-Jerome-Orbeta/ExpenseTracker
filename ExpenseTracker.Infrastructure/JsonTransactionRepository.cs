using System.Text.Json;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

// Stores transactions in a local JSON file using safe writes and thread-safe access.
public sealed class JsonTransactionRepository : ITransactionRepository
{
    // Path to the JSON file storing serialized transactions.
    private readonly string _filePath;

    // Prevents concurrent read/write races within the same process.
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    // Formats JSON cleanly for readability and preserves record properties.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    // Initializes the repository and ensures the target directory exists.
    public JsonTransactionRepository(string? filePath = null)
    {
        // Default to a local app data folder if no custom path is passed.
        string defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExpenseTracker");

        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(defaultFolder, "transactions.json")
            : filePath;

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    // Adds a transaction and saves the updated list to disk.
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            List<Transaction> transactions = await LoadInternalAsync(cancellationToken);
            transactions.Add(transaction);
            await SaveInternalAsync(transactions, cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // Loads all transactions sorted newest-first.
    public async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            List<Transaction> transactions = await LoadInternalAsync(cancellationToken);
            return transactions
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id)
                .ToList();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // Filters stored transactions by calendar year and month.
    public async Task<IReadOnlyList<Transaction>> GetByMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            List<Transaction> transactions = await LoadInternalAsync(cancellationToken);
            return transactions
                .Where(t => t.Date.Year == year && t.Date.Month == month)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id)
                .ToList();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // Deletes a transaction by ID and saves the remaining records.
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            List<Transaction> transactions = await LoadInternalAsync(cancellationToken);
            int removedCount = transactions.RemoveAll(t => t.Id == id);
            if (removedCount == 0)
            {
                return false;
            }

            await SaveInternalAsync(transactions, cancellationToken);
            return true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // Reads transactions from the JSON file, returning an empty list if missing or empty.
    private async Task<List<Transaction>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using FileStream stream = new(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        if (stream.Length == 0)
        {
            return [];
        }

        List<Transaction>? loaded = await JsonSerializer.DeserializeAsync<List<Transaction>>(
            stream,
            SerializerOptions,
            cancellationToken);

        return loaded ?? [];
    }

    // Writes to a temporary file first, then replaces the target file to avoid partial writes.
    private async Task SaveInternalAsync(List<Transaction> transactions, CancellationToken cancellationToken)
    {
        string tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (FileStream stream = new(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, transactions, SerializerOptions, cancellationToken);
            }

            // Overwrite the destination file atomically once serialization finishes.
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}