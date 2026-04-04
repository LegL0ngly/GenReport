using FastEndpoints;
using GenReport.DB.Domain.Interfaces;
using GenReport.DB.Domain.Seed;
using GenReport.Domain.DBContext;
using GenReport.Domain.Interfaces;
using GenReport.Helpers;
using GenReport.Infrastructure.Configuration;
using GenReport.Infrastructure.InMemory;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Security;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Infrastructure.SharedServices.Core.Databases;
using GenReport.Infrastructure.SharedServices.Core.Ai;
using GenReport.Middlewares;
using GenReport.Services.Implementations;
using GenReport.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

// Create a new web application builder
// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// Configuration setup
var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var applicationConfiguration = new ApplicationConfiguration();
configuration.GetSection("Configuration").Bind(applicationConfiguration);

try
{
    Log.Information("Starting GenReport API...");

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)).UseNpgsql(configuration.GetConnectionString("GenReportPostgres"),
        npgSqlOptions => {
            npgSqlOptions.CommandTimeout(applicationConfiguration.CommandTimeOut);
            npgSqlOptions.UseVector();
        }));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// Add FastEndpoints
builder.Services.AddFastEndpoints();



// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();

// Add Swagger generation
builder.Services.AddSwaggerGen();


// Register custom services
builder.Services.AddSingleton<IApplicationConfiguration>(applicationConfiguration);
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IApplicationSeeder, ApplicationDBContextSeeder>();
builder.Services.AddSingleton<IJWTTokenService, JWTTokenService>();
builder.Services.AddScoped<ITestConnectionService, TestConnectionService>();
builder.Services.AddSingleton<IChatCompletionFactory, ChatCompletionFactory>();
builder.Services.AddScoped<ITestAiConnectionService, TestAiConnectionService>();
builder.Services.AddScoped<IIntentClassifierService, IntentClassifierService>();
builder.Services.AddScoped<ITokenCountService, TokenCountService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ISchemaSearchService, SchemaSearchService>();

// In-memory AI store (models + default configs, seeded at startup)
var inMemoryAiStore = new InMemoryAiStore();
builder.Services.AddSingleton<IInMemoryAiStore>(inMemoryAiStore);
builder.Services.AddSingleton(inMemoryAiStore);           // also inject concrete type for seeder
builder.Services.AddSingleton<InMemoryAiSeeder>();

// Credential Encryption — factory pattern
builder.Services.AddSingleton<ICredentialEncryptor>(new ApiKeyEncryptor(applicationConfiguration.EncryptionMasterKey));
builder.Services.AddSingleton<ICredentialEncryptor>(new PasswordEncryptor(applicationConfiguration.EncryptionMasterKey));
builder.Services.AddSingleton<ICredentialEncryptor>(new ConnectionStringEncryptor(applicationConfiguration.EncryptionMasterKey));
builder.Services.AddSingleton<ICredentialEncryptorFactory>(sp =>
    new CredentialEncryptorFactory(sp.GetRequiredService<IEnumerable<ICredentialEncryptor>>()));
builder.Services.AddHttpClient("GoService", client =>
{
    client.BaseAddress = new Uri($"http://{applicationConfiguration.GoHost}:{applicationConfiguration.GoPort}");
});
builder.Services.AddHttpClient("Anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});
builder.Services.AddHttpClient("Gemini", client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
});
builder.Services.AddHttpContextAccessor();

// add cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("allow all", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(applicationConfiguration.IssuerSigningKey)),
        ValidateIssuerSigningKey = true,
        ValidateIssuer = false,
        ValidateAudience = false,
        SaveSigninToken = false,
        RequireExpirationTime = true,
    };
    options.Events = new JwtBearerEvents
    {
        // Add user to context on successful token validation
        OnTokenValidated = res =>
        {
            HttpResponseHelpers.AddUserToContext(res.HttpContext);
            return Task.CompletedTask;
        },
        // Handle authentication failures
        OnAuthenticationFailed = err =>
        {
            Log.Error(err.Exception, "ERROR validating app: {Message}", err.Exception.Message);
            if (err.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                HttpResponseHelpers.SendTokenExpiredResponse(err.HttpContext);

            }
            return Task.CompletedTask;
        },
        // Handle forbidden requests
        OnForbidden = req =>
        {
            HttpResponseHelpers.SendInvalidTokenResponse(req.HttpContext);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GenReport",
        Version = "v1"
    });
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer jhfdkj.jkdsakjdsa.jkdsajk\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// configure logging
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
// Build the application
var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("allow all");
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints((config) =>
{
    config.Endpoints.Configurator = (endpointconfigurator) => endpointconfigurator.Options(o => o.AddEndpointFilter<PerformanceInspector>().AddEndpointFilter<GlobalExceptionHandler>().AddEndpointFilter<ModuleAuthorizationFilter>());
});
// Enable Swagger in development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

bool shouldCreateDb = args.Contains("--create-db") || applicationConfiguration.CreateDB;
bool shouldRecreateDb = args.Contains("--recreate-db") || applicationConfiguration.DeleteDB;

if (shouldRecreateDb)
{
    Log.Information("Deleting database...");
    await DeleteDB(app);
    Log.Information("Database deleted");
    shouldCreateDb = true; // if we delete, we should also create
}

// Initialize and seed the database
if (shouldCreateDb)
{
    Log.Information("Applying migrations and creating database...");
    await CreateDB(app);
    Log.Information("Database setup completed");
}

bool shouldSeedDb = args.Contains("--seed-db") || applicationConfiguration.SeedDB;
if (shouldSeedDb)
{
    await SeedDB(app);
}
await SeedInMemoryAiStore(app);


// Run the application 
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// add DB triggers to set created and updatedon values

// Helper methods for database operations

/// <summary>
/// Creates the database if it doesn't exist
/// </summary>
async Task CreateDB(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
}

/// <summary>
/// Seeds the database with initial data
/// </summary>
async Task SeedDB(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IApplicationSeeder>();
    
    if (seeder is ApplicationDBContextSeeder appSeeder)
    {
        var encryptorFactory = scope.ServiceProvider.GetRequiredService<GenReport.Infrastructure.Security.Encryption.ICredentialEncryptorFactory>();
        appSeeder.PasswordEncryptor = val => encryptorFactory.GetEncryptor(GenReport.Infrastructure.Security.Encryption.CredentialType.Password).Encrypt(val);
        appSeeder.ConnectionStringEncryptor = val => encryptorFactory.GetEncryptor(GenReport.Infrastructure.Security.Encryption.CredentialType.ConnectionString).Encrypt(val);
        appSeeder.ApiKeyEncryptor = val => encryptorFactory.GetEncryptor(GenReport.Infrastructure.Security.Encryption.CredentialType.ApiKey).Encrypt(val);
    }
    
    await seeder.SeedMandatoryTables();
    await seeder.RunScripts();
    if (applicationConfiguration.SeedDB)
        await seeder.Seed();
}

/// <summary>
/// Seeds the in-memory AI store with provider models (from OpenRouter) and default configs.
/// </summary>
async Task SeedInMemoryAiStore(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<InMemoryAiSeeder>();
    await seeder.SeedAsync();
}

/// <summary>
/// Deletes the database
/// </summary>
/// <param name="app">The application.</param>
/// <returns></returns>
async Task DeleteDB(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureDeletedAsync();
}








public partial class Program { }
