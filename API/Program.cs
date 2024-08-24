using Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<StoreContext>(opt => 
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

// Ensure the database is ready before starting the application
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<Program>();
    try
    {
        var context = services.GetRequiredService<StoreContext>();

        // Retry logic to wait for the database to be ready
        const int maxRetries = 10;
        int retryCount = 0;
        bool connected = false;

        while (retryCount < maxRetries && !connected)
        {
            try
            {
                logger.LogInformation("Attempting to connect to the database...");
                context.Database.Migrate();  // Apply any pending migrations and create the database if it doesn't exist
                connected = true;
                logger.LogInformation("Connected to the database successfully.");
            }
            catch (SqlException)
            {
                retryCount++;
                logger.LogWarning($"Failed to connect to the database. Retrying... ({retryCount}/{maxRetries})");
                System.Threading.Thread.Sleep(2000);  // Wait for 2 seconds before retrying
            }
        }

        if (!connected)
        {
            throw new Exception("Unable to connect to the database after multiple attempts.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during migration.");
        throw;
    }
}

// Configure the HTTP request pipeline.
app.MapControllers();

app.Run();
