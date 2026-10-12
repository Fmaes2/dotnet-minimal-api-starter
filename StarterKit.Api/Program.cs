using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Serilog;
using FluentValidation;
using StarterKit.Api.Endpoints;
using StarterKit.Api.Auth;
using StarterKit.Api.Data;
using StarterKit.Api.Middleware;


Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day),
        preserveStaticLogger: true);

    // Database (PostgreSQL)
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    // JWT
    var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
              ?? throw new InvalidOperationException("The 'Jwt' section is missing from the configuration.");

    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        throw new InvalidOperationException(
            "Jwt:Key is missing or too short (minimum 32 characters). " +
            "Set it with: dotnet user-secrets set \"Jwt:Key\" \"<your-key>\"");

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        });
    builder.Services.AddAuthorization();

    // Global error handling (ProblemDetails) and OpenAPI
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();

    builder.Services.AddIdentityCore<IdentityUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.User.RequireUniqueEmail = true;
    })
        .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.AddScoped<TokenService>();

    var app = builder.Build();
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

    app.MapProductEndpoints();
    app.MapAuthEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }