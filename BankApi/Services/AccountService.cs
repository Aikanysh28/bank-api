using BankApi.Contracts;
using BankApi.Data;
using BankApi.Domain;
using BankApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public class AccountService(BankDbContext db, ClientService clients)
{
    public async Task<IReadOnlyList<AccountResponse>> GetListAsync(int clientId, CancellationToken ct)
    {
        await clients.EnsureExistsAsync(clientId, ct);

        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        return accounts.Select(a => a.ToResponse()).ToList();
    }

    public async Task<AccountResponse> GetAsync(int clientId, int id, CancellationToken ct) =>
        (await FindAsync(clientId, id, ct)).ToResponse();

    public async Task<AccountResponse> CreateAsync(int clientId, AccountCreateRequest request, CancellationToken ct)
    {
        await clients.EnsureExistsAsync(clientId, ct);

        string number;
        if (request.Number is null)
        {
            number = await GenerateNumberAsync(ct);
        }
        else
        {
            number = request.Number;
            if (await db.Accounts.AnyAsync(a => a.Number == number, ct))
                throw new ConflictException($"Account with number '{number}' already exists.");
        }

        var account = Account.Open(
            clientId, number, NormalizeName(request.Name), request.Currency!.Value, request.InitialBalance, DateTime.UtcNow);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    public async Task<AccountResponse> UpdateAsync(int clientId, int id, AccountUpdateRequest request, CancellationToken ct)
    {
        var account = await FindAsync(clientId, id, ct);
        account.Rename(NormalizeName(request.Name));

        await db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    public async Task DeleteAsync(int clientId, int id, CancellationToken ct)
    {
        var account = await FindAsync(clientId, id, ct);
        account.EnsureCanBeDeleted();

        if (await db.Transfers.AnyAsync(t => t.FromAccountId == id || t.ToAccountId == id, ct))
            throw new DomainException("Account has transfer history and cannot be deleted. Close it instead.");

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AccountResponse> CloseAsync(int clientId, int id, CancellationToken ct)
    {
        var account = await db.Accounts.Include(a => a.Cards)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == clientId, ct)
            ?? throw NotFound(clientId, id);

        var now = DateTime.UtcNow;
        account.Close(DateOnly.FromDateTime(now), now);

        await db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    public async Task<AccountResponse> DepositAsync(int clientId, int id, decimal amount, CancellationToken ct)
    {
        var account = await FindAsync(clientId, id, ct);
        account.Deposit(amount);

        await db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    public async Task<AccountResponse> WithdrawAsync(int clientId, int id, decimal amount, CancellationToken ct)
    {
        var account = await FindAsync(clientId, id, ct);
        account.Withdraw(amount);

        await db.SaveChangesAsync(ct);
        return account.ToResponse();
    }

    private async Task<Account> FindAsync(int clientId, int id, CancellationToken ct) =>
        await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.ClientId == clientId, ct)
        ?? throw NotFound(clientId, id);

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        while (true)
        {
            var number = string.Create(16, Random.Shared, (span, random) =>
            {
                span[0] = (char)('1' + random.Next(9));
                for (var i = 1; i < span.Length; i++)
                    span[i] = (char)('0' + random.Next(10));
            });

            if (!await db.Accounts.AnyAsync(a => a.Number == number, ct))
                return number;
        }
    }

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static NotFoundException NotFound(int clientId, int id) =>
        new($"Account {id} of client {clientId} was not found.");
}
