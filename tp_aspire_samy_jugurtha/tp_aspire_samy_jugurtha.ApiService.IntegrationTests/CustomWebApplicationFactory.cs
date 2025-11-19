using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using tp_aspire_samy_jugurtha.ApiService.Data;

namespace tp_aspire_samy_jugurtha.ApiService.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["RunMigrations"] = "false",
                ["UseInMemoryDatabase"] = "true"
            };
            cfg.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthScheme, options =>
            {
                options.TimeProvider = TimeProvider.System;
            });

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WorklyDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            
            var efProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<WorklyDbContext>(options =>
            {
                options.UseInMemoryDatabase("WorklyTests");
                options.UseInternalServiceProvider(efProvider);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder) => base.CreateHost(builder);

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
