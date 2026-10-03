using BankApi.Contracts;
using BankApi.Domain;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController(ClientService clients) : ControllerBase
{
    /// <summary>Returns a filtered, sorted and paginated list of clients.</summary>
    [HttpGet]
    public Task<PagedResult<ClientResponse>> GetList([FromQuery] ClientFilter filter, CancellationToken ct) =>
        clients.GetListAsync(filter, ct);

    /// <summary>Returns a client with phones.</summary>
    [HttpGet("{id:int}")]
    public Task<ClientResponse> Get(int id, CancellationToken ct) => clients.GetAsync(id, ct);

    /// <summary>Creates a client. Email must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> Create(ClientRequest request, CancellationToken ct)
    {
        var client = await clients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client);
    }

    /// <summary>Replaces client data.</summary>
    [HttpPut("{id:int}")]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public Task<ClientResponse> Update(int id, ClientRequest request, CancellationToken ct) =>
        clients.UpdateAsync(id, request, ct);

    /// <summary>Deletes a client without accounts. Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesErrors(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await clients.DeleteAsync(id, ct);
        return NoContent();
    }
}
