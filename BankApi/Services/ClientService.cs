using BankApi.Contracts;
using BankApi.Data;
using BankApi.Domain;
using BankApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public class ClientService(BankDbContext db)
{
    public async Task<PagedResult<ClientResponse>> GetListAsync(ClientFilter filter, CancellationToken ct)
    {
        var query = db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.LastName, pattern) ||
                EF.Functions.Like(c.FirstName, pattern) ||
                (c.MiddleName != null && EF.Functions.Like(c.MiddleName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            var pattern = $"%{filter.Email.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Email, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.Phone))
        {
            var phone = filter.Phone.Trim();
            query = query.Where(c => c.Phones.Any(p => p.Number.Contains(phone)));
        }

        if (filter.BirthDateFrom is { } from)
            query = query.Where(c => c.BirthDate >= from);

        if (filter.BirthDateTo is { } to)
            query = query.Where(c => c.BirthDate <= to);

        var total = await query.CountAsync(ct);

        var ordered = filter.SortBy switch
        {
            ClientSortBy.Email => filter.Desc ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            ClientSortBy.BirthDate => filter.Desc ? query.OrderByDescending(c => c.BirthDate) : query.OrderBy(c => c.BirthDate),
            ClientSortBy.CreatedAt => filter.Desc ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
            _ => filter.Desc
                ? query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName)
                : query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
        };

        var clients = await ordered
            .ThenBy(c => c.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(c => c.Phones)
            .ToListAsync(ct);

        return new PagedResult<ClientResponse>(
            clients.Select(c => c.ToResponse()).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<ClientResponse> GetAsync(int id, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().Include(c => c.Phones).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFound(id);
        return client.ToResponse();
    }

    public async Task<ClientResponse> CreateAsync(ClientRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        await EnsureEmailIsFreeAsync(email, exceptClientId: null, ct);

        var client = new Client
        {
            LastName = request.LastName.Trim(),
            FirstName = request.FirstName.Trim(),
            MiddleName = NormalizeOptional(request.MiddleName),
            Email = email,
            BirthDate = request.BirthDate!.Value,
            CreatedAt = DateTime.UtcNow
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return client.ToResponse();
    }

    public async Task<ClientResponse> UpdateAsync(int id, ClientRequest request, CancellationToken ct)
    {
        var client = await db.Clients.Include(c => c.Phones).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFound(id);

        var email = NormalizeEmail(request.Email);
        await EnsureEmailIsFreeAsync(email, exceptClientId: id, ct);

        client.LastName = request.LastName.Trim();
        client.FirstName = request.FirstName.Trim();
        client.MiddleName = NormalizeOptional(request.MiddleName);
        client.Email = email;
        client.BirthDate = request.BirthDate!.Value;

        await db.SaveChangesAsync(ct);
        return client.ToResponse();
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFound(id);

        if (await db.Accounts.AnyAsync(a => a.ClientId == id, ct))
            throw new DomainException("Client has accounts. Delete the accounts before deleting the client.");

        db.Clients.Remove(client);
        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureExistsAsync(int id, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == id, ct))
            throw NotFound(id);
    }

    private async Task EnsureEmailIsFreeAsync(string email, int? exceptClientId, CancellationToken ct)
    {
        if (await db.Clients.AnyAsync(c => c.Email == email && c.Id != exceptClientId, ct))
            throw new ConflictException($"Client with email '{email}' already exists.");
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotFoundException NotFound(int id) => new($"Client {id} was not found.");
}
