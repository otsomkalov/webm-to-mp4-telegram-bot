namespace Domain

open System.Threading.Tasks
open Domain.Core
open Microsoft.FSharp.Core

module Repos =
  [<RequireQualifiedAccess>]
  module Conversion =
    [<RequireQualifiedAccess>]
    module New =
      type Load = ConversionId -> Task<Conversion.New>

      [<RequireQualifiedAccess>]
      module InputFile =
        type DownloadLink = Conversion.New.InputLink -> Task<Result<string, Conversion.New.DownloadLinkError>>
        type DownloadDocument = Conversion.New.InputDocument -> Task<string>

    [<RequireQualifiedAccess>]
    module Prepared =
      type Save = Conversion.Prepared -> Task<unit>

      type QueueConversion = Conversion.Prepared -> Task<unit>
      type QueueThumbnailing = Conversion.Prepared -> Task<unit>

    [<RequireQualifiedAccess>]
    module Converted =
      type Save = Conversion.Converted -> Task<Conversion.Converted>

    [<RequireQualifiedAccess>]
    module Thumbnailed =
      type Save = Conversion.Thumbnailed -> Task<Conversion.Thumbnailed>

    [<RequireQualifiedAccess>]
    module Completed =
      type Save = Conversion.Completed -> Task<Conversion.Completed>

      type Load = ConversionId -> Task<Conversion.Completed>
      type DeleteVideo = Conversion.Video -> Task<unit>
      type DeleteThumbnail = Conversion.Thumbnail -> Task<unit>

      type QueueUpload = Conversion.Completed -> Task<unit>

    [<RequireQualifiedAccess>]
    module PreparedOrConverted =
      type Load = ConversionId -> Task<Conversion.PreparedOrConverted>

    [<RequireQualifiedAccess>]
    module PreparedOrThumbnailed =
      type Load = ConversionId -> Task<Conversion.PreparedOrThumbnailed>

  type IPreparedOrConvertedConversionLoader =
    abstract member LoadPreparedOrConverted: Conversion.PreparedOrConverted.Load

  type IPreparedOrThumbnailedConversionLoader =
    abstract member LoadPreparedOrThumbnailed: Conversion.PreparedOrThumbnailed.Load

  type INewConversionLoader =
    abstract member LoadNew: Conversion.New.Load

  type IPreparedConversionSaver =
    abstract member SavePrepared: Conversion.Prepared.Save

  type ICompletedConversionLoader =
    abstract member LoadCompleted: Conversion.Completed.Load

  type ICompletedConversionSaver =
    abstract member SaveCompleted: Conversion.Completed.Save

  type IConvertedConversionSaver =
    abstract member SaveConverted: Conversion.Converted.Save

  type IThumbnailedConversionSaver =
    abstract member SaveThumbnailed: Conversion.Thumbnailed.Save