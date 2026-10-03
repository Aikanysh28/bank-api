using BankApi.Domain;

namespace BankApi.Tests.Domain;

public class CardTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static Account OpenAccount() =>
        Account.Open(clientId: 1, "1000000000000001", name: null, Currency.KGS, 0, Now);

    private static Card IssueCard(Account account, DateOnly? expiryDate = null) =>
        account.IssueCard("4000000000000002", "IVAN IVANOV", expiryDate ?? Today.AddYears(3), CardType.Debit, Today, Now);

    [Fact]
    public void Issued_card_is_active_and_attached_to_account()
    {
        var account = OpenAccount();

        var card = IssueCard(account);

        Assert.Equal(CardStatus.Active, card.Status);
        Assert.True(card.IsActive(Today));
        Assert.Same(card, Assert.Single(account.Cards));
    }

    [Fact]
    public void Issue_rejects_expiry_date_in_the_past()
    {
        var account = OpenAccount();

        Assert.Throws<DomainException>(() => IssueCard(account, Today.AddDays(-1)));
        Assert.Empty(account.Cards);
    }

    [Fact]
    public void Card_expiring_today_is_still_valid()
    {
        var card = IssueCard(OpenAccount(), Today);

        Assert.False(card.IsExpired(Today));
        Assert.True(card.IsExpired(Today.AddDays(1)));
    }

    [Fact]
    public void Block_and_unblock_switch_status()
    {
        var card = IssueCard(OpenAccount());

        card.Block();
        Assert.Equal(CardStatus.Blocked, card.Status);
        Assert.False(card.IsActive(Today));

        card.Unblock();
        Assert.Equal(CardStatus.Active, card.Status);
    }

    [Fact]
    public void Block_rejects_already_blocked_card()
    {
        var card = IssueCard(OpenAccount());
        card.Block();

        Assert.Throws<DomainException>(card.Block);
    }

    [Fact]
    public void Unblock_rejects_card_that_is_not_blocked()
    {
        var card = IssueCard(OpenAccount());

        Assert.Throws<DomainException>(card.Unblock);
    }

    [Fact]
    public void Unblock_rejects_card_of_closed_account()
    {
        var account = OpenAccount();
        var card = IssueCard(account);
        card.Block();
        account.Close(Today, Now);

        Assert.Throws<DomainException>(card.Unblock);
        Assert.Equal(CardStatus.Blocked, card.Status);
    }

    [Fact]
    public void Update_rejects_expired_date_and_closed_account()
    {
        var account = OpenAccount();
        var card = IssueCard(account);

        Assert.Throws<DomainException>(() => card.Update("NEW NAME", Today.AddDays(-1), CardType.Credit, Today));

        card.Update("NEW NAME", Today.AddYears(1), CardType.Credit, Today);
        Assert.Equal("NEW NAME", card.HolderName);
        Assert.Equal(CardType.Credit, card.Type);

        card.Block();
        account.Close(Today, Now);
        Assert.Throws<DomainException>(() => card.Update("OTHER", Today.AddYears(1), CardType.Debit, Today));
    }

    [Fact]
    public void EnsureCanDebit_rejects_blocked_and_expired_card()
    {
        var account = OpenAccount();
        var card = IssueCard(account, Today.AddDays(5));

        card.EnsureCanDebit(account, Today);

        var expired = Assert.Throws<DomainException>(() => card.EnsureCanDebit(account, Today.AddDays(6)));
        Assert.Contains("expired", expired.Message);

        card.Block();
        var blocked = Assert.Throws<DomainException>(() => card.EnsureCanDebit(account, Today));
        Assert.Contains("blocked", blocked.Message);
    }
}
