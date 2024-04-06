namespace Domain

open Domain.Core
open otsom.fs.Extensions
open Domain.Repos

module Workflows =
  [<RequireQualifiedAccess>]
  module Conversion =
    [<RequireQualifiedAccess>]
    module New =
      let prepare
        (env: #IPreparedConversionSaver)
        (downloadLink: Conversion.New.InputFile.DownloadLink)
        (downloadDocument: Conversion.New.InputFile.DownloadDocument)
        (queueConversion: Conversion.Prepared.QueueConversion)
        (queueThumbnailing: Conversion.Prepared.QueueThumbnailing)
        : Conversion.New.Prepare =
        fun conversionId file ->
          match file with
          | Conversion.New.Link l -> downloadLink l
          | Conversion.New.Document d -> downloadDocument d |> Task.map Ok
          |> TaskResult.map (fun downloadedFile ->
            { Id = conversionId
              InputFile = downloadedFile }
            : Conversion.Prepared)
          |> TaskResult.taskTap env.SavePrepared
          |> TaskResult.taskTap queueConversion
          |> TaskResult.taskTap queueThumbnailing

    [<RequireQualifiedAccess>]
    module Thumbnailed =
      let complete (env: #ICompletedConversionSaver) : Conversion.Thumbnailed.Complete =
        fun conversion video ->
          env.SaveCompleted
            { Id = conversion.Id
              OutputFile = video |> Conversion.Video
              ThumbnailFile = conversion.ThumbnailName |> Conversion.Thumbnail }

    [<RequireQualifiedAccess>]
    module Prepared =
      let saveThumbnail (env: #IThumbnailedConversionSaver) : Conversion.Prepared.SaveThumbnail =
        fun conversion thumbnail ->
          let thumbnailedConversion: Conversion.Thumbnailed =
            { Id = conversion.Id
              ThumbnailName = thumbnail }

          env.SaveThumbnailed thumbnailedConversion
          |> Task.map (fun _ -> thumbnailedConversion)

      let saveVideo (env: #IConvertedConversionSaver) : Conversion.Prepared.SaveVideo =
        fun conversion video ->
          let convertedConversion: Conversion.Converted =
            { Id = conversion.Id
              OutputFile = video }

          env.SaveConverted convertedConversion
          |> Task.map (fun _ -> convertedConversion)

    [<RequireQualifiedAccess>]
    module Converted =
      let complete (env: #ICompletedConversionSaver) : Conversion.Converted.Complete =
        fun conversion thumbnail ->
          env.SaveCompleted
            { Id = conversion.Id
              OutputFile = (conversion.OutputFile |> Conversion.Video)
              ThumbnailFile = (thumbnail |> Conversion.Thumbnail) }
