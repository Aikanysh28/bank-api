using BankApi.Contracts;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/clients/{clientId:int}/accounts")]
public class AccountsController(AccountService accounts) : ControllerBase
{
    /// <summary>Returns the client's accounts, including closed ones.</summary>
    [HttpGet]
    public Task<IReadOnlyList<AccountResponse>> GetList(int clientId, CancellationToken ct) =>
        accounts.GetListAsync(clientId, ct);

    /// <summary>Returns an account of the client.</summary>
    [HttpGet("{id:int}")]
    public Task<AccountResponse> Get(int clientId, int id, CancellationToken ct) => accounts.GetAsync(clientId, id, ct);

    /// <summary>Opens an account. The number is generated when omitted.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Create(int clientId, AccountCreateRequest request, CancellationToken ct)
    {
        var account = await accounts.CreateAsync(clientId, request, ct);
        return CreatedAtAction(nameof(Get), new { clientId, id = account.Id }, account);
    }

    /// <summary>Renames an open account. Number, currency and balance cannot be edited.</summary>
    [HttpPut("{id:int}")]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public Task<AccountResponse> Update(int clientId, int id, AccountUpdateRequest request, CancellationToken ct) =>
        accounts.UpdateAsync(clientId, id, request, ct);

    /// <summary>Deletes an account with zero balance and no transfer history.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken ct)
    {
        await accounts.DeleteAsync(clientId, id, ct);
        return NoContent();
    }

    /// <summary>Closes an account with zero balance and no active cards.</summary>
    [HttpPost("{id:int}/close")]
    [ProducesErrors(StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity)]
    public Task<AccountResponse> Close(int clientId, int id, CancellationToken ct) => accounts.CloseAsync(clientId, id, ct);

    /// <summary>Adds funds to an open account.</summary>
    [HttpPost("{id:int}/deposit")]
    [ProducesErrors(StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity)]
    public Task<AccountResponse> Deposit(int clientId, int id, AmountRequest request, CancellationToken ct) =>
        accounts.DepositAsync(clientId, id, request.Amount, ct);

    /// <summary>Withdraws funds from an open account. Overdraft is not allowed.</summary>
    [HttpPost("{id:int}/withdraw")]
    [ProducesErrors(StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity)]
    public Task<AccountResponse> Withdraw(int clientId, int id, AmountRequest request, CancellationToken ct) =>
        accounts.WithdrawAsync(clientId, id, request.Amount, ct);
}
