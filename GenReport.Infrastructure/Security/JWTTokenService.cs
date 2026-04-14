namespace GenReport.Infrastructure.Security
{
    using GenReport.Domain.Entities.Onboarding;
    using GenReport.Infrastructure.Interfaces;
    using GenReport.Infrastructure.Static.Constants;
    using Microsoft.IdentityModel.Tokens;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;

    /// <summary>
    /// Defines the <see cref="JWTTokenService" />
    /// </summary>
    public class JWTTokenService : IJWTTokenService
    {
        /// <summary>
        /// Gets or sets the _tokenHandler
        /// </summary>
        private JwtSecurityTokenHandler? _tokenHandler { get; set; }

        /// <summary>
        /// Gets or sets the tokenHandler
        /// </summary>
        public JwtSecurityTokenHandler tokenHandler
        {
            get { return _tokenHandler ??= new JwtSecurityTokenHandler(); }
            set { }
        }

        /// <summary>
        /// The GenrateAccessToken
        /// </summary>
        /// <param name="user">The user<see cref="User"/></param>
        /// <param name="jwtSecret">The jwtSecret key<see cref="string"/></param>
        /// <param name="expireIn">The expiration time in minutes<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string GenrateAccessToken(User user, string jwtSecret, int expireIn)
        {
            var claims = new List<Claim>() { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, string.Format("{0} {1}", user.FirstName, user.LastName)), new(ClaimTypes.Role, user.RoleId.ToString()), new(ClaimTypes.Email, user.Email) };
            var jwtToken = new JwtSecurityToken(
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expireIn),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(
                       Encoding.UTF8.GetBytes(jwtSecret)
                        ),
                    SecurityAlgorithms.HmacSha256Signature)
                );
            return tokenHandler.WriteToken(jwtToken);
        }

        /// <summary>
        /// The ValidateToken
        /// </summary>
        /// <param name="token">The token<see cref="string"/></param>
        /// <returns>The <see cref="(bool Status, string? Message)"/></returns>
        public async Task<(bool Status, string? Message)> ValidateToken(string token , string issuerKey)
        {
            var result = await tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(issuerKey)),
            });
            if (result.Exception != null)
            {
                return (false, result.Exception.Message);
            }
            if (result.IsValid)
            {
                return (true, null);
            }
            return (false,ErrorMessages.TOKEN_NOT_VALID);
        }

       
    }
}
