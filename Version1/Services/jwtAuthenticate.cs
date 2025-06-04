using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace SQCScanner.Services
{
    public static class JwtServiceExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "adityaInfotech",
                        ValidateAudience = true,
                        ValidAudience = "GTG's IntoTech",
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("aEj7A6mr5yVoDx0wq1jUj0A6xhb/8I+YJ0T+Y8h2sJk="))
                    };
                });

            return services;
        }
    }
}
