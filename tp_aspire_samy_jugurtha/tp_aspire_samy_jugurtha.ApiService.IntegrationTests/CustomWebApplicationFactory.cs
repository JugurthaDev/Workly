using System;
using System.Data.Common;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using tp_aspire_samy_jugurtha.ApiService.Data;
using Microsoft.EntityFrameworkCore;

namespace tp_aspire_samy_jugurtha.ApiService.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            // Skip migrations & seeding in tests
            var dict = new Dictionary<string, string?>
            {
                ["RunMigrations"] = "false",
                ["UseInMemoryDatabase"] = "true"
            };
            cfg.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            // Ensure an IHttpContextAccessor exists for the TestAuthHandler
            services.AddHttpContextAccessor();

            // Replace authentication with our test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthScheme, _ => { });

            // Replace DbContext to ensure InMemory provider is used
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

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        return host;
    }

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
