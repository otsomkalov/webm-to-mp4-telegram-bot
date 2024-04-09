namespace Domain

open System.Threading.Tasks

module Core =
  type ConversionId = ConversionId of string

  [<RequireQualifiedAccess>]
  module Conversion =
    type New = { Id: ConversionId }

    type Create = unit -> Task<New>

    type Prepared = { Id: ConversionId; InputFile: string }

    type Converted =
      { Id: ConversionId; OutputFile: string }

    type Thumbnailed =
      { Id: ConversionId
        ThumbnailName: string }

    type PreparedOrConverted = Choice<Prepared, Converted>
    type PreparedOrThumbnailed = Choice<Prepared, Thumbnailed>

    type Video = Video of string
    type Thumbnail = Thumbnail of string

    type Completed =
      { Id: ConversionId
        OutputFile: Video
        ThumbnailFile: Thumbnail }

    [<RequireQualifiedAccess>]
    module New =
      type InputLink = { Url: string }
      type InputDocument = { Id: string; Name: string }

      type InputFile =
        | Link of InputLink
        | Document of InputDocument

      [<RequireQualifiedAccess>]
      type DownloadLinkError =
        | Unauthorized
        | NotFound
        | ServerError

      type Prepare = ConversionId -> InputFile -> Task<Result<Prepared, DownloadLinkError>>

      type QueuePreparation = ConversionId -> InputFile -> Task<unit>

    [<RequireQualifiedAccess>]
    module Prepared =
      type SaveThumbnail = Prepared -> string -> Task<Thumbnailed>
      type SaveVideo = Prepared -> string -> Task<Converted>

    [<RequireQualifiedAccess>]
    module Converted =
      type Complete = Converted -> string -> Task<Completed>

    [<RequireQualifiedAccess>]
    module Thumbnailed =
      type Complete = Thumbnailed -> string -> Task<Completed>
