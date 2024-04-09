namespace Telegram

open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Telegram.Bot.Types
open otsom.fs.Telegram.Bot.Core

module Core =
  type ChatId = ChatId of int64
  type UserMessageId = UserMessageId of int
  type UploadCompletedConversion = ConversionId -> Task<unit>

  type User = { Id: UserId; Lang: string option }

  [<RequireQualifiedAccess>]
  type ConversionResult =
    | Success of name: string
    | Error of error: string

  type DownloadFileAndQueueConversion = ConversionId -> Conversion.New.InputFile -> Task<unit>

  type ProcessThumbnailingResult = ConversionId -> ConversionResult -> Task<unit>
  type ProcessConversionResult = ConversionId -> ConversionResult -> Task<unit>

  type UserConversion =
    { ReceivedMessageId: UserMessageId
      SentMessageId: BotMessageId
      ConversionId: ConversionId
      UserId: UserId option
      ChatId: UserId }

  [<RequireQualifiedAccess>]
  module UserConversion =
    type QueueProcessing = UserMessageId -> UserId option -> UserId -> BotMessageId -> Conversion.New.InputFile -> Task<unit>

  type Translation = { Key: string; Value: string }

  [<RequireQualifiedAccess>]
  module Translation =
    [<Literal>]
    let DefaultLang = "en"

    type GetTranslation = string -> string
    type FormatTranslation = string * obj array -> string

    type GetLocaleTranslations = string option -> Task<GetTranslation * FormatTranslation>

  type ProcessMessage = Message -> Task<unit>

  [<RequireQualifiedAccess>]
  type Command =
    | Start
    | Links of string seq
    | Document of string * string
    | Video of string * string

  type ParseCommand = Message -> Task<Command option>