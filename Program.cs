using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using DiscordBotApi.DiscordBot.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

//Logger writer
using var fileStream = new FileStream("bot_logs.log", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
using var textWriter = new StreamWriter(fileStream);
textWriter.AutoFlush = true;

//Create bot gateway client
var botClient = DiscordBotProvider.CreateBotClient(textWriter);

//Add bot dependencies
builder.Services.AddKeyedSingleton("writer", textWriter);
builder.Services.AddKeyedSingleton("client", botClient);

//Start bot in background
builder.Services.AddHostedService<BotBackgroundService>();

builder.Services.AddSingleton<UpdateUsersService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                              ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseForwardedHeaders();

app.UseRouting();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
