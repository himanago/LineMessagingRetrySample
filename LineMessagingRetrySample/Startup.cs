using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(LineMessagingRetrySample.Startup))]
namespace LineMessagingRetrySample
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("local.settings.json", true)
                .AddEnvironmentVariables()
                .Build();

            var settings = config.GetSection(nameof(LineBotSettings)).Get<LineBotSettings>();

            builder.Services
                .AddSingleton(settings)
                .AddHttpClient("line", c => c.BaseAddress = new Uri("https://api.line.me"));
        }
    }
}