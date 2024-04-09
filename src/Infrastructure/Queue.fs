namespace Infrastructure

open Azure.Storage.Queues
open Domain.Core
open Infrastructure.Helpers
open Infrastructure.Settings
open Microsoft.FSharp.Core
open otsom.fs.Extensions
open Domain.Repos
open Infrastructure.Core

module Queue =
  [<CLIMutable>]
  type UploaderMessage = { ConversionId: string }

  [<CLIMutable>]
  type DownloaderMessage =
    { ConversionId: ConversionId
      File: Conversion.New.InputFile }

  [<RequireQualifiedAccess>]
  module Conversion =

    [<RequireQualifiedAccess>]
    module New =
      let queuePreparation (workersSettings: WorkersSettings) : Conversion.New.QueuePreparation =
        fun conversionId inputFile ->
          let queueServiceClient = QueueServiceClient(workersSettings.ConnectionString)

          let queueClient =
            queueServiceClient.GetQueueClient(workersSettings.Downloader.Queue)

          let message =
            { ConversionId = conversionId
              File = inputFile }

          let messageBody = JSON.serialize message

          queueClient.SendMessageAsync(messageBody) |> Task.ignore

    [<RequireQualifiedAccess>]
    module Prepared =
      [<CLIMutable>]
      type private ConverterMessage = { Id: string; Name: string }

      let queueConversion (workersSettings: WorkersSettings) : Conversion.Prepared.QueueConversion =
        fun conversion ->
          let queueServiceClient = QueueServiceClient(workersSettings.ConnectionString)

          let queueClient =
            queueServiceClient.GetQueueClient(workersSettings.Converter.Input.Queue)

          { Id = conversion.Id |> ConversionId.value
            Name = conversion.InputFile }
          |> JSON.serialize
          |> queueClient.SendMessageAsync
          |> Task.ignore

      let queueThumbnailing (workersSettings: WorkersSettings) : Conversion.Prepared.QueueThumbnailing =
        fun conversion ->
          let queueServiceClient = QueueServiceClient(workersSettings.ConnectionString)

          let queueClient =
            queueServiceClient.GetQueueClient(workersSettings.Thumbnailer.Input.Queue)

          { Id = conversion.Id |> ConversionId.value
            Name = conversion.InputFile }
          |> JSON.serialize
          |> queueClient.SendMessageAsync
          |> Task.ignore

    [<RequireQualifiedAccess>]
    module Completed =
      let queueUpload (workersSettings: WorkersSettings) : Conversion.Completed.QueueUpload =
        fun conversion ->
          let queueServiceClient = QueueServiceClient(workersSettings.ConnectionString)

          let queueClient = queueServiceClient.GetQueueClient(workersSettings.Uploader.Queue)

          let messageBody =
            JSON.serialize { ConversionId = (conversion.Id |> ConversionId.value) }

          queueClient.SendMessageAsync(messageBody) |> Task.ignore
