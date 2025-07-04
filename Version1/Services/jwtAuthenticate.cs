using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using SixLabors.ImageSharp;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SQCScanner.Services
{
    public static class JwtServiceExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {

            var _conn = configuration.GetConnectionString("dbc");
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings.GetValue<string>("SecretKey");
            var issuer = jwtSettings.GetValue<string>("Issuer");
            var audience = jwtSettings.GetValue<string>("Audience");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "adityaInfotech",
                        ValidAudience = "GTG's IntoTech",
                        RoleClaimType = ClaimTypes.Role,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("aEj7A6mr5yVoDx0wq1jUj0A6xhb/8I+YJ0T+Y8h2sJk=")),
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Console.WriteLine("Authentication Failed");
                            return Task.CompletedTask;
                        },

                        OnTokenValidated = context =>
                        {
                            var token = context.SecurityToken as JwtSecurityToken;
                            var tokenString = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
                            dynamic res;
                            if (string.IsNullOrEmpty(tokenString))
                            {
                                context.Fail("Token missing");
                                return Task.CompletedTask;
                            }
                            try
                            {
                                using (var _conn = new SqlConnection(configuration.GetConnectionString("dbc")))
                                {
                                    _conn.Open();
                                    var query = @$"select COUNT(1) from LoginTokenRec where Token = @token";
                                    var result = _conn.ExecuteScalar<int>(query, new { token = tokenString });
                                    if (result > 0)
                                    {
                                        Console.WriteLine("Authorized success");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Authorized fail");
                                        context.Fail("Token not found in DB");
                                    }
                                }
                                return res = Task.CompletedTask;
                            }catch(Exception ex)
                            {
                                return res = ex.Message;

                            }
                        },

                        OnMessageReceived = context =>
                        {
                            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                            {
                                context.Token = authHeader.Substring("Bearer ".Length).Trim();
                            }
                            Console.WriteLine(context.Token);
                            return Task.CompletedTask;
                        }
                    };

                });
            return services;
        }
    }
}
