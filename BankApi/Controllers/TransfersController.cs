using BankApi.Contracts;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController(TransferService transfers) : ControllerBase
{
    /// <summary>Transfers funds between two open accounts in the same currency.</summary>
    /// <remarks>
    /// The accounts may belong to the same client or to different clients. When <c>cardId</c> is set,
    /// the card must be issued to the source account, must not be blocked and must not be expired.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferResponse>> Create(TransferRequest request, CancellationToken ct)
    {
        var transfer = await transfers.TransferAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, transfer);
    }

    /// <summary>Returns transfer history, newest first.</summary>
    [HttpGet]
    public Task<PagedResult<TransferResponse>> GetList([FromQuery] TransferQuery query, CancellationToken ct) =>
        transfers.GetListAsync(query, ct);
}
