using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Version1.Data;
using Version1.Services;
using SQCScanner.Services;
using SQCScanner.websoketManager;
using System.Net.WebSockets;
using Microsoft.Extensions.FileProviders;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
// Possible crash point
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("dbc")));
builder.Services.AddScoped<OmrProcessingService>();
builder.Services.AddScoped<JwtAuth>();
builder.Services.AddScoped<EncptDcript>();
builder.Services.AddScoped<RecordDBClass>();
builder.Services.AddScoped<RecordSave>();
builder.Services.AddScoped<table_gen>();
builder.Services.AddScoped<ImgSave>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<OmrProcessingControlService>();
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


// Syncfusion Key Add
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NCaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXhdcHRVQmVeV0F3Wks=\r\n");


// Add Authentication with JWT
builder.Services.AddJwtAuthentication();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // ✅ This shows detailed errors in browser
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Permission Folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wFileManager")),
    RequestPath = "/wFileManager",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});

// wwwroot ke liye CORS header add karo
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});
app.UseStaticFiles();



// Cross Policy
app.UseCors("AllowAnyOrigin");

// Http Routing Redirection
app.UseHttpsRedirection();

// Auth JWT valid / New
app.UseAuthentication();
app.UseAuthorization();


// web Soket Middleware
app.UseWebSockets();
app.Use(async (context, next) =>
{
    // make path, client ne header k sath req. bheji hai / ni.
    if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
    {
        // Check if the request is a WebSocket request
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        // 2. Extract token from query string
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 400; // Bad request
            await context.Response.WriteAsync("Token missing");
            return;
        }

        // 3. Parse JWT token to get userId
        string userId = null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
        }
        catch
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("Invalid token");
            return;
        }

        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("UserId not found in token");
            return;
        }

        // 4. Get services
        var connectionManager = context.RequestServices.GetRequiredService<WebSocketConnectionManager>();
        var handlerService = context.RequestServices.GetRequiredService<WebSoketHandler>();

        // 5. Add socket with userId
        connectionManager.AddSocket(userId, socket);
        var buffer = new byte[1024 * 4];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
        }
        finally
        {
            // 6. Remove socket on disconnect
            connectionManager.RemoveSocket(userId);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
    else
    {
        await next();
    }
});


app.MapControllers();
try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
    throw;
}
