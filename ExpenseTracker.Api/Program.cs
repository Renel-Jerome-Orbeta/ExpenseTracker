using System.Globalization;
using System.Text.Json.Serialization;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Api;

// Entry point for the local ExpenseTracker Minimal API (no Docker or AI dependencies).
public partial class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Serialize enums as readable strings (e.g., "Expense", "Income") in JSON payloads.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Register shared domain, reporting, and infrastructure services.
        string? dataFilePath = builder.Configuration["Storage:DataFilePath"];
        builder.Services.AddSingleton<ITransactionRepository>(_ => new JsonTransactionRepository(dataFilePath));
        builder.Services.AddSingleton<ITransactionCategorizer, RuleBasedTransactionCategorizer>();
        builder.Services.AddSingleton<IMonthlyReportService, LinqMonthlyReportService>();

        WebApplication app = builder.Build();

        // Catch unexpected server errors cleanly and return a consistent JSON error payload.
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred while processing the request." });
            });
        });

        // Return clean JSON responses when request body or query binding fails.
        app.UseStatusCodePages(async statusContext =>
        {
            HttpResponse response = statusContext.HttpContext.Response;
            if (response.HasStarted)
            {
                return;
            }

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                response.ContentType = "application/json";
                await response.WriteAsJsonAsync(new { error = "Invalid request parameters or JSON payload." });
            }
        });

        RouteGroupBuilder transactionsGroup = app.MapGroup("/api/transactions");

        // GET /api/transactions?year=2026&month=11&category=Groceries
        transactionsGroup.MapGet("/", async (
            int? year,
            int? month,
            string? category,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (month.HasValue && month.Value is < 1 or > 12)
            {
                return Results.BadRequest(new { error = "Month must be between 1 and 12." });
            }

            if (month.HasValue && !year.HasValue)
            {
                return Results.BadRequest(new { error = "Year is required when filtering by month." });
            }

            IReadOnlyList<Transaction> transactions = year.HasValue && month.HasValue
                ? await repository.GetByMonthAsync(year.Value, month.Value, cancellationToken)
                : await repository.GetAllAsync(cancellationToken);

            // Apply optional case-insensitive category filter after loading records.
            if (!string.IsNullOrWhiteSpace(category))
            {
                string targetCategory = category.Trim();
                transactions = transactions
                    .Where(t => string.Equals(t.Category, targetCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            List<TransactionResponse> response = transactions
                .Select(TransactionResponse.FromDomain)
                .ToList();

            return Results.Ok(response);
        });

        // POST /api/transactions
        transactionsGroup.MapPost("/", async (
            CreateTransactionRequest request,
            ITransactionRepository repository,
            ITransactionCategorizer categorizer,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.IsDefined(request.Type))
            {
                return Results.BadRequest(new { error = "Transaction type must be Expense or Income." });
            }

            try
            {
                string resolvedCategory = categorizer.Categorize(
                    request.Description,
                    request.Type,
                    request.Category);

                Transaction created = Transaction.Create(
                    request.Type,
                    request.Amount,
                    request.Date,
                    resolvedCategory,
                    request.Description);

                await repository.AddAsync(created, cancellationToken);

                TransactionResponse response = TransactionResponse.FromDomain(created);
                return Results.Created($"/api/transactions/{created.Id}", response);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // DELETE /api/transactions/{id:guid}
        transactionsGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            bool deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound(new { error = $"Transaction '{id}' was not found." });
        });

        // GET /api/summary?year=2026&month=11&budget=15000&categoryBudget=Groceries=5000,Dining=2500
        app.MapGet("/api/summary", async (
            int? year,
            int? month,
            decimal? budget,
            string? categoryBudget,
            IMonthlyReportService reportService,
            CancellationToken cancellationToken) =>
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            int targetYear = year ?? today.Year;
            int targetMonth = month ?? today.Month;

            if (targetMonth is < 1 or > 12)
            {
                return Results.BadRequest(new { error = "Month must be between 1 and 12." });
            }

            if (budget.HasValue && budget.Value <= 0m)
            {
                return Results.BadRequest(new { error = "Budget limit must be greater than zero." });
            }

            Dictionary<string, decimal>? parsedCategoryBudgets;
            try
            {
                parsedCategoryBudgets = ParseCategoryBudgets(categoryBudget);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            MonthlySummary summary = await reportService.GenerateMonthlySummaryAsync(
                targetYear,
                targetMonth,
                budget,
                parsedCategoryBudgets,
                cancellationToken);

            return Results.Ok(MonthlySummaryResponse.FromDomain(summary));
        });

        app.Run();
    }

    // Parses comma-separated "Category=Amount" pairs (e.g., "Groceries=5000,Dining=2500").
    private static Dictionary<string, decimal>? ParseCategoryBudgets(string? rawCategoryBudgets)
    {
        if (string.IsNullOrWhiteSpace(rawCategoryBudgets))
        {
            return null;
        }

        Dictionary<string, decimal> result = new(StringComparer.OrdinalIgnoreCase);
        string[] entries = rawCategoryBudgets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string entry in entries)
        {
            int equalsIndex = entry.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == entry.Length - 1)
            {
                throw new ArgumentException($"Invalid category budget '{entry}'. Use 'Category=Amount'.");
            }

            string category = entry[..equalsIndex].Trim();
            string amountText = entry[(equalsIndex + 1)..].Trim();

            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal limit) || limit <= 0m)
            {
                throw new ArgumentException($"Category budget for '{category}' must be a positive number.");
            }

            result[category] = limit;
        }

        return result;
    }
}