using System.Globalization;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

// Entry point for the local ExpenseTracker CLI (no external services or AI dependencies).
ITransactionRepository repository = new JsonTransactionRepository();
ITransactionCategorizer categorizer = new RuleBasedTransactionCategorizer();
IMonthlyReportService reportService = new LinqMonthlyReportService(repository);

if (args.Length == 0 || IsHelpFlag(args[0]))
{
    PrintUsage();
    return 0;
}

string command = args[0].ToLowerInvariant();

try
{
    return command switch
    {
        "add" => await HandleAddAsync(args[1..], repository, categorizer),
        "list" => await HandleListAsync(args[1..], repository),
        "summary" => await HandleSummaryAsync(args[1..], reportService),
        "export" => await HandleExportAsync(args[1..], repository),
        _ => HandleUnknownCommand(args[0])
    };
}
catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
{
    Console.Error.WriteLine($"Input error: {ex.Message}");
    return 1;
}

// Parses arguments for `add`, categorizes the entry, saves it, and prints confirmation.
static async Task<int> HandleAddAsync(
    string[] args,
    ITransactionRepository repository,
    ITransactionCategorizer categorizer)
{
    if (args.Length == 0 || IsHelpFlag(args[0]))
    {
        PrintAddUsage();
        return 0;
    }

    TransactionType? type = null;
    decimal? amount = null;
    string? description = null;
    string? category = null;
    DateOnly date = DateOnly.FromDateTime(DateTime.Today);

    // Parse flag-value pairs cleanly so argument order stays flexible.
    for (int i = 0; i < args.Length; i++)
    {
        string flag = args[i].ToLowerInvariant();
        string? value = i + 1 < args.Length ? args[i + 1] : null;

        switch (flag)
        {
            case "--type" or "-t":
                RequireValue(flag, value);
                type = ParseTransactionType(value!);
                i++;
                break;

            case "--amount" or "-a":
                RequireValue(flag, value);
                amount = ParsePositiveDecimal(value!, "Amount");
                i++;
                break;

            case "--description" or "--desc" or "-d":
                RequireValue(flag, value);
                description = value;
                i++;
                break;

            case "--category" or "-c":
                RequireValue(flag, value);
                category = value;
                i++;
                break;

            case "--date":
                RequireValue(flag, value);
                date = ParseDateOnly(value!);
                i++;
                break;

            default:
                throw new ArgumentException($"Unknown option '{args[i]}' for 'add'.");
        }
    }

    if (!type.HasValue)
    {
        throw new ArgumentException("Missing required option --type (expense or income).");
    }

    if (!amount.HasValue)
    {
        throw new ArgumentException("Missing required option --amount.");
    }

    if (string.IsNullOrWhiteSpace(description))
    {
        throw new ArgumentException("Missing required option --description.");
    }

    // Use explicit category if supplied; otherwise apply deterministic rule-based categorization.
    string resolvedCategory = categorizer.Categorize(description, type.Value, category);
    Transaction transaction = Transaction.Create(type.Value, amount.Value, date, resolvedCategory, description);

    await repository.AddAsync(transaction);

    Console.WriteLine("Saved transaction:");
    Console.WriteLine($"  ID:          {transaction.Id}");
    Console.WriteLine($"  Date:        {transaction.Date:yyyy-MM-dd}");
    Console.WriteLine($"  Type:        {transaction.Type}");
    Console.WriteLine($"  Category:    {transaction.Category}");
    Console.WriteLine($"  Amount:      {transaction.Amount:N2}");
    Console.WriteLine($"  Description: {transaction.Description}");

    return 0;
}

