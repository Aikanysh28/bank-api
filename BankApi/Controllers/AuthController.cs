using BankApi.Contracts;
using BankApi.Domain;
using BankApi.Services;
using BankApi.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Registers a user with the Operator role.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesErrors(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await auth.RegisterAsync(request.Username, request.Password, UserRole.Operator, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>Returns a JWT access token.</summary>
    [HttpPost("login")]
    [ProducesErrors(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var token = await auth.LoginAsync(request, ct);
        if (token is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid username or password.");

        return token;
    }
}
