using OnboardingBuddy.Hubs;
using OnboardingBuddy.Services;
using OnboardingBuddy.Models;
using OnboardingBuddy.Data;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;

// Load .env file if it exists
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Load additional configuration files specified in environment variables
var additionalConfigFiles = Environment.GetEnvironmentVariable("ADDITIONAL_CONFIG_FILES");
if (!string.IsNullOrEmpty(additionalConfigFiles))
{
    var configFiles = additionalConfigFiles.Split(',', StringSplitOptions.RemoveEmptyEntries);
    foreach (var configFile in configFiles)
    {
        var fileName = configFile.Trim();
        Console.WriteLine($"Loading additional configuration file: {fileName}");
        builder.Configuration.AddJsonFile(fileName, optional: true, reloadOnChange: true);
    }
}

// Alternative: Load single config file
var singleConfigFile = Environment.GetEnvironmentVariable("ADDITIONAL_CONFIG_FILE");
if (!string.IsNullOrEmpty(singleConfigFile))
{
    Console.WriteLine($"Loading additional configuration file: {singleConfigFile}");
    builder.Configuration.AddJsonFile(singleConfigFile, optional: true, reloadOnChange: true);
}

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Configure for virtual application deployment
builder.WebHost.UseWebRoot("wwwroot");

// Add Entity Framework
builder.Services.AddDbContext<OnboardingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=192.168.0.63;Database=OnboardingBuddy;User Id=acu;Password=Test123!;TrustServerCertificate=true;"));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Add SPA services for Vue.js development
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot";
});

// Configure forwarded headers for virtual applications
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register custom services
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<ITrainingMaterialService, TrainingMaterialService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddScoped<IAzureKeyVaultService, AzureKeyVaultService>();
builder.Services.AddHttpClient();

// Add configuration
builder.Services.Configure<AIConfiguration>(
    builder.Configuration.GetSection("AIConfiguration"));

var app = builder.Build();

// Initialize database with automatic migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OnboardingDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Use forwarded headers for virtual applications
app.UseForwardedHeaders();

// Serve static files from wwwroot
app.UseStaticFiles();
app.UseSpaStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

// Configure SPA for virtual application
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";
    spa.Options.DefaultPage = "/index.html";
    
    // Temporarily disable proxy to avoid connection issues
    // if (app.Environment.IsDevelopment())
    // {
    //     // In development, proxy to the Vite dev server
    //     spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
    // }
    // In production, files are served from wwwroot via static files middleware
});

app.Run();