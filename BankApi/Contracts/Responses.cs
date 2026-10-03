using BankApi.Domain;

namespace BankApi.Contracts;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record PhoneResponse(int Id, int ClientId, string Number, PhoneType Type);

public record ClientResponse(
    int Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    string Email,
    DateOnly BirthDate,
    DateTime CreatedAt,
    IReadOnlyList<PhoneResponse> Phones);

public record AccountResponse(
    int Id,
    int ClientId,
    string Number,
    string? Name,
    Currency Currency,
    decimal Balance,
    AccountStatus Status,
    DateTime OpenedAt,
    DateTime? ClosedAt);

public record CardResponse(
    int Id,
    int AccountId,
    string Number,
    string HolderName,
    DateOnly ExpiryDate,
    CardType Type,
    CardStatus Status,
    bool IsExpired);

public record TransferResponse(
    int Id,
    int FromAccountId,
    int ToAccountId,
    int? CardId,
    decimal Amount,
    Currency Currency,
    string? Description,
    DateTime CreatedAt);

public record UserResponse(int Id, string Username, UserRole Role);

public record TokenResponse(string AccessToken, DateTime ExpiresAt, string Username, UserRole Role);

public static class Mapping
{
    public static PhoneResponse ToResponse(this Phone p) => new(p.Id, p.ClientId, p.Number, p.Type);

    public static ClientResponse ToResponse(this Client c) => new(
        c.Id, c.LastName, c.FirstName, c.MiddleName, c.Email, c.BirthDate, c.CreatedAt,
        c.Phones.OrderBy(p => p.Id).Select(p => p.ToResponse()).ToList());

    public static AccountResponse ToResponse(this Account a) => new(
        a.Id, a.ClientId, a.Number, a.Name, a.Currency, a.Balance, a.Status, a.OpenedAt, a.ClosedAt);

    public static CardResponse ToResponse(this Card c) => new(
        c.Id, c.AccountId, c.Number, c.HolderName, c.ExpiryDate, c.Type, c.Status,
        c.IsExpired(DateOnly.FromDateTime(DateTime.UtcNow)));

    public static TransferResponse ToResponse(this Transfer t) => new(
        t.Id, t.FromAccountId, t.ToAccountId, t.CardId, t.Amount, t.Currency, t.Description, t.CreatedAt);

    public static UserResponse ToResponse(this User u) => new(u.Id, u.Username, u.Role);
}
