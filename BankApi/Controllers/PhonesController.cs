using BankApi.Contracts;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/clients/{clientId:int}/phones")]
public class PhonesController(PhoneService phones) : ControllerBase
{
    /// <summary>Returns the client's phones.</summary>
    [HttpGet]
    public Task<IReadOnlyList<PhoneResponse>> GetList(int clientId, CancellationToken ct) =>
        phones.GetListAsync(clientId, ct);

    /// <summary>Returns a phone of the client.</summary>
    [HttpGet("{id:int}")]
    public Task<PhoneResponse> Get(int clientId, int id, CancellationToken ct) => phones.GetAsync(clientId, id, ct);

    /// <summary>Adds a phone to the client. The number must be unique within the client.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PhoneResponse>> Create(int clientId, PhoneRequest request, CancellationToken ct)
    {
        var phone = await phones.CreateAsync(clientId, request, ct);
        return CreatedAtAction(nameof(Get), new { clientId, id = phone.Id }, phone);
    }

    /// <summary>Replaces phone data.</summary>
    [HttpPut("{id:int}")]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public Task<PhoneResponse> Update(int clientId, int id, PhoneRequest request, CancellationToken ct) =>
        phones.UpdateAsync(clientId, id, request, ct);

    /// <summary>Deletes a phone.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int clientId, int id, CancellationToken ct)
    {
        await phones.DeleteAsync(clientId, id, ct);
        return NoContent();
    }
}
