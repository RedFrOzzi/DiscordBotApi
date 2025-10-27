using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using DiscordBotApi.DiscordBot.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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

builder.Services.AddSingleton<DiscordBotBackgroundService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DiscordBotBackgroundService>());
builder.Services.AddSingleton<UpdateUsersService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("Running in development environment.");
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    Console.WriteLine("Running in release environment.");
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
