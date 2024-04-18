namespace Domain

open Domain.Core
open Domain.Core.Conversion
open Microsoft.FSharp.Core
open otsom.fs.Extensions
open Domain.Repos
open shortid

module Workflows =
  [<RequireQualifiedAccess>]
  module ConversionId =
    let generate : ConversionId.Generate =
      fun () -> ShortId.Generate() |> ConversionId

  [<RequireQualifiedAccess>]
  module Conversion =
    let create (generateId: ConversionId.Generate) (saveConversion: Conversion.Save) : Create =
      fun () ->
        task {
          let newConversion: Conversion.New = { Id = generateId () }

          do! saveConversion (Conversion.New newConversion)

          return newConversion
        }

    [<RequireQualifiedAccess>]
    module New =
      let prepare
        (downloadLink: Conversion.New.InputFile.DownloadLink)
        (downloadDocument: Conversion.New.InputFile.DownloadDocument)
        (saveConversion: Conversion.Save)
        (queueConversion: Conversion.Prepared.QueueConversion)
        (queueThumbnailing: Conversion.Prepared.QueueThumbnailing)
        : Conversion.New.Prepare =
        fun conversionId file ->
          match file with
          | New.Link l -> downloadLink l
          | New.Document d -> downloadDocument d |> Task.map Ok
          |> TaskResult.map (fun downloadedFile ->
            { Id = conversionId
              InputFile = downloadedFile })
          |> TaskResult.taskTap (Conversion.Prepared >> saveConversion)
          |> TaskResult.taskTap queueConversion
          |> TaskResult.taskTap queueThumbnailing

    [<RequireQualifiedAccess>]
    module Thumbnailed =
      let complete (saveConversion: Conversion.Save) : Thumbnailed.Complete =
        fun conversion video ->
          let completedConversion: Conversion.Completed =
            { Id = conversion.Id
              OutputFile = video |> Video
              ThumbnailFile = conversion.ThumbnailName |> Thumbnail }

          saveConversion (Conversion.Completed completedConversion)
          |> Task.map (fun _ -> completedConversion)

    [<RequireQualifiedAccess>]
    module Prepared =
      let saveThumbnail (saveConversion: Conversion.Save) : Prepared.SaveThumbnail =
        fun conversion thumbnail ->
          let thumbnailedConversion: Thumbnailed =
            { Id = conversion.Id
              ThumbnailName = thumbnail }

          saveConversion (Conversion.Thumbnailed thumbnailedConversion)
          |> Task.map (fun _ -> thumbnailedConversion)

      let saveVideo (saveConversion: Conversion.Save) : Prepared.SaveVideo =
        fun conversion video ->
          let convertedConversion: Conversion.Converted =
            { Id = conversion.Id
              OutputFile = video }

          saveConversion (Conversion.Converted convertedConversion)
          |> Task.map (fun _ -> convertedConversion)

    [<RequireQualifiedAccess>]
    module Converted =
      let complete (saveConversion: Conversion.Save) : Converted.Complete =
        fun conversion thumbnail ->
          let completedConversion: Conversion.Completed =
            { Id = conversion.Id
              OutputFile = (conversion.OutputFile |> Video)
              ThumbnailFile = (thumbnail |> Thumbnail) }

          saveConversion (Conversion.Completed completedConversion)
          |> Task.map (fun _ -> completedConversion)
