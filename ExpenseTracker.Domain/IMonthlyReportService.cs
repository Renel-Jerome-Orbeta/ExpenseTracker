namespace ExpenseTracker.Domain;

// Generates monthly financial summaries and budget alerts from stored transactions.
public interface IMonthlyReportService
{
    // Builds a monthly summary using an optional overall monthly cap and per-category limits.
    Task<MonthlySummary> GenerateMonthlySummaryAsync(
        int year,
        int month,
        decimal? monthlyExpenseLimit = null,
        IReadOnlyDictionary<string, decimal>? categoryLimits = null,
        CancellationToken cancellationToken = default);
}