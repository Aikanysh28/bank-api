namespace BankApi.Domain;

public class Transfer
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public int? CardId { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
