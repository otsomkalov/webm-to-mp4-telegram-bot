namespace Bot

open System
open System.Reflection
open System.Text.Json
open System.Text.Json.Serialization
open Infrastructure.Helpers
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.ApplicationInsights
open otsom.fs.Telegram.Bot
open Infrastructure
open Telegram.Infrastructure

#nowarn "20"

module Startup =
  let configureWebApp (builder: IFunctionsWorkerApplicationBuilder) =
    builder.Services.Configure<JsonSerializerOptions>(fun opts -> JSON.options.AddToJsonSerializerOptions(opts))

    ()

  let configureAppConfiguration _ (configBuilder: IConfigurationBuilder) =

    configBuilder.AddUserSecrets(Assembly.GetExecutingAssembly(), true)

    ()

  let configureLogging (builder: ILoggingBuilder) =
    builder.AddFilter<ApplicationInsightsLoggerProvider>(String.Empty, LogLevel.Information)

    ()

  let private configureServices _ (services: IServiceCollection) =
    services.AddApplicationInsightsTelemetryWorkerService()
    services.ConfigureFunctionsApplicationInsights()

    services
    |> Startup.addTelegramBotCore
    |> Startup.addDomain
    |> Startup.addTelegram

    services.AddMvcCore().AddNewtonsoftJson()

    ()

  let host =
    HostBuilder()
      .ConfigureFunctionsWebApplication(configureWebApp)
      .ConfigureAppConfiguration(configureAppConfiguration)
      .ConfigureLogging(configureLogging)
      .ConfigureServices(configureServices)
      .Build()

  // If using the Cosmos, Blob or Tables extension, you will need configure the extensions manually using the extension methods below.
  // Learn more about this here: https://go.microsoft.com/fwlink/?linkid=2245587
  // ConfigureFunctionsWorkerDefaults(fun (context: HostBuilderContext) (appBuilder: IFunctionsWorkerApplicationBuilder) ->
  //     appBuilder.ConfigureCosmosDBExtension() |> ignore
  //     appBuilder.ConfigureBlobStorageExtension() |> ignore
  //     appBuilder.ConfigureTablesExtension() |> ignore
  // ) |> ignore


  host.Run()
