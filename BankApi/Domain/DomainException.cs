namespace BankApi.Domain;

/// <summary>A business rule was violated. The message is safe to show to the API client.</summary>
public class DomainException(string message) : Exception(message);