// Parses optional `--month YYYY-MM` filter and prints matching transactions in a table.
static async Task<int> HandleListAsync(string[] args, ITransactionRepository repository)
{
    if (args.Length > 0 && IsHelpFlag(args[0]))
    {
        PrintListUsage();
        return 0;
    }

    int? filterYear = null;
    int? filterMonth = null;

    for (int i = 0; i < args.Length; i++)
    {
        string flag = args[i].ToLowerInvariant();
        string? value = i + 1 < args.Length ? args[i + 1] : null;

        switch (flag)
        {
            case "--month" or "-m":
                RequireValue(flag, value);
                (int year, int month) = ParseYearMonth(value!);
                filterYear = year;
                filterMonth = month;
                i++;
                break;

            default:
                throw new ArgumentException($"Unknown option '{args[i]}' for 'list'.");
        }
    }

    IReadOnlyList<Transaction> transactions = filterYear.HasValue && filterMonth.HasValue
        ? await repository.GetByMonthAsync(filterYear.Value, filterMonth.Value)
        : await repository.GetAllAsync();

    if (transactions.Count == 0)
    {
        Console.WriteLine("No transactions found.");
        return 0;
    }

    Console.WriteLine($"{"Date",-12} {"Type",-8} {"Category",-18} {"Amount",12}  {"Description"}");
    Console.WriteLine(new string('-', 80));

    foreach (Transaction tx in transactions)
    {
        string signedAmount = tx.Type == TransactionType.Expense
            ? $"-{tx.Amount:N2}"
            : $"+{tx.Amount:N2}";

        Console.WriteLine($"{tx.Date:yyyy-MM-dd,-12} {tx.Type,-8} {Truncate(tx.Category, 18),-18} {signedAmount,12}  {tx.Description}");
    }

    return 0;
}

// Computes and displays a monthly financial summary with optional overall and category budget caps.
static async Task<int> HandleSummaryAsync(string[] args, IMonthlyReportService reportService)
{
    if (args.Length > 0 && IsHelpFlag(args[0]))
    {
        PrintSummaryUsage();
        return 0;
    }

    DateOnly today = DateOnly.FromDateTime(DateTime.Today);
    int year = today.Year;
    int month = today.Month;
    decimal? monthlyLimit = null;
    Dictionary<string, decimal> categoryLimits = new(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < args.Length; i++)
    {
        string flag = args[i].ToLowerInvariant();
        string? value = i + 1 < args.Length ? args[i + 1] : null;

        switch (flag)
        {
            case "--month" or "-m":
                RequireValue(flag, value);
                (year, month) = ParseYearMonth(value!);
                i++;
                break;

            case "--budget":
                RequireValue(flag, value);
                monthlyLimit = ParsePositiveDecimal(value!, "Budget limit");
                i++;
                break;

            case "--category-budget":
                // Expects "Category=Amount" (e.g., Dining=3000).
                RequireValue(flag, value);
                ParseCategoryBudgetEntry(value!, categoryLimits);
                i++;
                break;

            default:
                throw new ArgumentException($"Unknown option '{args[i]}' for 'summary'.");
        }
    }

    MonthlySummary summary = await reportService.GenerateMonthlySummaryAsync(
        year,
        month,
        monthlyLimit,
        categoryLimits.Count > 0 ? categoryLimits : null);

    Console.WriteLine($"Monthly Summary ({summary.Year:D4}-{summary.Month:D2})");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"  Total Income:   {summary.TotalIncome,12:N2}");
    Console.WriteLine($"  Total Expenses: {summary.TotalExpenses,12:N2}");
    Console.WriteLine($"  Net Balance:    {summary.NetBalance,12:N2}");

    Console.WriteLine("\nExpenses by Category:");
    if (summary.ExpenseByCategory.Count == 0)
    {
        Console.WriteLine("  No expenses recorded for this month.");
    }
    else
    {
        Console.WriteLine($"  {"Category",-20} {"Spent",12} {"Share",8} {"Count",7}");
        Console.WriteLine($"  {new string('-', 50)}");
        foreach (CategorySummary cat in summary.ExpenseByCategory)
        {
            Console.WriteLine($"  {Truncate(cat.Category, 20),-20} {cat.TotalAmount,12:N2} {cat.PercentageOfTotal,7:N1}% {cat.TransactionCount,7}");
        }
    }

    if (summary.BudgetAlerts.Count > 0)
    {
        Console.WriteLine("\nBudget Status:");
        foreach (BudgetAlert alert in summary.BudgetAlerts)
        {
            string status = alert.IsOverBudget ? "OVER BUDGET" : "WITHIN LIMIT";
            Console.WriteLine($"  [{status}] {alert.Scope}: Spent {alert.ActualSpent:N2} / Limit {alert.BudgetLimit:N2} (Remaining: {alert.RemainingAmount:N2})");
        }
    }

    return 0;
}

