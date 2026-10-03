using BankApi.Contracts;
using BankApi.Data;
using BankApi.Domain;
using BankApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public class TransferService(BankDbContext db)
{
    public async Task<TransferResponse> TransferAsync(TransferRequest request, CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Where(a => a.Id == request.FromAccountId || a.Id == request.ToAccountId)
            .ToListAsync(ct);

        var from = accounts.FirstOrDefault(a => a.Id == request.FromAccountId)
            ?? throw new NotFoundException($"Source account {request.FromAccountId} was not found.");
        var to = accounts.FirstOrDefault(a => a.Id == request.ToAccountId)
            ?? throw new NotFoundException($"Destination account {request.ToAccountId} was not found.");

        var now = DateTime.UtcNow;

        if (request.CardId is { } cardId)
        {
            var card = await db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId, ct)
                ?? throw new NotFoundException($"Card {cardId} was not found.");
            card.EnsureCanDebit(from, DateOnly.FromDateTime(now));
        }

        from.TransferTo(to, request.Amount);

        var transfer = new Transfer
        {
            FromAccountId = from.Id,
            ToAccountId = to.Id,
            CardId = request.CardId,
            Amount = request.Amount,
            Currency = from.Currency,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = now
        };
        db.Transfers.Add(transfer);

        // Both balance updates and the transfer record are written in one transaction;
        // the Version concurrency token rejects the save if either account changed meanwhile.
        await db.SaveChangesAsync(ct);
        return transfer.ToResponse();
    }

    public async Task<PagedResult<TransferResponse>> GetListAsync(TransferQuery query, CancellationToken ct)
    {
        var transfers = db.Transfers.AsNoTracking();

        if (query.AccountId is { } accountId)
            transfers = transfers.Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId);

        var total = await transfers.CountAsync(ct);
        var items = await transfers
            .OrderByDescending(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TransferResponse>(
            items.Select(t => t.ToResponse()).ToList(), query.Page, query.PageSize, total);
    }
}
