namespace BankApi.Errors;

public abstract class ApiException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

/// <summary>Requested resource does not exist (404).</summary>
public class NotFoundException(string message)
    : ApiException(message, StatusCodes.Status404NotFound, "Resource not found");

/// <summary>Uniqueness violation (409).</summary>
public class ConflictException(string message)
    : ApiException(message, StatusCodes.Status409Conflict, "Conflict");
