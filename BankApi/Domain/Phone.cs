namespace BankApi.Domain;

public class Phone
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string Number { get; set; } = null!;
    public PhoneType Type { get; set; }
}
