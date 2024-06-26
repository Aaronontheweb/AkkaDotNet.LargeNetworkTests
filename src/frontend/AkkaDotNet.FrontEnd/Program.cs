﻿using Akka.Actor;
using Akka.Hosting;
using AkkaDotNet.FrontEnd.Actors;
using AkkaDotNet.Infrastructure;
using AkkaDotNet.Infrastructure.Actors;
using AkkaDotNet.Infrastructure.Configuration;
using AkkaDotNet.Infrastructure.Logging;
using AkkaDotNet.Infrastructure.OpenTelemetry;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Logging.ClearProviders().AddConsole().AddSerilog().AddFilter(null, LogLevel.Warning);

var akkaConfiguration = builder.Configuration.GetRequiredSection(nameof(StressOptions)).Get<StressOptions>() ?? new StressOptions();

builder.Services.AddAkka(ActorSystemConstants.ActorSystemName, configurationBuilder =>
{
    configurationBuilder.WithStressCluster(akkaConfiguration,
        new[] { ActorSystemConstants.FrontendRole, ActorSystemConstants.DistributedPubSubRole });
    configurationBuilder.WithSerilog(akkaConfiguration.SerilogOptions);
    configurationBuilder.WithReadyCheckActors();
    if (akkaConfiguration.DistributedPubSubOptions.Enabled)
    {
        configurationBuilder.StartActors((system, registry) =>
        {
            var cluster = Akka.Cluster.Cluster.Get(system);
            cluster.RegisterOnMemberUp(() =>
            {
                system.ActorOf(Props.Create(() => new PingerActor()), "pinger");
            });
        });
    }
});

builder.Services.WithOpenTelemetry();

var app = builder.Build();

// per https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus/README.md
app.UseRouting();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    
    endpoints.MapGet("/ready", async (ActorRegistry registry) =>
    {
        var readyCheck = registry.Get<ReadyCheckActor>();
        var checkResult = await readyCheck.Ask<ReadyResult>(ReadyCheck.Instance, TimeSpan.FromSeconds(3));
        //if (checkResult.IsReady)
            return Results.StatusCode(200);
        //return Results.StatusCode(500);
    });

});


await app.RunAsync();