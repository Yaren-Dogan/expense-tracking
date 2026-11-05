using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Application.Features.Account.Commands.ClaimDeviceData;
using ExpenseTracker.Application.Features.Households.Queries.GetMyHousehold;
using ExpenseTracker.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

/// <summary>
/// Registration and sign-in with ASP.NET Core Identity. The session is an HttpOnly cookie, so page scripts never
/// see a token. Identity is an infrastructure concern, which is why this controller talks to it directly.
/// </summary>
[ApiController]
[Route("api/account")]
public sealed class AccountController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ISender mediator) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    public async Task<ActionResult<MeResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is 0 or > AppUser.DisplayNameMaxLength)
            return ValidationError("displayName", $"Ad 1-{AppUser.DisplayNameMaxLength} karakter olmalı.");

        var user = new AppUser { UserName = request.Email?.Trim(), Email = request.Email?.Trim(), DisplayName = displayName };
        var result = await userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
            return IdentityErrors(result);

        await SignInAsync(user);
        var claimed = await ClaimDeviceDataAsync(request.DeviceUserId, ct);
        return StatusCode(StatusCodes.Status201Created, await MeAsync(user, claimed, ct));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email?.Trim() ?? string.Empty);
        if (user is null)
        {
            // Hash anyway, so an unknown address takes as long as a wrong password and timing does not reveal accounts.
            userManager.PasswordHasher.VerifyHashedPassword(UnknownUser, UnknownUserHash, request.Password ?? string.Empty);
            return InvalidCredentials();
        }

        // A locked account gets the same answer as a wrong password: a distinct "locked" reply would tell anyone, after
        // five tries, that the address is registered.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password ?? string.Empty, lockoutOnFailure: true);
        if (!result.Succeeded)
            return InvalidCredentials();

        await SignInAsync(user);
        var claimed = await ClaimDeviceDataAsync(request.DeviceUserId, ct);
        return Ok(await MeAsync(user, claimed, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();
        return Ok(await MeAsync(user, 0, ct));
    }

    private async Task SignInAsync(AppUser user)
    {
        await signInManager.SignInAsync(user, isPersistent: true);
        // The cookie only takes effect on the next request; make this request act as the new user too,
        // so claiming device data below runs under their identity.
        HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
    }

    private async Task<int> ClaimDeviceDataAsync(Guid? deviceUserId, CancellationToken ct) =>
        deviceUserId is { } id ? await mediator.Send(new ClaimDeviceDataCommand(id), ct) : 0;

    private async Task<MeResponse> MeAsync(AppUser user, int claimed, CancellationToken ct)
    {
        var household = await mediator.Send(new GetMyHouseholdQuery(), ct);
        var ledger = household is not null && !LedgerMiddleware.WantsPersonal(HttpContext) ? LedgerMiddleware.Household : LedgerMiddleware.Personal;
        return new MeResponse(user.Id, user.Email!, user.DisplayName,
            household is null ? null : new HouseholdSummary(household.Id, household.Name), ledger, claimed);
    }

    // Same answer for unknown e-mail and wrong password, so the endpoint cannot be used to find registered addresses.
    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials",
            detail: "E-posta veya şifre hatalı. Art arda 5 hatalı denemeden sonra giriş 5 dakika kapanır.");

    private static readonly AppUser UnknownUser = new();
    private static readonly string UnknownUserHash = new PasswordHasher<AppUser>().HashPassword(UnknownUser, Guid.NewGuid().ToString());

    private ActionResult ValidationError(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

    private ActionResult IdentityErrors(IdentityResult result)
    {
        var errors = result.Errors
            .Select(e => e.Code switch
            {
                "DuplicateUserName" or "DuplicateEmail" => ("email", "Bu e-posta ile zaten bir hesap var."),
                "InvalidEmail" or "InvalidUserName" => ("email", "Geçerli bir e-posta adresi gir."),
                "PasswordTooShort" => ("password", "Şifre en az 8 karakter olmalı."),
                "PasswordRequiresDigit" => ("password", "Şifre en az bir rakam içermeli."),
                _ when e.Code.StartsWith("Password", StringComparison.Ordinal) => ("password", e.Description),
                _ => ("form", e.Description)
            })
            .GroupBy(e => e.Item1)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Item2).Distinct().ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
