using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Version1.Data;
using Version1.Services;
using SQCScanner.Services;
using SQCScanner.websoketManager;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("dbc")));
builder.Services.AddScoped<OmrProcessingService>();
builder.Services.AddScoped<JwtAuth>();
builder.Services.AddScoped<EncptDcript>();
builder.Services.AddScoped<RecordDBClass>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSoketHandler>();

// Cross Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigin", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Add Authentication with JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAnyOrigin");
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        var connectionManager = context.RequestServices.GetRequiredService<WebSocketConnectionManager>();
        var handler = context.RequestServices.GetRequiredService<WebSoketHandler>();

        connectionManager.AddSocket(socket);

        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    else
    {
        await next();
    }
});

app.MapControllers();
app.Run();
