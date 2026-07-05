using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MPM.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<MPM.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password",
                ["ConnectionStrings:Redis"] = "localhost:6379,password=redis_password"
            });
        });
    }
}
