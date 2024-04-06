namespace Infrastructure

open System
open System.Net.Http
open Domain.Core
open Domain.Workflows
open Infrastructure.Settings
open Microsoft.Extensions.DependencyInjection
open MongoDB.Driver
open Polly.Extensions.Http
open otsom.fs.Extensions.DependencyInjection
open Queue
open Domain.Repos
open Polly
open Infrastructure.Repos
open Infrastructure.Workflows

module Startup =
  type DomainEnv(db: IMongoDatabase) =
    interface INewConversionLoader with
      member this.LoadNew = Conversion.New.load db

    interface IPreparedConversionSaver with
      member this.SavePrepared = Conversion.Prepared.save db

    interface ICompletedConversionLoader with
      member this.LoadCompleted = Conversion.Completed.load db

    interface ICompletedConversionSaver with
      member this.SaveCompleted = Conversion.Completed.save db

    interface IThumbnailedConversionSaver with
      member this.SaveThumbnailed = Conversion.Thumbnailed.save db

    interface IConvertedConversionSaver with
      member this.SaveConverted = Conversion.Converted.save db

    interface IPreparedOrConvertedConversionLoader with
      member this.LoadPreparedOrConverted = Conversion.PreparedOrConverted.load db

    interface IPreparedOrThumbnailedConversionLoader with
      member this.LoadPreparedOrThumbnailed = Conversion.PreparedOrThumbnailed.load db

  [<Literal>]
  let private chromeUserAgent =
    "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36"

  let private retryPolicy =
    HttpPolicyExtensions
      .HandleTransientHttpError()
      .WaitAndRetryAsync(5, (fun retryAttempt -> TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))

  let addDomain (services: IServiceCollection) =
    services
      .AddHttpClient(fun (client: HttpClient) -> client.DefaultRequestHeaders.UserAgent.ParseAdd(chromeUserAgent))
      .AddPolicyHandler(retryPolicy)

    services
      .BuildSingleton<DomainEnv, IMongoDatabase>(DomainEnv)
      .BuildSingleton<Conversion.New.InputFile.DownloadLink, IHttpClientFactory, WorkersSettings>(Conversion.New.InputFile.downloadLink)
      .BuildSingleton<Conversion.New.Prepare, DomainEnv>(Conversion.New.prepare)

      // TODO: Functions of same type. How to register?
      // .BuildSingleton<Conversion.Prepared.QueueConversion, WorkersSettings>(Conversion.Prepared.queueConversion)
      // .BuildSingleton<Conversion.Prepared.QueueThumbnailing, WorkersSettings>(Conversion.Prepared.queueThumbnailing)


      .BuildSingleton<Conversion.Prepared.SaveVideo, DomainEnv>(Conversion.Prepared.saveVideo)
      .BuildSingleton<Conversion.Prepared.SaveThumbnail, DomainEnv>(Conversion.Prepared.saveThumbnail)


      .BuildSingleton<Conversion.Completed.DeleteVideo, WorkersSettings>(Conversion.Completed.deleteVideo)
      .BuildSingleton<Conversion.Completed.DeleteThumbnail, WorkersSettings>(Conversion.Completed.deleteThumbnail)
      .BuildSingleton<Conversion.Completed.QueueUpload, WorkersSettings>(Conversion.Completed.queueUpload)

      .BuildSingleton<Conversion.Thumbnailed.Complete, DomainEnv>(Conversion.Thumbnailed.complete)
      .BuildSingleton<Conversion.Converted.Complete, DomainEnv>(Conversion.Converted.complete)
