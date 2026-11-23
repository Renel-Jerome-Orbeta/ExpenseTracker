# ExpenseTracker (.NET 10.0)

A local-first **Personal Finance & Expense Tracker** built in C# on **.NET 10.0 (LTS)** with a **CLI** and an **ASP.NET Core Minimal API**.

## Solution Structure

- `ExpenseTracker.Domain`: Core domain models (`Transaction`, `TransactionType`, `CategorySummary`, `BudgetAlert`, `MonthlySummary`), repository and service interfaces, validation rules, and exact `decimal` currency handling.
- `ExpenseTracker.Infrastructure`: JSON file persistence (`JsonTransactionRepository`) with atomic writes and `SemaphoreSlim` locking, deterministic rule-based categorization (`RuleBasedTransactionCategorizer`) using C# pattern matching and source-generated regexes, and LINQ reporting (`LinqMonthlyReportService`).
- `ExpenseTracker.Cli`: Console application supporting `add`, `list`, `summary`, and `export` (CSV/JSON) commands, wired with `Microsoft.Extensions.DependencyInjection`, `IConfiguration`, and `IOptions<BudgetOptions>`.
- `ExpenseTracker.Api`: ASP.NET Core Minimal API exposing transaction CRUD and monthly summary endpoints with validation and structured JSON error responses.
- `ExpenseTracker.Tests`: `xUnit` test suite covering domain validation, rule-based categorization, LINQ summaries and budget alerts, JSON persistence, and in-process Minimal API integration tests via `WebApplicationFactory`.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and Test

Run all commands from the solution root:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## CLI Usage

```powershell
# Add an expense (auto-categorized unless --category is passed)
dotnet run --project ExpenseTracker.Cli -- add --type expense --amount 420.00 --description "Supermarket groceries" --date 2026-11-23

# Add income
dotnet run --project ExpenseTracker.Cli -- add --type income --amount 25000.00 --description "Monthly salary payroll" --date 2026-11-23

# List transactions (all or filtered by YYYY-MM)
dotnet run --project ExpenseTracker.Cli -- list --month 2026-11

# Generate a monthly summary with optional overall and per-category budgets
dotnet run --project ExpenseTracker.Cli -- summary --month 2026-11 --budget 15000 --category-budget Groceries=5000

# Export monthly transactions to CSV or JSON
dotnet run --project ExpenseTracker.Cli -- export --month 2026-11 --format csv --output november-2026.csv
dotnet run --project ExpenseTracker.Cli -- export --month 2026-11 --format json --output november-2026.json
```

## Minimal API Endpoints

Start the API:

```powershell
dotnet run --project ExpenseTracker.Api
```

Available endpoints:

- `GET /api/transactions?year=2026&month=11&category=Groceries` — List transactions with optional year, month, and category filters.
- `POST /api/transactions` — Create a transaction (`type`, `amount`, `date`, `description`, optional `category`).
- `DELETE /api/transactions/{id}` — Delete a transaction by GUID (`204 No Content` or `404 Not Found`).
- `GET /api/summary?year=2026&month=11&budget=15000&categoryBudget=Groceries=5000` — Retrieve monthly totals, category percentages, and budget alerts.