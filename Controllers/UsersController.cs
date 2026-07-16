using DiscordBotApi.Data.ApiUsers;
using DiscordBotApi.Data.ApiUsers.Dtos;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
            if (_context.SaveChanges() > 0)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }                

            return Created();
        }

        [HttpPost("/login")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

            var apiUser = _context.ApiUsers
                .AsNoTracking()
                .FirstOrDefault(u => u.Login == userDto.Login);

            if (apiUser == null)
                return Unauthorized("Wrong login or password");

            if (!_passwordHasher.IsVarified(userDto.Password, apiUser.PasswordHash))
                return Unauthorized("Wrong login or password");

            var token = _tokenProvider.Create(apiUser);

            return Ok(token);
        }

        [HttpPost("/create-admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult CreateAdmin([FromBody] ApiUserAddAdminDto apiUserAddAdminDto)
        {
            if (string.IsNullOrEmpty(apiUserAddAdminDto?.Keyword) || string.IsNullOrEmpty(apiUserAddAdminDto?.UserLogin))
            {
                return BadRequest(new ProblemDetails()
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Keyword was not accepted"
                });
            }

            var storedKeyword = Environment.GetEnvironmentVariable("ADMIN_INITIALIZATION_PASSWORD");
            if (string.IsNullOrEmpty(storedKeyword))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var user = _context.ApiUsers.FirstOrDefault(u => u.Login == apiUserAddAdminDto.UserLogin);
            if (user == null)
            {
                return NotFound(new ProblemDetails()
                {
                    Status = StatusCodes.Status404NotFound,
                    Detail = "User not found"
                });
            }

            if (string.Equals(apiUserAddAdminDto.Keyword, storedKeyword, StringComparison.Ordinal))
            {
                user.IsAdmin = true;
                if (_context.SaveChanges() > 0)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }

                return Ok();
            }

            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Data error"
            });
        }
    }
}
