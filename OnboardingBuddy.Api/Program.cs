using OnboardingBuddy.Api.Hubs;
using OnboardingBuddy.Api.Services;
using OnboardingBuddy.Api.Models;
using OnboardingBuddy.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSpaStaticFiles();
app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

// Configure SPA to serve built files
app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";
    spa.Options.DefaultPage = "/index.html";
    
    // In production, serve the built files from the dist folder
    if (!app.Environment.IsDevelopment())
    {
        spa.Options.DefaultPageStaticFileOptions = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Cache static assets for 1 year
                if (ctx.File.Name.EndsWith(".js") || ctx.File.Name.EndsWith(".css") || 
                    ctx.File.Name.EndsWith(".woff") || ctx.File.Name.EndsWith(".woff2"))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
                }
            }
        };
    }
});

app.Run();