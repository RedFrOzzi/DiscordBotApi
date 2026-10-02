using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.RefreshTokens;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("users")]
public class UsersController(
    IConfiguration configuration,
    ApplicationDbContext context,
    PasswordHasher passwordHasher,
    TokenProvider tokenProvider) : ControllerBase
{
    readonly IConfiguration _configuration = configuration;
    readonly ApplicationDbContext _context = context;
    readonly PasswordHasher _passwordHasher = passwordHasher;
    readonly TokenProvider _tokenProvider = tokenProvider;
    readonly string _refreshCookieName = configuration["Jwt:RefreshCookieName"] ?? "dba_refresh";

    [HttpPost("create")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public IActionResult CreateUser([FromBody] ApiUserLoginDto userDto)
    {
        if (string.IsNullOrEmpty(userDto.Login) || string.IsNullOrEmpty(userDto.Password))
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Login or password was not provided"
            });
        }

        if (_context.ApiUsers.Any(u => u.Login == userDto.Login))
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Already exist"
            });
        }

        var user = new ApiUser
        {
            Login = userDto.Login,
            PasswordHash = _passwordHasher.GetHash(userDto.Password)
        };

        _context.ApiUsers.Add(user);
        if (_context.SaveChanges() == 0)
            return StatusCode(StatusCodes.Status500InternalServerError);      

        return Created();
    }

    [HttpPost("login")]
    [ProducesResponseType<string>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public IActionResult LoginUser([FromBody] ApiUserLoginDto userDto)
    {
        if (userDto == null || string.IsNullOrEmpty(userDto.Login) || string.IsNullOrEmpty(userDto.Password))
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Login or password was not provided"
            });
        }

        var apiUser = _context.ApiUsers
            .FirstOrDefault(u => u.Login == userDto.Login);

        if (apiUser == null)
            return Unauthorized("Wrong login or password");

        if (!_passwordHasher.IsVarified(userDto.Password, apiUser.PasswordHash))
            return Unauthorized("Wrong login or password");

        var token = _tokenProvider.Create(apiUser);

        var rawRefreshToken = _tokenProvider.CreateRawRefreshToken();
        var refreshToken = _tokenProvider.BuildRefreshToken(apiUser, rawRefreshToken);

        _context.RefreshTokens.Add(refreshToken);
        _context.SaveChanges();

        Response.Cookies.Append(
            _configuration["Jwt:RefreshCookieName"] ?? "dba_refresh",
            rawRefreshToken,
            AuthCookies.RefreshCookieOptions());

        return Ok(token);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<string>(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(_refreshCookieName, out var rawToken) || string.IsNullOrEmpty(rawToken))
            return Unauthorized();

        var hash = TokenProvider.Hash(rawToken);

        var stored = _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefault(t => t.TokenHash == hash);

        if (stored is null || !stored.IsActive)
        {
            Response.Cookies.Delete(_refreshCookieName, AuthCookies.RefreshCookieOptions());
            return Unauthorized();
        }

        stored.RevokedAt = DateTime.UtcNow;
        var newRawRefreshToken = _tokenProvider.CreateRawRefreshToken();
        var newRefreshToken = _tokenProvider.BuildRefreshToken(stored.User, newRawRefreshToken);

        _context.RefreshTokens.Add(newRefreshToken);
        _context.SaveChanges();

        Response.Cookies.Append(_refreshCookieName, newRawRefreshToken, AuthCookies.RefreshCookieOptions());

        var newAccess = _tokenProvider.Create(stored.User);
        return Ok(newAccess);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(_refreshCookieName, out var rawToken) && !string.IsNullOrEmpty(rawToken))
        {
            var hash = TokenProvider.Hash(rawToken);
            var stored = _context.RefreshTokens
                .FirstOrDefault(t => t.TokenHash == hash);

            if (stored is not null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                _context.SaveChanges();
            }
        }

        Response.Cookies.Delete(_refreshCookieName, AuthCookies.RefreshCookieOptions());
        return Ok();
    }

    [HttpPost("create-admin")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public IActionResult CreateAdmin([FromBody] ApiUserAddAdminDto apiUserAddAdminDto)
    {
        if (apiUserAddAdminDto == null 
            || string.IsNullOrEmpty(apiUserAddAdminDto?.Keyword)
            || string.IsNullOrEmpty(apiUserAddAdminDto?.Login)
            || string.IsNullOrEmpty(apiUserAddAdminDto?.Password)
            || string.IsNullOrEmpty(apiUserAddAdminDto?.NewAdminLogin))
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Parameters error"
            });
        }

        var storedKeyword = Environment.GetEnvironmentVariable("ADMIN_INITIALIZATION_PASSWORD");
        if (string.IsNullOrEmpty(storedKeyword))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var authorizedUser = _context.ApiUsers.AsNoTracking().FirstOrDefault(u => u.Login == apiUserAddAdminDto.Login);
        var newAdminUser = _context.ApiUsers.FirstOrDefault(u => u.Login == apiUserAddAdminDto.NewAdminLogin);

        if (authorizedUser == null)
            return Unauthorized("Wrong login or password");

        if (!_passwordHasher.IsVarified(apiUserAddAdminDto.Password, authorizedUser.PasswordHash))
            return Unauthorized("Wrong login or password");

        if (newAdminUser == null)
            return NotFound("User not found");

        if (string.Equals(apiUserAddAdminDto.Keyword, storedKeyword, StringComparison.Ordinal))
        {
            newAdminUser.IsAdmin = true;
            if (_context.SaveChanges() == 0)
                return StatusCode(StatusCodes.Status500InternalServerError);

            return Ok();
        }

        return StatusCode(StatusCodes.Status500InternalServerError);
    }

    [HttpPost("create-moderator")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public IActionResult CreateMod([FromBody] ApiUserAddModeratorDto apiUserAddModDto)
    {
        if (apiUserAddModDto == null 
            || string.IsNullOrEmpty(apiUserAddModDto?.Login)
            || string.IsNullOrEmpty(apiUserAddModDto?.Password)
            || string.IsNullOrEmpty(apiUserAddModDto?.NewModeratorLogin))
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Parameters error"
            });
        }

        var authorizedUser = _context.ApiUsers.AsNoTracking().FirstOrDefault(u => u.Login == apiUserAddModDto.Login);
        var newModeratorUser = _context.ApiUsers.FirstOrDefault(u => u.Login == apiUserAddModDto.NewModeratorLogin);

        if (authorizedUser == null)
            return Unauthorized("Wrong login or password");

        if (!_passwordHasher.IsVarified(apiUserAddModDto.Password, authorizedUser.PasswordHash))
            return Unauthorized("Wrong login or password");

        if (newModeratorUser == null)
            return NotFound("User not found");

        newModeratorUser.IsModerator = true;
        if (_context.SaveChanges() == 0)
            return StatusCode(StatusCodes.Status500InternalServerError);

        return Ok();
    }
}
