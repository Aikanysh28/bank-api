namespace BankApi.Domain;

public class Card
{
    // Used by EF Core.
    private Card()
    {
    }

    public int Id { get; private set; }
    public int AccountId { get; private set; }
    public Account Account { get; private set; } = null!;
    public string Number { get; private set; } = null!;
    public string HolderName { get; private set; } = null!;
    public DateOnly ExpiryDate { get; private set; }
    public CardType Type { get; private set; }
    public CardStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Cards are issued through Account.IssueCard so that the account's state is checked.
    internal static Card Create(Account account, string number, string holderName, DateOnly expiryDate, CardType type, DateOnly today, DateTime now)
    {
        EnsureNotExpired(expiryDate, today);

        return new Card
        {
            Account = account,
            AccountId = account.Id,
            Number = number,
            HolderName = holderName,
            ExpiryDate = expiryDate,
            Type = type,
            Status = CardStatus.Active,
            CreatedAt = now
        };
    }

    public bool IsExpired(DateOnly today) => ExpiryDate < today;

    public bool IsActive(DateOnly today) => Status == CardStatus.Active && !IsExpired(today);

    /// <summary>Changes card details. <see cref="Account"/> must be loaded.</summary>
    public void Update(string holderName, DateOnly expiryDate, CardType type, DateOnly today)
    {
        Account.EnsureOpen();
        EnsureNotExpired(expiryDate, today);

        HolderName = holderName;
        ExpiryDate = expiryDate;
        Type = type;
    }

    public void Block()
    {
        if (Status == CardStatus.Blocked)
            throw new DomainException($"Card {Id} is already blocked.");

        Status = CardStatus.Blocked;
    }

    /// <summary>Unblocks the card. <see cref="Account"/> must be loaded.</summary>
    public void Unblock()
    {
        if (Status != CardStatus.Blocked)
            throw new DomainException($"Card {Id} is not blocked.");

        // A closed account must not get an active card back.
        Account.EnsureOpen();

        Status = CardStatus.Active;
    }

    /// <summary>Checks that the card can be used to debit the given account.</summary>
    public void EnsureCanDebit(Account account, DateOnly today)
    {
        if (AccountId != account.Id)
            throw new DomainException($"Card {Id} is not issued to the source account {account.Id}.");

        if (Status == CardStatus.Blocked)
            throw new DomainException($"Card {Id} is blocked.");

        if (IsExpired(today))
            throw new DomainException($"Card {Id} has expired.");
    }

    private static void EnsureNotExpired(DateOnly expiryDate, DateOnly today)
    {
        if (expiryDate < today)
            throw new DomainException("Card expiry date must not be in the past.");
    }
}
