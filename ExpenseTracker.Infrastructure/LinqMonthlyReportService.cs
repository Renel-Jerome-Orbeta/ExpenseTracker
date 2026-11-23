using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

// Computes monthly totals, category percentages, and budget alerts using LINQ queries.
public sealed class LinqMonthlyReportService : IMonthlyReportService
{
    // Repository providing stored transactions.
    private readonly ITransactionRepository _repository;

    // Injects the transaction repository dependency.
    public LinqMonthlyReportService(ITransactionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // Aggregates monthly income, expenses, category share, and budget status.
    public async Task<MonthlySummary> GenerateMonthlySummaryAsync(
        int year,
        int month,
        decimal? monthlyExpenseLimit = null,
        IReadOnlyDictionary<string, decimal>? categoryLimits = null,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        IReadOnlyList<Transaction> monthlyTransactions = await _repository.GetByMonthAsync(year, month, cancellationToken);

        // Sum income and expense entries separately with exact decimal rounding.
        decimal totalIncome = decimal.Round(
            monthlyTransactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => t.Amount),
            2,
            MidpointRounding.AwayFromZero);

        decimal totalExpenses = decimal.Round(
            monthlyTransactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => t.Amount),
            2,
            MidpointRounding.AwayFromZero);

        decimal netBalance = decimal.Round(totalIncome - totalExpenses, 2, MidpointRounding.AwayFromZero);

        // Group expenses by category and compute each category's share of total monthly expenses.
        List<CategorySummary> expenseByCategory = monthlyTransactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                decimal categoryTotal = decimal.Round(group.Sum(t => t.Amount), 2, MidpointRounding.AwayFromZero);
                decimal percentage = totalExpenses > 0m
                    ? decimal.Round((categoryTotal / totalExpenses) * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new CategorySummary(
                    Category: group.Key,
                    TotalAmount: categoryTotal,
                    TransactionCount: group.Count(),
                    PercentageOfTotal: percentage);
            })
            .OrderByDescending(c => c.TotalAmount)
            .ThenBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<BudgetAlert> alerts = [];

        // Evaluate overall monthly spending limit when configured.
        if (monthlyExpenseLimit.HasValue)
        {
            decimal limit = decimal.Round(monthlyExpenseLimit.Value, 2, MidpointRounding.AwayFromZero);
            decimal remaining = decimal.Round(limit - totalExpenses, 2, MidpointRounding.AwayFromZero);
            alerts.Add(new BudgetAlert(
                Scope: "Monthly Total",
                BudgetLimit: limit,
                ActualSpent: totalExpenses,
                RemainingAmount: remaining,
                IsOverBudget: totalExpenses > limit));
        }

        // Evaluate per-category spending limits against actual category totals.
        if (categoryLimits is not null)
        {
            Dictionary<string, decimal> spendLookup = expenseByCategory
                .ToDictionary(c => c.Category, c => c.TotalAmount, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, decimal> entry in categoryLimits.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                decimal limit = decimal.Round(entry.Value, 2, MidpointRounding.AwayFromZero);
                decimal spent = spendLookup.GetValueOrDefault(entry.Key, 0m);
                decimal remaining = decimal.Round(limit - spent, 2, MidpointRounding.AwayFromZero);

                alerts.Add(new BudgetAlert(
                    Scope: entry.Key,
                    BudgetLimit: limit,
                    ActualSpent: spent,
                    RemainingAmount: remaining,
                    IsOverBudget: spent > limit));
            }
        }

        return new MonthlySummary(
            Year: year,
            Month: month,
            TotalIncome: totalIncome,
            TotalExpenses: totalExpenses,
            NetBalance: netBalance,
            ExpenseByCategory: expenseByCategory,
            BudgetAlerts: alerts);
    }
}