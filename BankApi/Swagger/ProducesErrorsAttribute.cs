namespace BankApi.Swagger;

/// <summary>
/// Declares error status codes an action can return in addition to the ones inferred by
/// <see cref="ErrorResponsesOperationFilter"/>. Affects only the OpenAPI document.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ProducesErrorsAttribute(params int[] statusCodes) : Attribute
{
    public IReadOnlyList<int> StatusCodes { get; } = statusCodes;
}
