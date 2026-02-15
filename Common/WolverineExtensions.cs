using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Common
{
    public static class WolverineExtensions
    {
        public static async Task UseWolverineWithRabbitMqAsync(
            this IHostApplicationBuilder builder, 
            IConfiguration config, 
            Action<WolverineOptions> configureMessaging)
        {
            var retryPolicy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        Console.WriteLine($"Retry attempt {retryCount} failed.  Retrying in {timeSpan.Seconds} seconds...");
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                var endpoint = builder.Configuration.GetConnectionString("messaging")
                    ?? throw new InvalidOperationException("messaging connection string not found");
                // Attempt to connect to RabbitMQ
                var factory = new ConnectionFactory() { Uri = new Uri(endpoint) };
                await using var connection = await factory.CreateConnectionAsync();
            });

            builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
            {
                traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(builder.Environment.ApplicationName))
                    .AddSource("Wolverine");
            });

            builder.UseWolverine(opts =>
            {
                opts.UseRabbitMqUsingNamedConnection("messaging")
                    .AutoProvision()
                    .DeclareExchange("questions");
                configureMessaging(opts);
            });

        }
    }
}
