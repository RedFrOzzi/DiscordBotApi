using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

DotEnv.Load();
builder.Configuration.AddEnvironmentVariables();
var securityKey =  Environment.GetEnvironmentVariable("SECURITY_KEY") ?? throw new("Security key was null");

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
builder.Services.AddOpenApi(options => 
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

//Add dicord bot gateway
builder.Services.AddDiscordGateway(opt =>
{
    opt.Token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
});

builder.Services
    .AddApplicationCommands()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    .AddComponentInteractions<UserMenuInteraction, UserMenuInteractionContext>()
    .AddComponentInteractions<ChannelMenuInteraction, ChannelMenuInteractionContext>()
    .AddComponentInteractions<ModalInteraction, ModalInteractionContext>();

builder.Services.AddSingleton<UpdateUsersService>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenProvider>();
builder.Services.AddSingleton<RaffleAllowedUsersService>();

//builder.Services.Configure<ForwardedHeadersOptions>(options =>
//{
//    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
//                              ForwardedHeaders.XForwardedProto;
//    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
//});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey)),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero,
        };
    });

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseForwardedHeaders();

app.UseRouting();

app.UseCors();

app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.AddModules(typeof(Program).Assembly);

await app.RunAsync();
