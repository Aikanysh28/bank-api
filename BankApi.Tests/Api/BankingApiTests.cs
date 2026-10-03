using System.Net;
using System.Net.Http.Json;
using BankApi.Contracts;
using BankApi.Domain;

namespace BankApi.Tests.Api;

public class BankingApiTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    [Fact]
    public async Task Transfer_moves_money_between_accounts_of_different_clients()
    {
        var api = await factory.CreateAdminClientAsync();
        var from = await api.CreateAccountAsync((await api.CreateClientAsync()).Id, initialBalance: 1000);
        var to = await api.CreateAccountAsync((await api.CreateClientAsync()).Id);

        var transfer = await (await api.TransferAsync(from.Id, to.Id, 250.50m)).ReadAsync<TransferResponse>();

        Assert.Equal(250.50m, transfer.Amount);
        Assert.Equal(Currency.KGS, transfer.Currency);
        Assert.Equal(749.50m, (await api.GetAccountAsync(from)).Balance);
        Assert.Equal(250.50m, (await api.GetAccountAsync(to)).Balance);

        var history = await (await api.GetAsync($"/api/transfers?accountId={to.Id}"))
            .ReadAsync<PagedResult<TransferResponse>>();
        Assert.Equal(transfer.Id, Assert.Single(history.Items).Id);
    }

    [Fact]
    public async Task Transfer_moves_money_between_accounts_of_one_client()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var to = await api.CreateAccountAsync(client.Id);

        await (await api.TransferAsync(from.Id, to.Id, 100)).ReadAsync<TransferResponse>();

        Assert.Equal(0, (await api.GetAccountAsync(from)).Balance);
        Assert.Equal(100, (await api.GetAccountAsync(to)).Balance);
    }

    [Fact]
    public async Task Transfer_with_insufficient_funds_is_rejected_and_balances_are_kept()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var to = await api.CreateAccountAsync(client.Id);

        var problem = await (await api.TransferAsync(from.Id, to.Id, 100.01m))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        Assert.Contains("Insufficient funds", problem.Detail);
        Assert.Equal(100, (await api.GetAccountAsync(from)).Balance);
        Assert.Equal(0, (await api.GetAccountAsync(to)).Balance);
    }

    [Fact]
    public async Task Transfer_between_different_currencies_is_rejected()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, Currency.KGS, 100);
        var to = await api.CreateAccountAsync(client.Id, Currency.USD);

        var problem = await (await api.TransferAsync(from.Id, to.Id, 10))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        Assert.Contains("Cross-currency", problem.Detail);
        Assert.Equal(100, (await api.GetAccountAsync(from)).Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1.005)]
    public async Task Transfer_with_invalid_amount_is_rejected_with_400(double value)
    {
        var amount = (decimal)value;
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var to = await api.CreateAccountAsync(client.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await api.TransferAsync(from.Id, to.Id, amount)).StatusCode);
    }

    [Fact]
    public async Task Transfer_to_the_same_or_missing_account_is_rejected()
    {
        var api = await factory.CreateAdminClientAsync();
        var account = await api.CreateAccountAsync((await api.CreateClientAsync()).Id, initialBalance: 100);

        Assert.Equal(HttpStatusCode.BadRequest, (await api.TransferAsync(account.Id, account.Id, 10)).StatusCode);
        await (await api.TransferAsync(account.Id, int.MaxValue, 10)).ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blocked_card_cannot_be_used_until_unblocked()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var to = await api.CreateAccountAsync(client.Id);
        var card = await api.IssueCardAsync(from.Id);
        var cardUrl = $"/api/accounts/{from.Id}/cards/{card.Id}";

        var blocked = await (await api.PostAsync($"{cardUrl}/block")).ReadAsync<CardResponse>();
        Assert.Equal(CardStatus.Blocked, blocked.Status);
        await (await api.PostAsync($"{cardUrl}/block")).ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        var problem = await (await api.TransferAsync(from.Id, to.Id, 10, card.Id))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("blocked", problem.Detail);
        Assert.Equal(100, (await api.GetAccountAsync(from)).Balance);

        var unblocked = await (await api.PostAsync($"{cardUrl}/unblock")).ReadAsync<CardResponse>();
        Assert.Equal(CardStatus.Active, unblocked.Status);
        await (await api.PostAsync($"{cardUrl}/unblock")).ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        await (await api.TransferAsync(from.Id, to.Id, 10, card.Id)).ReadAsync<TransferResponse>();
        Assert.Equal(90, (await api.GetAccountAsync(from)).Balance);
    }

    [Fact]
    public async Task Card_of_another_account_cannot_be_used_for_transfer()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var from = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var to = await api.CreateAccountAsync(client.Id);
        var foreignCard = await api.IssueCardAsync(to.Id);

        await (await api.TransferAsync(from.Id, to.Id, 10, foreignCard.Id))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Card_is_issued_with_generated_number_and_default_holder()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync("Ivanov", "Ivan");
        var account = await api.CreateAccountAsync(client.Id);

        var card = await api.IssueCardAsync(account.Id);

        Assert.Matches("^[0-9]{16}$", card.Number);
        Assert.Equal("Ivan Ivanov", card.HolderName);
        Assert.Equal(CardStatus.Active, card.Status);

        var duplicate = await api.PostAsJsonAsync($"/api/accounts/{account.Id}/cards",
            new { number = card.Number, expiryDate = "2099-12-31", type = "Credit" });
        await duplicate.ReadProblemAsync(HttpStatusCode.Conflict);

        var expired = await api.PostAsJsonAsync($"/api/accounts/{account.Id}/cards",
            new { expiryDate = "2020-01-31", type = "Debit" });
        await expired.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Account_with_balance_cannot_be_closed()
    {
        var api = await factory.CreateAdminClientAsync();
        var account = await api.CreateAccountAsync((await api.CreateClientAsync()).Id, initialBalance: 5);

        var problem = await (await api.PostAsync($"/api/clients/{account.ClientId}/accounts/{account.Id}/close"))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        Assert.Contains("balance must be zero", problem.Detail);
        Assert.Equal(AccountStatus.Open, (await api.GetAccountAsync(account)).Status);
    }

    [Fact]
    public async Task Account_with_active_card_can_be_closed_only_after_the_card_is_blocked()
    {
        var api = await factory.CreateAdminClientAsync();
        var account = await api.CreateAccountAsync((await api.CreateClientAsync()).Id);
        var card = await api.IssueCardAsync(account.Id);
        var closeUrl = $"/api/clients/{account.ClientId}/accounts/{account.Id}/close";

        var problem = await (await api.PostAsync(closeUrl)).ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("active card", problem.Detail);

        await (await api.PostAsync($"/api/accounts/{account.Id}/cards/{card.Id}/block")).ReadAsync<CardResponse>();
        var closed = await (await api.PostAsync(closeUrl)).ReadAsync<AccountResponse>();

        Assert.Equal(AccountStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
    }

    [Fact]
    public async Task Closed_account_rejects_further_operations()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var closed = await api.CreateAccountAsync(client.Id);
        var open = await api.CreateAccountAsync(client.Id, initialBalance: 100);
        var card = await api.IssueCardAsync(closed.Id);
        var accountUrl = $"/api/clients/{client.Id}/accounts/{closed.Id}";
        await (await api.PostAsync($"/api/accounts/{closed.Id}/cards/{card.Id}/block")).ReadAsync<CardResponse>();
        await (await api.PostAsync($"{accountUrl}/close")).ReadAsync<AccountResponse>();

        var responses = new[]
        {
            await api.PostAsync($"{accountUrl}/close"),
            await api.PostAsJsonAsync($"{accountUrl}/deposit", new { amount = 10 }),
            await api.PutAsJsonAsync(accountUrl, new { name = "Renamed" }),
            await api.TransferAsync(open.Id, closed.Id, 10),
            await api.PostAsJsonAsync($"/api/accounts/{closed.Id}/cards", new { expiryDate = "2099-12-31", type = "Debit" }),
            await api.PostAsync($"/api/accounts/{closed.Id}/cards/{card.Id}/unblock")
        };

        foreach (var response in responses)
            await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);

        Assert.Equal(100, (await api.GetAccountAsync(open)).Balance);
    }

    [Fact]
    public async Task Deposit_and_withdraw_change_balance()
    {
        var api = await factory.CreateAdminClientAsync();
        var account = await api.CreateAccountAsync((await api.CreateClientAsync()).Id);
        var url = $"/api/clients/{account.ClientId}/accounts/{account.Id}";

        await (await api.PostAsJsonAsync($"{url}/deposit", new { amount = 100.75m })).ReadAsync<AccountResponse>();
        var afterWithdraw = await (await api.PostAsJsonAsync($"{url}/withdraw", new { amount = 0.75m }))
            .ReadAsync<AccountResponse>();
        Assert.Equal(100, afterWithdraw.Balance);

        await (await api.PostAsJsonAsync($"{url}/withdraw", new { amount = 500 }))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(100, (await api.GetAccountAsync(account)).Balance);
    }

    [Fact]
    public async Task Account_can_be_deleted_only_when_empty_and_without_history()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var withBalance = await api.CreateAccountAsync(client.Id, initialBalance: 10);
        var withHistory = await api.CreateAccountAsync(client.Id);
        var empty = await api.CreateAccountAsync(client.Id);
        await (await api.TransferAsync(withBalance.Id, withHistory.Id, 5)).ReadAsync<TransferResponse>();
        await (await api.TransferAsync(withHistory.Id, withBalance.Id, 5)).ReadAsync<TransferResponse>();

        await (await api.DeleteAsync($"/api/clients/{client.Id}/accounts/{withBalance.Id}"))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        await (await api.DeleteAsync($"/api/clients/{client.Id}/accounts/{withHistory.Id}"))
            .ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal(HttpStatusCode.NoContent,
            (await api.DeleteAsync($"/api/clients/{client.Id}/accounts/{empty.Id}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_account_number_is_rejected_with_409()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var account = await api.CreateAccountAsync(client.Id);

        var response = await api.PostAsJsonAsync($"/api/clients/{client.Id}/accounts",
            new { number = account.Number, currency = "USD" });

        await response.ReadProblemAsync(HttpStatusCode.Conflict);
    }
}