// Exports transactions for a given month to a CSV or JSON file on disk.
static async Task<int> HandleExportAsync(string[] args, ITransactionRepository repository)
{
    if (args.Length == 0 || IsHelpFlag(args[0]))
    {
        PrintExportUsage();
        return 0;
    }

    DateOnly today = DateOnly.FromDateTime(DateTime.Today);
    int year = today.Year;
    int month = today.Month;
    string format = "csv";
    string? outputPath = null;

    for (int i = 0; i < args.Length; i++)
    {
        string flag = args[i].ToLowerInvariant();
        string? value = i + 1 < args.Length ? args[i + 1] : null;

        switch (flag)
        {
            case "--month" or "-m":
                RequireValue(flag, value);
                (year, month) = ParseYearMonth(value!);
                i++;
                break;

            case "--format" or "-f":
                RequireValue(flag, value);
                format = value!.Trim().ToLowerInvariant();
                if (format is not ("csv" or "json"))
                {
                    throw new ArgumentException($"Unsupported export format '{value}'. Use 'csv' or 'json'.");
                }
                i++;
                break;

            case "--output" or "-o":
                RequireValue(flag, value);
                outputPath = value;
                i++;
                break;

            default:
                throw new ArgumentException($"Unknown option '{args[i]}' for 'export'.");
        }
    }

    string resolvedPath = string.IsNullOrWhiteSpace(outputPath)
        ? Path.Combine(Environment.CurrentDirectory, $"transactions-{year:D4}-{month:D2}.{format}")
        : outputPath;

    string? directory = Path.GetDirectoryName(Path.GetFullPath(resolvedPath));
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    IReadOnlyList<Transaction> transactions = await repository.GetByMonthAsync(year, month);

    if (format == "json")
    {
        JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        string jsonContent = JsonSerializer.Serialize(transactions, jsonOptions);
        await File.WriteAllTextAsync(resolvedPath, jsonContent, Encoding.UTF8);
    }
    else
    {
        string csvContent = BuildCsv(transactions);
        await File.WriteAllTextAsync(resolvedPath, csvContent, Encoding.UTF8);
    }

    Console.WriteLine($"Exported {transactions.Count} transaction(s) to '{resolvedPath}'.");
    return 0;
}

// Formats transactions into RFC-friendly CSV rows with proper field escaping.
static string BuildCsv(IReadOnlyList<Transaction> transactions)
{
    StringBuilder sb = new();
    sb.AppendLine("Id,Date,Type,Category,Amount,Description");

    foreach (Transaction tx in transactions)
    {
        sb.AppendLine(string.Join(",",
            EscapeCsv(tx.Id.ToString()),
            EscapeCsv(tx.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            EscapeCsv(tx.Type.ToString()),
            EscapeCsv(tx.Category),
            tx.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            EscapeCsv(tx.Description)));
    }

    return sb.ToString();
}

// Wraps CSV fields in quotes and doubles embedded quotes when needed.
static string EscapeCsv(string field)
{
    bool needsQuotes = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
    if (!needsQuotes)
    {
        return field;
    }

    return $"\"{field.Replace("\"", "\"\"")}\"";
}

