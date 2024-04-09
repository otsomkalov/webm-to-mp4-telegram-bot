namespace Telegram.Infrastructure

open System.Text.RegularExpressions
open Azure.Storage.Blobs
open Domain.Core
open FSharp
open Infrastructure.Helpers
open Infrastructure.Settings
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Telegram.Bot
open Telegram.Core
open Telegram.Bot.Types
open Telegram.Infrastructure.Settings
open Telegram.Workflows
open Telegram.Infrastructure.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open System.Threading.Tasks
open System
open Domain.Repos
open Telegram.Infrastructure.Helpers
open otsom.fs.Extensions.String

module Workflows =
  let deleteBotMessage (bot: ITelegramBotClient) : DeleteBotMessage =
    fun userId messageId -> bot.DeleteMessageAsync((userId |> UserId.value |> ChatId), (messageId |> BotMessageId.value))

  let replyWithVideo (workersSettings: WorkersSettings) (bot: ITelegramBotClient) : ReplyWithVideo =
    let blobServiceClient = BlobServiceClient(workersSettings.ConnectionString)

    fun userId messageId ->
      fun video thumbnail ->
        let (Conversion.Video video) = video
        let (Conversion.Thumbnail thumbnail) = thumbnail

        let videoContainer =
          blobServiceClient.GetBlobContainerClient workersSettings.Converter.Output.Container

        let thumbnailContainer =
          blobServiceClient.GetBlobContainerClient workersSettings.Thumbnailer.Output.Container

        let videoBlob = videoContainer.GetBlobClient(video)
        let thumbnailBlob = thumbnailContainer.GetBlobClient(thumbnail)

        [ videoBlob.DownloadStreamingAsync(); thumbnailBlob.DownloadStreamingAsync() ]
        |> Task.WhenAll
        |> Task.bind (fun [| videoStreamResponse; thumbnailStreamResponse |] ->
          bot.SendVideoAsync(
            (userId |> UserId.value |> ChatId),
            InputFileStream(videoStreamResponse.Value.Content, video),
            caption = "🇺🇦 Help the Ukrainian army fight russian and belarus invaders: https://savelife.in.ua/en/donate/",
            replyToMessageId = (messageId |> UserMessageId.value),
            thumbnail = InputFileStream(thumbnailStreamResponse.Value.Content, thumbnail),
            disableNotification = true
          ))
        |> Task.ignore

  let parseCommand (settings: InputValidationSettings) : ParseCommand =
    let linkRegex = Regex(settings.LinkRegex)

    fun message ->
      match message with
      | FromBot -> None |> Task.FromResult
      | Text messageText ->
        match messageText with
        | StartsWith "/start" -> Command.Start |> Some |> Task.FromResult
        | Regex linkRegex matches -> matches |> Command.Links |> Some |> Task.FromResult
        | _ -> None |> Task.FromResult
      | Document settings.MimeTypes doc -> Command.Document(doc.FileId, doc.FileName) |> Some |> Task.FromResult
      | Video settings.MimeTypes vid -> Command.Video(vid.FileId, vid.FileName) |> Some |> Task.FromResult
      | _ -> None |> Task.FromResult

[<RequireQualifiedAccess>]
module Conversion =

  [<RequireQualifiedAccess>]
  module New =
    [<RequireQualifiedAccess>]
    module InputFile =
      let downloadDocument (bot: ITelegramBotClient) (workersSettings: WorkersSettings) : Conversion.New.InputFile.DownloadDocument =
        fun document ->
          task {
            use! converterBlobStream = Storage.getBlobStream workersSettings document.Name workersSettings.Converter.Input.Container

            do! bot.GetInfoAndDownloadFileAsync(document.Id, converterBlobStream) |> Task.ignore

            use! thumbnailerBlobStream = Storage.getBlobStream workersSettings document.Name workersSettings.Thumbnailer.Input.Container

            do!
              bot.GetInfoAndDownloadFileAsync(document.Id, thumbnailerBlobStream)
              |> Task.ignore

            return document.Name
          }

[<RequireQualifiedAccess>]
module Translation =
  let private loadTranslationsMap (collection: IMongoCollection<Database.Translation>) key =
    collection.Find(fun t -> t.Lang = key).ToListAsync()
    |> Task.map (
      Seq.groupBy (_.Key)
      >> Seq.map (fun (key, translations) -> (key, translations |> Seq.map (_.Value) |> Seq.head))
      >> Map.ofSeq
    )

  let private formatWithFallback formats fallback =
    fun (key, args) ->
      match formats |> Map.tryFind key with
      | Some fmt -> String.Format(fmt, args)
      | None -> fallback

  let private loadDefaultTranslations (collection: IMongoCollection<_>) logger =
    fun () ->
      task {
        Logf.logfi logger "Loading default translations"
        let! translations = loadTranslationsMap collection Translation.DefaultLang
        Logf.logfi logger "Default translations map loaded from DB"

        let getTranslation =
          fun key -> translations |> Map.tryFind key |> Option.defaultValue key

        let formatTranslation =
          fun (key, args) -> formatWithFallback translations key (key, args)

        return (getTranslation, formatTranslation)
      }

  let getLocaleTranslations (db: IMongoDatabase) (loggerFactory: ILoggerFactory) : Translation.GetLocaleTranslations =
    let logger = loggerFactory.CreateLogger(nameof Translation.GetLocaleTranslations)
    let collection = db.GetCollection "resources"
    let getDefaultTranslations = loadDefaultTranslations collection logger

    function
    | Some l when l <> Translation.DefaultLang ->
      task {
        let! tran, tranf = getDefaultTranslations ()

        Logf.logfi logger "Loading translations for lang %s{Lang}" l

        let! localeTranslations = loadTranslationsMap collection l

        Logf.logfi logger "Translations for lang %s{Lang} is loaded" l

        let getTranslation: Translation.GetTranslation =
          fun key -> localeTranslations |> Map.tryFind key |> Option.defaultValue (tran key)

        let formatTranslation: Translation.FormatTranslation =
          fun (key, args) -> formatWithFallback localeTranslations (tranf (key, args)) (key, args)

        return (getTranslation, formatTranslation)
      }
    | _ -> getDefaultTranslations ()
