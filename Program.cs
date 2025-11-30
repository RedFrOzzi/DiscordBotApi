using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.DiscordBot.Services.Secrets;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Net;
using System.Text;
using System.Text.Json;

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
builder.Services.AddOpenApi("v1", options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

//Get secret
string secret;
if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
{
    throw new Exception($"secrets not found in base directory {AppDomain.CurrentDomain.BaseDirectory}");
}
else
{
    var secretsJson = JsonSerializer.Deserialize<SecretsJson>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
        ?? throw new Exception($"could not deserialize json file");

    if (secretsJson.Salt == null)
        throw new Exception($"secrets file does not contain salt");

    secret = secretsJson.Salt;
}

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

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenProvider>(sp =>
{
    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
    return new TokenProvider(configuration, secret);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                              ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
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

app.Run();
