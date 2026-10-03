using System.Text.Json;

namespace BankApi.Tests.Api;

public class SwaggerTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    [Fact]
    public async Task Document_contains_summaries_and_error_responses()
    {
        var json = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        var transfer = paths.GetProperty("/api/transfers").GetProperty("post");
        Assert.Equal("Transfers funds between two open accounts in the same currency.",
            transfer.GetProperty("summary").GetString());
        AssertResponses(transfer, "201", "400", "401", "404", "409", "422");

        var deleteClient = paths.GetProperty("/api/clients/{id}").GetProperty("delete");
        AssertResponses(deleteClient, "204", "401", "403", "404", "422");

        var listClients = paths.GetProperty("/api/clients").GetProperty("get");
        AssertResponses(listClients, "200", "400", "401");

        var login = paths.GetProperty("/api/auth/login").GetProperty("post");
        AssertResponses(login, "200", "400", "401");
    }

    private static void AssertResponses(JsonElement operation, params string[] expected)
    {
        var actual = operation.GetProperty("responses").EnumerateObject().Select(p => p.Name).Order();
        Assert.Equal(expected.Order(), actual);
    }
}
