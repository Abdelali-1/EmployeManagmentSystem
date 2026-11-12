using baseLibrary.DTO;
using baseLibrary.Entities;
using baseLibrary.Responses;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServerLibrary.Helpers;
using ServerLibrary.Repository.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ServerLibrary.Repository.Implementation
{
    public class UserAccountRepository(IOptions<JwtSection> config, 
        IRepository<ApplicationUser> repository, 
        IRepository<Roles> repositoryRole, 
        IRepository<UserRole> UserRoleRepo,
        IRepository<RefrishTokenInfo> refirshTokenRepo) : IUserAccount
    {

        public async Task<GeneralResponse> CreateAsync(Register user)
        {
            // 1. Validate registration data
            if (user is null ||
                string.IsNullOrWhiteSpace(user.FullName) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return new GeneralResponse(
                    false,
                    "Full name, email, and password are required."
                );
            }

            // 2. Check if email already exists
            var email = user.Email.Trim();

            var existingUser = await FindUserByMail(email);

            if (existingUser is not null)
                return new GeneralResponse(
                    false,
                    "An account with this email already exists."
                );

            // 3. Create application user
            var applicationUser = new ApplicationUser
            {
                FullName = user.FullName.Trim(),
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
            };

            var result = await AddUserToDB(applicationUser);

            // 4. Get or create default User role
            var userRole = await repositoryRole.FirstOrDefaultAsync(
                x => x.Name == Constants.User
            );

            if (userRole is null)
            {
                userRole = await AddRoleToDB(new Roles
                {
                    Name = Constants.User
                });
            }

            // 5. Assign User role
            await AddUserRoleToDB(new UserRole
            {
                UserId = result.Id,
                RoldId = userRole.Id
            });

            // 6. Return success response
            return new GeneralResponse(
                true,
                "Your account has been created successfully."
            );
        }

        public async Task<LoginResponse> SignInAsync(Login user)
        {
            if (user is null ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return new LoginResponse(
                    false,
                    "Email and password are required."
                );
            }

            var applicationUser = await FindUserByMail(user.Email);

            if (applicationUser is null)
                return new LoginResponse(false, "Invalid email or password.");

            if (!BCrypt.Net.BCrypt.Verify(user.Password, applicationUser.Password))
                return new LoginResponse(false, "Invalid email or password.");

            var userRole = await FindUserRole(applicationUser.Id);

            if (userRole is null)
                return new LoginResponse(false, "User role is not assigned.");

            var role = await FindRoleName(userRole.UserId);

            if (role is null)
                return new LoginResponse(false, "User role could not be found.");

            string jwtToken = GenerateToken(applicationUser, role.Name!);
            string refreshToken = GenerateRefrishToken();

            var existingRefrishToken = await refirshTokenRepo.FirstOrDefaultAsync(x => x.UserId == applicationUser.Id);
            if(existingRefrishToken is null)
            {
                await refirshTokenRepo.AddAsync(new RefrishTokenInfo()
                {
                    UserId = applicationUser.Id,
                    Token = refreshToken
                });
                await refirshTokenRepo.SaveChangesAsync();
            }
            else
            {
                existingRefrishToken.Token = refreshToken;
                refirshTokenRepo.Update(existingRefrishToken);
                await refirshTokenRepo.SaveChangesAsync();

            }
            return new LoginResponse(
                true,
                "Login successful.",
                jwtToken,
                refreshToken
            );
        }

        private string GenerateToken(ApplicationUser user, string role)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.Key!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var userClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Role, role),
            };

            var token = new JwtSecurityToken(
                issuer: config.Value.Issuer,
                audience: config.Value.Audience,
                claims: userClaims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        private static string GenerateRefrishToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private async Task<UserRole> FindUserRole(int userId) => await UserRoleRepo.FirstOrDefaultAsync(x => x.UserId == userId);
        private async Task<Roles> FindRoleName(int roleId) => await repositoryRole.FirstOrDefaultAsync(x => x.Id == roleId);

        public async Task<LoginResponse> RefrishTokenAsync(RefrishToken token)
        {
            if (token is null || string.IsNullOrWhiteSpace(token.Token))
                return new LoginResponse(false, "Refresh token is required.");

            // 1. Find the stored refresh token
            var storedToken = await refirshTokenRepo.FirstOrDefaultAsync(x => x.Token == token.Token);
            if (storedToken is null)
                return new LoginResponse(false, "Invalid refresh token. Please sign in again.");

            // 2. Find the user who owns it
            var user = await repository.FirstOrDefaultAsync(x => x.Id == storedToken.UserId);
            if (user is null)
                return new LoginResponse(false, "User not found. Please sign in again.");

            // 3. Find the user's role
            var userRole = await FindUserRole(user.Id);
            if (userRole is null)
                return new LoginResponse(false, "No role is assigned to this user.");

            var roleName = await FindRoleName(userRole.RoldId);
            if (roleName is null)
                return new LoginResponse(false, "The user's role could not be found.");

            // 4. Generate new tokens
            string jwtToken = GenerateToken(user, roleName.Name!);
            string newRefreshToken = GenerateRefrishToken();

            // 5. Save the new refresh token
            storedToken.Token = newRefreshToken;
            await refirshTokenRepo.SaveChangesAsync();

            return new LoginResponse(true, "Token refreshed successfully.", jwtToken, newRefreshToken);
        }

        private async Task<ApplicationUser> FindUserByMail(string email) =>
            await repository.FirstOrDefaultAsync(_ => _.Email!.ToLower()!.Equals(email!.ToLower()));

        private async Task<ApplicationUser> AddUserToDB(ApplicationUser user)
        {
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
            return user;
        }
        private async Task<Roles> AddRoleToDB(Roles role)
        {
            await repositoryRole.AddAsync(role);
            await repositoryRole.SaveChangesAsync();
            return role;
        }

        private async Task<UserRole> AddUserRoleToDB(UserRole role)
        {
            await UserRoleRepo.AddAsync(role);
            await UserRoleRepo.SaveChangesAsync();
            return role;
        }

    }
}
