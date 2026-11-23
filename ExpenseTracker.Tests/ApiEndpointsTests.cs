using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Tests;

// Integration tests for Minimal API endpoints using an isolated temporary JSON file per test run.
public sealed class ApiEndpointsTests : IClassFixture<ApiEndpointsTests.TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    // Matches the API's string-based enum serialization when reading/writing JSON in tests.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Configures the test host with a dedicated temporary storage path so tests never touch real user data.
    public ApiEndpointsTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Verifies creating a transaction via POST, listing it via GET, checking summary, and deleting it via DELETE.
    [Fact]
    public async Task TransactionsLifecycle_CreateListSummaryAndDelete_Succeeds()
    {
        _factory.ResetDataFile();

        CreateTransactionRequest createRequest = new(
            Type: TransactionType.Expense,
            Amount: 350.00m,
            Date: new DateOnly(2026, 11, 23),
            Description: "Supermarket groceries",
            Category: null);

        HttpResponseMessage postResponse = await _client.PostAsJsonAsync("/api/transactions", createRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        TransactionResponse? created = await postResponse.Content.ReadFromJsonAsync<TransactionResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Groceries", created.Category);
        Assert.Equal(350.00m, created.Amount);

        List<TransactionResponse>? listed = await _client.GetFromJsonAsync<List<TransactionResponse>>(
            "/api/transactions?year=2026&month=11",
            JsonOptions);
        Assert.NotNull(listed);
        Assert.Single(listed);
        Assert.Equal(created.Id, listed[0].Id);

        MonthlySummaryResponse? summary = await _client.GetFromJsonAsync<MonthlySummaryResponse>(
            "/api/summary?year=2026&month=11&budget=1000&categoryBudget=Groceries=500",
            JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(350.00m, summary.TotalExpenses);
        Assert.Equal(-350.00m, summary.NetBalance);
        Assert.Equal(2, summary.BudgetAlerts.Count);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage secondDeleteResponse = await _client.DeleteAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);
    }

    // Verifies edge cases such as non-positive amounts, month without year, and malformed categoryBudget.
    [Fact]
    public async Task InvalidInputs_ReturnBadRequest()
    {
        _factory.ResetDataFile();

        CreateTransactionRequest invalidAmountRequest = new(
            Type: TransactionType.Expense,
            Amount: -25m,
            Date: new DateOnly(2026, 11, 23),
            Description: "Invalid expense");

        HttpResponseMessage badCreate = await _client.PostAsJsonAsync("/api/transactions", invalidAmountRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, badCreate.StatusCode);

        HttpResponseMessage monthWithoutYear = await _client.GetAsync("/api/transactions?month=11");
        Assert.Equal(HttpStatusCode.BadRequest, monthWithoutYear.StatusCode);

        HttpResponseMessage badCategoryBudget = await _client.GetAsync(
            "/api/summary?year=2026&month=11&categoryBudget=MalformedEntry");
        Assert.Equal(HttpStatusCode.BadRequest, badCategoryBudget.StatusCode);
    }

    // Custom WebApplicationFactory isolating file storage inside a temporary directory.
    public sealed class TestApiFactory : WebApplicationFactory<ExpenseTracker.Api.Program>, IDisposable
    {
        private readonly string _tempDir;
        private readonly string _tempFile;

        public TestApiFactory()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ExpenseTrackerApiTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _tempFile = Path.Combine(_tempDir, "test-transactions.json");
        }

        // Clears the temporary JSON store between test cases.
        public void ResetDataFile()
        {
            File.WriteAllText(_tempFile, JsonSerializer.Serialize(Array.Empty<Transaction>()));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                Dictionary<string, string?> overrides = new()
                {
                    ["Storage:DataFilePath"] = _tempFile
                };
                configBuilder.AddInMemoryCollection(overrides);
            });

            builder.ConfigureServices(services =>
            {
                // Replace ITransactionRepository so each test run uses its own isolated temp file.
                ServiceDescriptor? existing = services.SingleOrDefault(d => d.ServiceType == typeof(ITransactionRepository));
                if (existing is not null)
                {
                    services.Remove(existing);
                }

                services.AddSingleton<ITransactionRepository>(_ => new JsonTransactionRepository(_tempFile));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }
}