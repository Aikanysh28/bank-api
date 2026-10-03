using BankApi.Contracts;
using BankApi.Data;
using BankApi.Domain;
using BankApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public class PhoneService(BankDbContext db, ClientService clients)
{
    public async Task<IReadOnlyList<PhoneResponse>> GetListAsync(int clientId, CancellationToken ct)
    {
        await clients.EnsureExistsAsync(clientId, ct);

        var phones = await db.Phones.AsNoTracking()
            .Where(p => p.ClientId == clientId)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        return phones.Select(p => p.ToResponse()).ToList();
    }

    public async Task<PhoneResponse> GetAsync(int clientId, int id, CancellationToken ct) =>
        (await FindAsync(clientId, id, ct)).ToResponse();

    public async Task<PhoneResponse> CreateAsync(int clientId, PhoneRequest request, CancellationToken ct)
    {
        await clients.EnsureExistsAsync(clientId, ct);
        await EnsureNumberIsFreeAsync(clientId, request.Number, exceptPhoneId: null, ct);

        var phone = new Phone { ClientId = clientId, Number = request.Number, Type = request.Type!.Value };
        db.Phones.Add(phone);
        await db.SaveChangesAsync(ct);
        return phone.ToResponse();
    }

    public async Task<PhoneResponse> UpdateAsync(int clientId, int id, PhoneRequest request, CancellationToken ct)
    {
        var phone = await FindAsync(clientId, id, ct);
        await EnsureNumberIsFreeAsync(clientId, request.Number, exceptPhoneId: id, ct);

        phone.Number = request.Number;
        phone.Type = request.Type!.Value;
        await db.SaveChangesAsync(ct);
        return phone.ToResponse();
    }

    public async Task DeleteAsync(int clientId, int id, CancellationToken ct)
    {
        var phone = await FindAsync(clientId, id, ct);
        db.Phones.Remove(phone);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Phone> FindAsync(int clientId, int id, CancellationToken ct) =>
        await db.Phones.FirstOrDefaultAsync(p => p.Id == id && p.ClientId == clientId, ct)
        ?? throw new NotFoundException($"Phone {id} of client {clientId} was not found.");

    private async Task EnsureNumberIsFreeAsync(int clientId, string number, int? exceptPhoneId, CancellationToken ct)
    {
        if (await db.Phones.AnyAsync(p => p.ClientId == clientId && p.Number == number && p.Id != exceptPhoneId, ct))
            throw new ConflictException($"Client {clientId} already has phone '{number}'.");
    }
}
