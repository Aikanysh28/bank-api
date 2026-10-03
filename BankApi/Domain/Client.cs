namespace BankApi.Domain;

public class Client
{
    public int Id { get; set; }
    public string LastName { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string Email { get; set; } = null!;
    public DateOnly BirthDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Phone> Phones { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
}
