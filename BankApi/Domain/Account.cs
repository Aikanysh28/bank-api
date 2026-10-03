namespace BankApi.Domain;

public class Account
{
    // Used by EF Core.
    private Account()
    {
    }

    public int Id { get; private set; }
    public int ClientId { get; private set; }
    public Client Client { get; private set; } = null!;
    public string Number { get; private set; } = null!;
    public string? Name { get; private set; }
    public Currency Currency { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    // Optimistic concurrency token: bumped on every balance or status change.
    public int Version { get; private set; }

    public List<Card> Cards { get; private set; } = [];

    public static Account Open(int clientId, string number, string? name, Currency currency, decimal initialBalance, DateTime now)
    {
        if (initialBalance < 0)
            throw new DomainException("Initial balance must not be negative.");

        return new Account
        {
            ClientId = clientId,
            Number = number,
            Name = name,
            Currency = currency,
            Balance = initialBalance,
            Status = AccountStatus.Open,
            OpenedAt = now
        };
    }

    public void Rename(string? name)
    {
        EnsureOpen();
        Name = name;
    }

    public void Deposit(decimal amount)
    {
        EnsurePositive(amount);
        EnsureOpen();

        Balance += amount;
        Version++;
    }

    public void Withdraw(decimal amount)
    {
        EnsurePositive(amount);
        EnsureOpen();

        if (Balance < amount)
            throw new DomainException($"Insufficient funds on account {Id}: available {Balance:0.00} {Currency}, required {amount:0.00} {Currency}.");

        Balance -= amount;
        Version++;
    }

    public void TransferTo(Account destination, decimal amount)
    {
        if (ReferenceEquals(this, destination))
            throw new DomainException("Source and destination accounts must differ.");

        EnsureOpen();
        destination.EnsureOpen();

        if (Currency != destination.Currency)
            throw new DomainException($"Cross-currency transfers are not supported: source account is in {Currency}, destination account is in {destination.Currency}.");

        Withdraw(amount);
        destination.Deposit(amount);
    }

    /// <summary>Issues a card to this account. The card is added to <see cref="Cards"/>.</summary>
    public Card IssueCard(string number, string holderName, DateOnly expiryDate, CardType type, DateOnly today, DateTime now)
    {
        EnsureOpen();

        var card = Card.Create(this, number, holderName, expiryDate, type, today, now);
        Cards.Add(card);
        return card;
    }

    /// <summary>Closes the account. <see cref="Cards"/> must be loaded.</summary>
    public void Close(DateOnly today, DateTime now)
    {
        EnsureOpen();

        if (Balance != 0)
            throw new DomainException($"Account balance must be zero to close it. Current balance: {Balance:0.00} {Currency}.");

        var activeCards = Cards.Count(c => c.IsActive(today));
        if (activeCards > 0)
            throw new DomainException($"Account has {activeCards} active card(s). Block or delete them before closing the account.");

        Status = AccountStatus.Closed;
        ClosedAt = now;
        Version++;
    }

    public void EnsureCanBeDeleted()
    {
        if (Balance != 0)
            throw new DomainException("Only an account with zero balance can be deleted.");
    }

    public void EnsureOpen()
    {
        if (Status != AccountStatus.Open)
            throw new DomainException($"Account {Id} is closed.");
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");
    }
}
