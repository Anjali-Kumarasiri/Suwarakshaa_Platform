using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
namespace Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public string Create(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new("sub", user.UserId.ToString()),
            new("email", user.Email),
            new("name", user.FullName),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"], audience: _cfg["Jwt:Audience"], claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);   // no claim-type remapping: we use short names
    }
}
