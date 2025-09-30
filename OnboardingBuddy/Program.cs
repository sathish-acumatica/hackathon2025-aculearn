using OnboardingBuddy.Hubs;
using OnboardingBuddy.Services;
using OnboardingBuddy.Models;
using OnboardingBuddy.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Configure for virtual application deployment
builder.WebHost.UseWebRoot("wwwroot");

// Add Entity Framework
builder.Services.AddDbContext<OnboardingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=onboarding.db"));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Add SPA services for Vue.js development
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "ClientApp/dist";
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
    
    if (app.Environment.IsDevelopment())
    {
        // In development, proxy to the Vite dev server
        spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
    }
    // In production, files are served from wwwroot via static files middleware
});

app.Run();