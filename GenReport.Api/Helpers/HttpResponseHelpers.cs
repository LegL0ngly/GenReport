using GenReport.Infrastructure.Models;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Static.Constants;
using System.Net;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace GenReport.Helpers
{
    /// <summary>
    /// Helper functions for httpResponse
    /// </summary>
    public class HttpResponseHelpers
    {
        /// <summary>
        /// The send token expired response
        /// </summary>
        private static Action<HttpContext> sendTokenExpiredResponse = (HttpContext httpcontext) =>
        {
            httpcontext.Response.Headers.Append(GenericConstants.TOKEN_EXPIRED_HEADER, "true");
            httpcontext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            var response = new HttpErrorResponse(HttpStatusCode.Unauthorized, "", ErrorMessages.TOKEN_EXPIRED);
            response.AddError($"the token generated has expired please use the refresh token and URL: {httpcontext.GetBaseUrl()}/refresh to generate a new token.");
        };

        /// <summary>
        /// Gets or sets the send token expired response.
        /// </summary>
        /// <value>
        /// The send token expired response.
        /// </value>
        public static Action<HttpContext> SendTokenExpiredResponse { get => sendTokenExpiredResponse; set => sendTokenExpiredResponse = value; }
        /// <summary>
        /// Gets or sets the send invalid token response.
        /// </summary>
        /// <value>
        /// The send invalid token response.
        /// </value>
        public static Action<HttpContext> SendInvalidTokenResponse { get => sendInvalidTokenResponse; set => sendInvalidTokenResponse = value; }

        /// <summary>
        /// The send invalid token response
        /// </summary>
        private static Action<HttpContext> sendInvalidTokenResponse = (HttpContext httpContext) =>
        {
            httpContext.Response.Headers.Append(GenericConstants.INVALID_TOKEN_HEADER, "true");
            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            var response = new HttpErrorResponse(HttpStatusCode.Unauthorized, "", ErrorMessages.TOKEN_NOT_VALID);
            response.AddError($"the token is invalid please check credentials");

        };
        /// <summary>
        /// Gets or sets the add user to context.
        /// </summary>
        /// <value>
        /// The add user to context.
        /// </value>
        public static Action<HttpContext> AddUserToContext { get => addUserToContext; set => addUserToContext = value; }

        /// <summary>
        /// The add user to context
        /// </summary>
        private static Action<HttpContext> addUserToContext = (HttpContext httpContext) => 
        {
            if (httpContext?.User?.Identity is not ClaimsIdentity existingIdentity || !existingIdentity.IsAuthenticated)
            {
                return;
            }

            var sourceClaims = httpContext.User.Claims.ToList();

            string? userId = sourceClaims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "sub")?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "nameid")?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "userId")?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "userid")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            string userName = sourceClaims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.UniqueName)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "name")?.Value
                ?? userId;

            string? role = sourceClaims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "role")?.Value;

            string? email = sourceClaims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Email)?.Value
                ?? sourceClaims.FirstOrDefault(x => x.Type == "email")?.Value;

            var normalizedClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, userName)
            };

            if (!string.IsNullOrWhiteSpace(role))
            {
                normalizedClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                normalizedClaims.Add(new Claim(ClaimTypes.Email, email));
            }

            foreach (var claim in sourceClaims)
            {
                bool exists = normalizedClaims.Any(x => x.Type == claim.Type && x.Value == claim.Value);
                if (!exists)
                {
                    normalizedClaims.Add(claim);
                }
            }

            var identity = new ClaimsIdentity(
                normalizedClaims,
                existingIdentity.AuthenticationType ?? "Bearer",
                ClaimTypes.Name,
                ClaimTypes.Role);

            httpContext.User = new ClaimsPrincipal(identity);
        };
    }
}
