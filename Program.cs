using Microsoft.EntityFrameworkCore;
using Sbt.Data;

namespace Sbt;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        string connectionString = builder.Configuration.GetConnectionString("Cosmos_ConnectionString")
            ?? throw new InvalidOperationException("Connection string not found in configuration.");

        builder.Services.AddDbContext<DemoContext>(options =>
        {
            options.UseCosmos(connectionString, databaseName: "Sbt-EF");
        });

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        else
        {
            app.UseMigrationsEndPoint();
        }

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<DemoContext>();
            context.Database.EnsureCreated();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}