// Parses "Category=Amount" pairs for per-category budget checks.
static void ParseCategoryBudgetEntry(string entry, IDictionary<string, decimal> target)
{
    int equalsIndex = entry.IndexOf('=');
    if (equalsIndex <= 0 || equalsIndex == entry.Length - 1)
    {
        throw new ArgumentException($"Invalid --category-budget '{entry}'. Expected format Category=Amount (e.g., Dining=2500).");
    }

    string category = entry[..equalsIndex].Trim();
    string amountText = entry[(equalsIndex + 1)..].Trim();
    decimal limit = ParsePositiveDecimal(amountText, $"Category budget for '{category}'");
    target[category] = limit;
}

// Parses a positive decimal value using invariant culture.
static decimal ParsePositiveDecimal(string value, string fieldName)
{
    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) || parsed <= 0m)
    {
        throw new ArgumentException($"{fieldName} must be a positive number (e.g., 250.50).");
    }

    return parsed;
}

// Parses a YYYY-MM string into year and month components.
static (int Year, int Month) ParseYearMonth(string value)
{
    if (!DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
    {
        throw new ArgumentException("Month must use YYYY-MM format (e.g., 2026-11).");
    }

    return (parsed.Year, parsed.Month);
}

// Parses a YYYY-MM-DD date string.
static DateOnly ParseDateOnly(string value)
{
    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
    {
        throw new ArgumentException("Date must use YYYY-MM-DD format (e.g., 2026-11-23).");
    }

    return parsed;
}

// Handles unrecognized top-level commands.
static int HandleUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return 1;
}

// Parses "expense" or "income" case-insensitively.
static TransactionType ParseTransactionType(string value) =>
    value.Trim().ToLowerInvariant() switch
    {
        "expense" or "exp" => TransactionType.Expense,
        "income" or "inc" => TransactionType.Income,
        _ => throw new ArgumentException($"Invalid transaction type '{value}'. Use 'expense' or 'income'.")
    };

// Ensures a flag that requires a value actually has one following it.
static void RequireValue(string flag, string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
    {
        throw new ArgumentException($"Option '{flag}' requires a value.");
    }
}

// Checks common help flags.
static bool IsHelpFlag(string arg) =>
    arg is "-h" or "--help" or "help";

// Truncates long labels cleanly so table columns stay aligned.
static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength ? value : $"{value[..(maxLength - 1)]}…";

// Prints top-level CLI commands.
static void PrintUsage()
{
    Console.WriteLine("ExpenseTracker CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project ExpenseTracker.Cli -- add --type <expense|income> --amount <number> --description <text> [--category <name>] [--date <YYYY-MM-DD>]");
    Console.WriteLine("  dotnet run --project ExpenseTracker.Cli -- list [--month <YYYY-MM>]");
    Console.WriteLine("  dotnet run --project ExpenseTracker.Cli -- summary [--month <YYYY-MM>] [--budget <number>] [--category-budget <Category=Amount>]");
    Console.WriteLine("  dotnet run --project ExpenseTracker.Cli -- export [--month <YYYY-MM>] [--format <csv|json>] [--output <path>]");
}

// Prints usage details for the `add` command.
static void PrintAddUsage()
{
    Console.WriteLine("Usage: add --type <expense|income> --amount <number> --description <text> [--category <name>] [--date <YYYY-MM-DD>]");
    Console.WriteLine("Example: add --type expense --amount 350.00 --description \"Supermarket groceries\" --date 2026-11-23");
}

// Prints usage details for the `list` command.
static void PrintListUsage()
{
    Console.WriteLine("Usage: list [--month <YYYY-MM>]");
    Console.WriteLine("Example: list --month 2026-11");
}

// Prints usage details for the `summary` command.
static void PrintSummaryUsage()
{
    Console.WriteLine("Usage: summary [--month <YYYY-MM>] [--budget <number>] [--category-budget <Category=Amount>]");
    Console.WriteLine("Example: summary --month 2026-11 --budget 15000 --category-budget Groceries=5000");
}

// Prints usage details for the `export` command.
static void PrintExportUsage()
{
    Console.WriteLine("Usage: export [--month <YYYY-MM>] [--format <csv|json>] [--output <path>]");
    Console.WriteLine("Example: export --month 2026-11 --format csv --output november-2026.csv");
}