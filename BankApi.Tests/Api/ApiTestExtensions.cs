using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankApi.Contracts;
using BankApi.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Tests.Api;

public static class ApiTestExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Asserts a successful response and reads its body.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    /// <summary>Asserts the status code of a failed response and returns its problem details.</summary>
    public static async Task<ProblemDetails> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonSerializer.Deserialize<ProblemDetails>(body, Json)!;
    }

    public static async Task<ClientResponse> CreateClientAsync(
        this HttpClient api, string lastName = "Ivanov", string firstName = "Ivan", string birthDate = "1990-05-17")
    {
        var response = await api.PostAsJsonAsync("/api/clients", new
        {
            lastName,
            firstName,
            email = $"{Guid.NewGuid():N}@example.com",
            birthDate
        });
        return await response.ReadAsync<ClientResponse>();
    }

    public static async Task<AccountResponse> CreateAccountAsync(
        this HttpClient api, int clientId, Currency currency = Currency.KGS, decimal initialBalance = 0)
    {
        var response = await api.PostAsJsonAsync($"/api/clients/{clientId}/accounts",
            new { currency = currency.ToString(), initialBalance });
        return await response.ReadAsync<AccountResponse>();
    }

    public static async Task<CardResponse> IssueCardAsync(this HttpClient api, int accountId)
    {
        var response = await api.PostAsJsonAsync($"/api/accounts/{accountId}/cards",
            new { expiryDate = "2099-12-31", type = "Debit" });
        return await response.ReadAsync<CardResponse>();
    }

    public static async Task<AccountResponse> GetAccountAsync(this HttpClient api, AccountResponse account) =>
        await (await api.GetAsync($"/api/clients/{account.ClientId}/accounts/{account.Id}")).ReadAsync<AccountResponse>();

    public static Task<HttpResponseMessage> TransferAsync(
        this HttpClient api, int fromAccountId, int toAccountId, decimal amount, int? cardId = null) =>
        api.PostAsJsonAsync("/api/transfers", new { fromAccountId, toAccountId, amount, cardId });

    public static Task<HttpResponseMessage> PostAsync(this HttpClient api, string url) => api.PostAsync(url, content: null);
}
