using DiscordBotApi.Data.Users;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBotApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher _passwordHasher;
        private readonly TokenProvider _tokenProvider;

        public UsersController(ApplicationDbContext context, PasswordHasher passwordHasher, TokenProvider tokenProvider)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenProvider = tokenProvider;
        }

        [HttpPost("/create")]
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

            var passwordHash = _passwordHasher.GetHash(userDto.Password);
            var isCreated = _context.CreateApiUser(userDto.Login, passwordHash);
            if (!isCreated)
            {
                return StatusCode(500);
            }

            return Created();
        }

        [HttpPost("/login")]
        public IActionResult LoginUser([FromBody] ApiUserLoginDto userDto)
        {
            if (string.IsNullOrEmpty(userDto.Login) || string.IsNullOrEmpty(userDto.Password))
            {
                return BadRequest(new ProblemDetails()
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Login or password was not provided"
                });
            }

            var apiUser = _context.GetApiUser(userDto.Login);

            if (apiUser == null)
            {
                return Unauthorized("Wrong login or password");
            }

            if (!_passwordHasher.IsVarified(userDto.Password, apiUser.PasswordHash))
            {
                return Unauthorized("Wrong login or password");
            }

            var token = _tokenProvider.Create(apiUser);

            return Ok(token);
        }
    }
}
