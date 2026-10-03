using BankApi.Contracts;
using BankApi.Data;
using BankApi.Domain;
using BankApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public class CardService(BankDbContext db)
{
    public async Task<IReadOnlyList<CardResponse>> GetListAsync(int accountId, CancellationToken ct)
    {
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, ct))
            throw AccountNotFound(accountId);

        var cards = await db.Cards.AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        return cards.Select(c => c.ToResponse()).ToList();
    }

    public async Task<CardResponse> GetAsync(int accountId, int id, CancellationToken ct) =>
        (await FindAsync(accountId, id, ct)).ToResponse();

    public async Task<CardResponse> CreateAsync(int accountId, CardCreateRequest request, CancellationToken ct)
    {
        var account = await db.Accounts.Include(a => a.Client).FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw AccountNotFound(accountId);

        string number;
        if (request.Number is null)
        {
            number = await GenerateNumberAsync(ct);
        }
        else
        {
            number = request.Number;
            if (await db.Cards.AnyAsync(c => c.Number == number, ct))
                throw new ConflictException($"Card with number '{number}' already exists.");
        }

        var holderName = string.IsNullOrWhiteSpace(request.HolderName)
            ? $"{account.Client.FirstName} {account.Client.LastName}"
            : request.HolderName.Trim();

        var now = DateTime.UtcNow;
        var card = account.IssueCard(
            number, holderName, request.ExpiryDate!.Value, request.Type!.Value, DateOnly.FromDateTime(now), now);

        await db.SaveChangesAsync(ct);
        return card.ToResponse();
    }

    public async Task<CardResponse> UpdateAsync(int accountId, int id, CardUpdateRequest request, CancellationToken ct)
    {
        var card = await FindAsync(accountId, id, ct, includeAccount: true);
        card.Update(request.HolderName.Trim(), request.ExpiryDate!.Value, request.Type!.Value, Today());

        await db.SaveChangesAsync(ct);
        return card.ToResponse();
    }

    public async Task DeleteAsync(int accountId, int id, CancellationToken ct)
    {
        var card = await FindAsync(accountId, id, ct);
        db.Cards.Remove(card);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CardResponse> BlockAsync(int accountId, int id, CancellationToken ct)
    {
        var card = await FindAsync(accountId, id, ct);
        card.Block();

        await db.SaveChangesAsync(ct);
        return card.ToResponse();
    }

    public async Task<CardResponse> UnblockAsync(int accountId, int id, CancellationToken ct)
    {
        var card = await FindAsync(accountId, id, ct, includeAccount: true);
        card.Unblock();

        await db.SaveChangesAsync(ct);
        return card.ToResponse();
    }

    private async Task<Card> FindAsync(int accountId, int id, CancellationToken ct, bool includeAccount = false)
    {
        IQueryable<Card> query = db.Cards;
        if (includeAccount)
            query = query.Include(c => c.Account);

        return await query.FirstOrDefaultAsync(c => c.Id == id && c.AccountId == accountId, ct)
            ?? throw new NotFoundException($"Card {id} of account {accountId} was not found.");
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        while (true)
        {
            var number = GenerateLuhnNumber();
            if (!await db.Cards.AnyAsync(c => c.Number == number, ct))
                return number;
        }
    }

    private static string GenerateLuhnNumber()
    {
        Span<char> digits = stackalloc char[16];
        digits[0] = '4';
        for (var i = 1; i < 15; i++)
            digits[i] = (char)('0' + Random.Shared.Next(10));

        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var digit = digits[i] - '0';
            // Counting from the check digit, every second digit is doubled.
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
        }

        digits[15] = (char)('0' + (10 - sum % 10) % 10);
        return new string(digits);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static NotFoundException AccountNotFound(int accountId) => new($"Account {accountId} was not found.");
}
