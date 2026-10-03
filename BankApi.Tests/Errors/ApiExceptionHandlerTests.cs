using BankApi.Domain;
using BankApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Tests.Errors;

public class ApiExceptionHandlerTests
{
    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }

    private static async Task<(bool Handled, HttpContext Context, RecordingProblemDetailsService Writer)> HandleAsync(Exception exception)
    {
        var writer = new RecordingProblemDetailsService();
        var context = new DefaultHttpContext();
        var handled = await new ApiExceptionHandler(writer).TryHandleAsync(context, exception, CancellationToken.None);
        return (handled, context, writer);
    }

    [Fact]
    public async Task Unique_constraint_violation_becomes_409()
    {
        var exception = new DbUpdateException("save failed",
            new SqliteException("UNIQUE constraint failed: Clients.Email", 19, 2067));

        var (handled, context, writer) = await HandleAsync(exception);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        // The database message with table and column names must not leak to the client.
        Assert.DoesNotContain("Clients", writer.Written!.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Other_database_errors_are_not_handled()
    {
        var exception = new DbUpdateException("save failed",
            new SqliteException("FOREIGN KEY constraint failed", 19, 787));

        var (handled, _, writer) = await HandleAsync(exception);

        Assert.False(handled);
        Assert.Null(writer.Written);
    }

    [Fact]
    public async Task Concurrency_conflict_becomes_409()
    {
        var (handled, context, _) = await HandleAsync(new DbUpdateConcurrencyException("stale"));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task Domain_exception_becomes_422_with_its_message()
    {
        var (handled, context, writer) = await HandleAsync(new DomainException("Card 1 is blocked."));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        Assert.Equal("Card 1 is blocked.", writer.Written!.ProblemDetails.Detail);
    }

    [Fact]
    public async Task Not_found_exception_becomes_404()
    {
        var (handled, context, _) = await HandleAsync(new NotFoundException("Client 5 was not found."));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_exception_is_left_to_the_default_handler()
    {
        var (handled, _, _) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.False(handled);
    }
}
