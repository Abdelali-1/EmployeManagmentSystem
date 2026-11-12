using baseLibrary.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.Extensions.Localization;
using ServerLibrary.Repository.Contracts;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController(IUserAccount userAccount) : ControllerBase
    {

        [HttpPost("Register")]
        public async Task<IActionResult> CreateAsync([FromBody] Register user)
        {
            if (user is null)
                return BadRequest(new
                {
                    message = "Registration data is required."
                });

            var result = await userAccount.CreateAsync(user);
            if (!result.Flag)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Ok(result);
        }

        [HttpPost("Login")]
        public async Task<IActionResult> SignInAsync([FromBody] Login user)
        {
            if (user is null)
                return BadRequest(new
                {
                    message = "Login credentials are required."
                });

            if (string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new
                {
                    message = "Email and password are required."
                });
            }

            var result = await userAccount.SignInAsync(user);
            if (!result.Flag)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = result.Message
                });
            }
            return Ok(result);
        }

        [HttpPost("RefrishToken")]
        public async Task<IActionResult> RefirshTokenAsync(RefrishToken token)
        {
            if (token is null || string.IsNullOrWhiteSpace(token.Token)) return BadRequest( new 
            {
                success = false,
                message = "Refresh token is required."
            });
            var result = await userAccount.RefrishTokenAsync(token);
            if (!result.Flag)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Ok(result);
        }

    }


}
