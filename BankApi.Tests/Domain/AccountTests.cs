using BankApi.Domain;

namespace BankApi.Tests.Domain;

public class AccountTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static Account OpenAccount(decimal balance = 0, Currency currency = Currency.KGS) =>
        Account.Open(clientId: 1, "1000000000000001", name: null, currency, balance, Now);

    private static Card IssueCard(Account account, DateOnly? expiryDate = null) =>
        account.IssueCard("4000000000000002", "IVAN IVANOV", expiryDate ?? Today.AddYears(3), CardType.Debit, Today, Now);

    [Fact]
    public void Open_creates_open_account_with_initial_balance()
    {
        var account = OpenAccount(100);

        Assert.Equal(AccountStatus.Open, account.Status);
        Assert.Equal(100, account.Balance);
        Assert.Equal(Now, account.OpenedAt);
        Assert.Null(account.ClosedAt);
    }

    [Fact]
    public void Open_rejects_negative_initial_balance()
    {
        Assert.Throws<DomainException>(() => OpenAccount(-1));
    }

    [Fact]
    public void Deposit_and_withdraw_change_balance_and_version()
    {
        var account = OpenAccount(100);

        account.Deposit(50.25m);
        account.Withdraw(20);

        Assert.Equal(130.25m, account.Balance);
        Assert.Equal(2, account.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Deposit_and_withdraw_reject_non_positive_amount(int amount)
    {
        var account = OpenAccount(100);

        Assert.Throws<DomainException>(() => account.Deposit(amount));
        Assert.Throws<DomainException>(() => account.Withdraw(amount));
        Assert.Equal(100, account.Balance);
    }

    [Fact]
    public void Withdraw_rejects_amount_above_balance()
    {
        var account = OpenAccount(100);

        var error = Assert.Throws<DomainException>(() => account.Withdraw(100.01m));

        Assert.Contains("Insufficient funds", error.Message);
        Assert.Equal(100, account.Balance);
    }

    [Fact]
    public void TransferTo_moves_funds_between_accounts()
    {
        var from = OpenAccount(100);
        var to = OpenAccount(10);

        from.TransferTo(to, 40);

        Assert.Equal(60, from.Balance);
        Assert.Equal(50, to.Balance);
    }

    [Fact]
    public void TransferTo_rejects_insufficient_funds_and_keeps_balances()
    {
        var from = OpenAccount(100);
        var to = OpenAccount(10);

        Assert.Throws<DomainException>(() => from.TransferTo(to, 500));

        Assert.Equal(100, from.Balance);
        Assert.Equal(10, to.Balance);
    }

    [Fact]
    public void TransferTo_rejects_different_currencies()
    {
        var from = OpenAccount(100, Currency.KGS);
        var to = OpenAccount(0, Currency.USD);

        var error = Assert.Throws<DomainException>(() => from.TransferTo(to, 10));

        Assert.Contains("Cross-currency", error.Message);
        Assert.Equal(100, from.Balance);
        Assert.Equal(0, to.Balance);
    }

    [Fact]
    public void TransferTo_rejects_same_account()
    {
        var account = OpenAccount(100);

        Assert.Throws<DomainException>(() => account.TransferTo(account, 10));
    }

    [Fact]
    public void TransferTo_rejects_closed_source_or_destination()
    {
        var open = OpenAccount(100);
        var closed = OpenAccount();
        closed.Close(Today, Now);

        Assert.Throws<DomainException>(() => open.TransferTo(closed, 10));
        Assert.Throws<DomainException>(() => closed.TransferTo(open, 10));
        Assert.Equal(100, open.Balance);
    }

    [Fact]
    public void Close_succeeds_with_zero_balance_and_no_cards()
    {
        var account = OpenAccount();

        account.Close(Today, Now);

        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.Equal(Now, account.ClosedAt);
    }

    [Fact]
    public void Close_rejects_non_zero_balance()
    {
        var account = OpenAccount(0.01m);

        var error = Assert.Throws<DomainException>(() => account.Close(Today, Now));

        Assert.Contains("balance must be zero", error.Message);
        Assert.Equal(AccountStatus.Open, account.Status);
    }

    [Fact]
    public void Close_rejects_account_with_active_card()
    {
        var account = OpenAccount();
        IssueCard(account);

        var error = Assert.Throws<DomainException>(() => account.Close(Today, Now));

        Assert.Contains("active card", error.Message);
        Assert.Equal(AccountStatus.Open, account.Status);
    }

    [Fact]
    public void Close_allows_blocked_and_expired_cards()
    {
        var account = OpenAccount();
        IssueCard(account).Block();
        IssueCard(account, expiryDate: Today.AddDays(10));

        // The second card has expired by the time the account is closed.
        var later = Now.AddDays(30);
        account.Close(DateOnly.FromDateTime(later), later);

        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void Closed_account_rejects_all_operations()
    {
        var account = OpenAccount();
        account.Close(Today, Now);

        Assert.Throws<DomainException>(() => account.Deposit(10));
        Assert.Throws<DomainException>(() => account.Withdraw(10));
        Assert.Throws<DomainException>(() => account.Rename("New"));
        Assert.Throws<DomainException>(() => account.Close(Today, Now));
        Assert.Throws<DomainException>(() => IssueCard(account));
    }

    [Fact]
    public void EnsureCanBeDeleted_requires_zero_balance()
    {
        Assert.Throws<DomainException>(() => OpenAccount(5).EnsureCanBeDeleted());
        OpenAccount().EnsureCanBeDeleted();
    }
}
