using BankApi.Contracts;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/accounts/{accountId:int}/cards")]
public class CardsController(CardService cards) : ControllerBase
{
    /// <summary>Returns the account's cards.</summary>
    [HttpGet]
    public Task<IReadOnlyList<CardResponse>> GetList(int accountId, CancellationToken ct) =>
        cards.GetListAsync(accountId, ct);

    /// <summary>Returns a card of the account.</summary>
    [HttpGet("{id:int}")]
    public Task<CardResponse> Get(int accountId, int id, CancellationToken ct) => cards.GetAsync(accountId, id, ct);

    /// <summary>Issues a card to an open account. The number is generated when omitted.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CardResponse>> Create(int accountId, CardCreateRequest request, CancellationToken ct)
    {
        var card = await cards.CreateAsync(accountId, request, ct);
        return CreatedAtAction(nameof(Get), new { accountId, id = card.Id }, card);
    }

    /// <summary>Changes holder name, expiry date and type of a card of an open account.</summary>
    [HttpPut("{id:int}")]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public Task<CardResponse> Update(int accountId, int id, CardUpdateRequest request, CancellationToken ct) =>
        cards.UpdateAsync(accountId, id, request, ct);

    /// <summary>Deletes a card.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int accountId, int id, CancellationToken ct)
    {
        await cards.DeleteAsync(accountId, id, ct);
        return NoContent();
    }

    /// <summary>Blocks an active card.</summary>
    [HttpPost("{id:int}/block")]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public Task<CardResponse> Block(int accountId, int id, CancellationToken ct) => cards.BlockAsync(accountId, id, ct);

    /// <summary>Unblocks a blocked card of an open account.</summary>
    [HttpPost("{id:int}/unblock")]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public Task<CardResponse> Unblock(int accountId, int id, CancellationToken ct) => cards.UnblockAsync(accountId, id, ct);
}
