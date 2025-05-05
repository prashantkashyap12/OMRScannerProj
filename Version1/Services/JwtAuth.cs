using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SQCScanner.Modal;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SQCScanner.Services
{
    public class JwtAuth
    {
        public string GenerateJwtToken(EmpModel emp)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes("aEj7A6mr5yVoDx0wq1jUj0A6xhb/8I+YJ0T+Y8h2sJk=\r\n");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
            {
                    new Claim(ClaimTypes.NameIdentifier, emp.EmpId.ToString()),
                    new Claim(ClaimTypes.Name, emp.EmpName),
                    new Claim(ClaimTypes.Email, emp.EmpEmail),
                    new Claim("Password", emp.password),
                    new Claim(ClaimTypes.MobilePhone, emp.contact),
                    new Claim(ClaimTypes.Role, emp.role),
                }),
                Expires = DateTime.UtcNow.AddHours(1), // token 1 ghante me expire hoga
                Issuer = "adityaInfotech",
                Audience = "GTG's IntoTech",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            // Token ko create karte hain
             var token = tokenHandler.CreateToken(tokenDescriptor);
            // Token ko string me convert karte hain
             return tokenHandler.WriteToken(token);
        }
    }
}
