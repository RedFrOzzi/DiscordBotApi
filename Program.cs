using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Middlewares;
using DiscordBotApi.Utilities;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Запуск приложения");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    DotEnv.Load();
    builder.Configuration.AddEnvironmentVariables();
    var securityKey = Environment.GetEnvironmentVariable("SECURITY_KEY") ?? throw new("Security key was null");

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

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddHttpClient();

    //Add dicord bot gateway
    builder.Services.AddDiscordGateway(opt =>
    {
        opt.Token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        opt.Intents = NetCord.Gateway.GatewayIntents.All;
    });

    builder.Services.AddGatewayHandlers(typeof(Program).Assembly);

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
    builder.Services.AddSingleton<VoiceInstancesContainer>();

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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey)),
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero,
            };
        });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось неожиданно");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;