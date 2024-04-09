namespace Infrastructure

open Domain.Core
open Infrastructure.Core

module Mappings =
  [<RequireQualifiedAccess>]
  module Conversion =
    [<RequireQualifiedAccess>]
    module New =
      let fromDb (conversion: Database.Conversion) : Conversion.New =
        match conversion.State with
        | Database.ConversionState.New -> { Id = ConversionId conversion.Id }

      let toDb (conversion: Conversion.New) : Database.Conversion =
        Database.Conversion(Id = (conversion.Id |> ConversionId.value), State = Database.ConversionState.New)

    [<RequireQualifiedAccess>]
    module Prepared =
      let fromDb (conversion: Database.Conversion) : Conversion.Prepared =
        match conversion.State with
        | Database.ConversionState.Prepared ->
          { Id = (conversion.Id |> ConversionId)
            InputFile = conversion.InputFileName }

      let toDb (conversion: Conversion.Prepared) : Database.Conversion =
        Database.Conversion(
          Id = (conversion.Id |> ConversionId.value),
          InputFileName = conversion.InputFile,
          State = Database.ConversionState.Prepared
        )

    [<RequireQualifiedAccess>]
    module Converted =
      let fromDb (conversion: Database.Conversion) : Conversion.Converted =
        match conversion.State with
        | Database.ConversionState.Converted ->
          { Id = (conversion.Id |> ConversionId)
            OutputFile = conversion.OutputFileName }

      let toDb (conversion: Conversion.Converted) : Database.Conversion =
        Database.Conversion(
          Id = (conversion.Id |> ConversionId.value),
          OutputFileName = conversion.OutputFile,
          State = Database.ConversionState.Converted
        )

    [<RequireQualifiedAccess>]
    module Thumbnailed =
      let fromDb (conversion: Database.Conversion) : Conversion.Thumbnailed =
        match conversion.State with
        | Database.ConversionState.Thumbnailed ->
          { Id = (conversion.Id |> ConversionId)
            ThumbnailName = conversion.ThumbnailFileName }

      let toDb (conversion: Conversion.Thumbnailed) : Database.Conversion =
        Database.Conversion(
          Id = (conversion.Id |> ConversionId.value),
          ThumbnailFileName = conversion.ThumbnailName,
          State = Database.ConversionState.Thumbnailed
        )

    [<RequireQualifiedAccess>]
    module PreparedOrConverted =
      let fromDb (conversion: Database.Conversion) : Conversion.PreparedOrConverted =
        match conversion.State with
        | Database.ConversionState.Prepared -> Prepared.fromDb conversion |> Choice1Of2
        | Database.ConversionState.Converted -> Converted.fromDb conversion |> Choice2Of2

    [<RequireQualifiedAccess>]
    module PreparedOrThumbnailed =
      let fromDb (conversion: Database.Conversion) : Conversion.PreparedOrThumbnailed =
        match conversion.State with
        | Database.ConversionState.Prepared -> Prepared.fromDb conversion |> Choice1Of2
        | Database.ConversionState.Thumbnailed -> Thumbnailed.fromDb conversion |> Choice2Of2

    [<RequireQualifiedAccess>]
    module Completed =
      let fromDb (conversion: Database.Conversion) : Conversion.Completed =
        match conversion.State with
        | Database.ConversionState.Completed ->
          { Id = (conversion.Id |> ConversionId)
            OutputFile = (conversion.OutputFileName |> Conversion.Video)
            ThumbnailFile = (conversion.ThumbnailFileName |> Conversion.Thumbnail) }

      let toDb (conversion: Conversion.Completed) : Database.Conversion =
        Database.Conversion(
          Id = (conversion.Id |> ConversionId.value),
          OutputFileName = (conversion.OutputFile |> Conversion.Video.value),
          ThumbnailFileName = (conversion.ThumbnailFile |> Conversion.Thumbnail.value),
          State = Database.ConversionState.Completed
        )
