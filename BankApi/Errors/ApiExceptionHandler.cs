using BankApi.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Errors;

/// <summary>Turns domain and persistence exceptions into RFC 7807 problem details.</summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const int SqliteConstraintUnique = 2067;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ApiException e => Problem(e.StatusCode, e.Title, e.Message),
            DomainException e => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violation", e.Message),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Conflict",
                "The resource was modified by another request. Retry the operation."),
            // Services check uniqueness before saving; this covers two requests racing past that check.
            DbUpdateException { InnerException: SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique } } =>
                Problem(StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),
            _ => null
        };

        // Anything else falls through to the default handler and becomes a 500.
        if (problem is null)
            return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
