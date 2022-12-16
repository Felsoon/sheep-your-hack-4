﻿using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using backend.Models;
using backend.Entities;

namespace backend.Authentication
{
    public class JWTManager
    {
        private readonly IConfiguration configuration;

        public JWTManager(IConfiguration configuration) => this.configuration = configuration;

        public static TokenValidationParameters CreateTokenValidationParameters(IConfiguration configuration) => new TokenValidationParameters()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = configuration["JWT:Issuer"],
            ValidAudience = configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Key"]))
        };

        public string GenerateToken(User user)
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            byte[]? key = Encoding.UTF8.GetBytes(configuration["JWT:Key"]);
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Nickname)
                }),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            SecurityToken? token = handler.CreateToken(descriptor);
            string tokenString = handler.WriteToken(token);
            return tokenString;
        }

        public string GenerateRefreshToken()
        {
            byte[]? randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? GetPrincipalFromToken(string token, bool allowExpiration)
        {
            TokenValidationParameters tokenValidationParameters = CreateTokenValidationParameters(configuration);
            tokenValidationParameters.ValidateLifetime = !allowExpiration;
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsPrincipal? principal;
            SecurityToken securityToken;

            try
            {
                principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            }
            catch
            {
                return null;
            }

            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                return null;

            return principal;
        }

        public TokenResult CreateTokenResult(User user) => new TokenResult()
        {
            Token = GenerateToken(user),
            RefreshToken = GenerateRefreshToken()
        };

        public static string GetTokenFromRequest(HttpRequest request)
        {
            string value = request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(value) || value.Length < 7)
                return string.Empty;

            return request.Headers["Authorization"].ToString()[7..];
        }
    }
}
