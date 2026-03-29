namespace GenReport.Endpoints.Onboarding
{
    using FastEndpoints;
    using GenReport.DB.Domain.Enums;
    using GenReport.Domain.DBContext;
    using GenReport.Infrastructure.Interfaces;
    using GenReport.Infrastructure.Models.HttpRequests.Onboarding;
    using GenReport.Infrastructure.Models.HttpResponse.Onboarding;
    using GenReport.Infrastructure.Models.Shared;
    using GenReport.Infrastructure.Static.Constants;
    using Microsoft.EntityFrameworkCore;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;

    /// <summary>
    /// Defines the <see cref="Refresh" />
    /// </summary>
    public class Refresh(ApplicationDbContext context, IApplicationConfiguration configuration, IJWTTokenService jwtTokenService) : Endpoint<RefreshRequest, HttpResponse<LoginResponse>>
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IApplicationConfiguration _configuration = configuration;
        private readonly IJWTTokenService _jwtTokenService = jwtTokenService;

        public override void Configure()
        {
            Post("/refresh");
            AllowAnonymous();
        }

        public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                await SendAsync(new HttpResponse<LoginResponse>(System.Net.HttpStatusCode.BadRequest, "Refresh token is missing", ErrorMessages.TOKEN_NOT_VALID, ["Refresh token must be provided"]), cancellation: ct);
                return;
            }

            var validationResult = await _jwtTokenService.ValidateToken(req.RefreshToken, _configuration.IssuerRefreshKey);
            if (!validationResult.Status)
            {
                await SendAsync(new HttpResponse<LoginResponse>(System.Net.HttpStatusCode.Unauthorized, "Invalid refresh token", ErrorMessages.TOKEN_NOT_VALID, [validationResult.Message ?? "Invalid refresh token"]), cancellation: ct);
                return;
            }

            // Extract user ID from token
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(req.RefreshToken);

            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long userId))
            {
                await SendAsync(new HttpResponse<LoginResponse>(System.Net.HttpStatusCode.Unauthorized, "Invalid refresh token claims", ErrorMessages.TOKEN_NOT_VALID, ["User ID could not be extracted from the token"]), cancellation: ct);
                return;
            }

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken: ct);
            if (user == null)
            {
                await SendAsync(new HttpResponse<LoginResponse>(System.Net.HttpStatusCode.Unauthorized, "User not found", ErrorMessages.USER_NOT_FOUND, [$"User with ID {userId} not found"]), cancellation: ct);
                return;
            }

            // Generate new tokens
            var newToken = _jwtTokenService.GenrateAccessToken(user, _configuration.IssuerSigningKey, _configuration.AccessTokenExpiry);
            var newRefreshToken = _jwtTokenService.GenrateAccessToken(user, _configuration.IssuerRefreshKey, _configuration.RefreshTokenExpiry);

            var roleName = Enum.IsDefined(typeof(Role), user.RoleId) ? ((Role)user.RoleId).ToString().ToLower() : "user";

            await SendAsync(new HttpResponse<LoginResponse>(new LoginResponse
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                Role = roleName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            }, "Tokens refreshed successfully", System.Net.HttpStatusCode.OK), cancellation: ct);
        }
    }
}
