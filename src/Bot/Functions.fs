﻿namespace Bot.Functions

open System.Net.Http
open System.Threading.Tasks
open Bot
open Bot.Domain
open Bot.Database
open Bot.Workflows
open Domain.Workflows
open FSharp
open Infrastructure.Queue
open Infrastructure.Settings
open Microsoft.AspNetCore.Http
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Core
open Telegram.Infrastructure.Workflows
open Telegram.Workflows
open shortid
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open Infrastructure.Workflows
open Domain.Deps
open Domain.Core

type Functions
  (
    workersSettings: WorkersSettings,
    _bot: ITelegramBotClient,
    _db: IMongoDatabase,
    _httpClientFactory: IHttpClientFactory,
    _logger: ILogger<Functions>,
    sendUserMessage: SendUserMessage,
    replyToUserMessage: ReplyToUserMessage,
    editBotMessage: EditBotMessage,
    inputValidationSettings: Settings.InputValidationSettings,
    loggerFactory: ILoggerFactory,
    loadUserConversion: UserConversion.Load,
    loadCompletedConversion: Conversion.Completed.Load,
    deleteBotMessage: DeleteBotMessage,
    replyWithVideo: ReplyWithVideo,
    deleteVideo: Conversion.Completed.DeleteVideo,
    deleteThumbnail: Conversion.Completed.DeleteThumbnail,
    getLocaleTranslations: Translation.GetLocaleTranslations,
    queueUpload: Conversion.Completed.QueueUpload,
    loadUser: User.Load,
    saveCompletedConversion: Conversion.Completed.Save
  ) =

  let sendDownloaderMessage = Queue.sendDownloaderMessage workersSettings _logger

  let processMessage (message: Message) =
    let chatId = message.Chat.Id |> UserId
    let userId = message.From |> Option.ofObj |> Option.map (_.Id >> UserId)
    let sendMessage = sendUserMessage chatId
    let replyToMessage = replyToUserMessage chatId message.MessageId
    let saveUserConversion = UserConversion.save _db
    let saveConversion = Conversion.New.save _db

    let ensureUserExists = User.ensureExists _db loggerFactory
    let parseCommand = Workflows.parseCommand inputValidationSettings loggerFactory

    let saveAndQueueConversion sentMessageId getDownloaderMessage =
      task {
        let newConversion: Domain.Conversion.New = { Id = ShortId.Generate() }

        do! saveConversion newConversion

        let userConversion: UserConversion =
          { ConversionId = newConversion.Id
            UserId = userId
            SentMessageId = sentMessageId
            ReceivedMessageId = UserMessageId message.MessageId
            ChatId = chatId }

        do! saveUserConversion userConversion

        let message = getDownloaderMessage newConversion.Id

        return! sendDownloaderMessage message
      }

    let processLinks (_, tranf: Translation.FormatTranslation) links =
      let sendUrlToQueue (url: string) =
        task {
          let! sentMessageId = replyToMessage (tranf (Telegram.Resources.LinkDownload, [| url |]))

          let getDownloaderMessage: string -> Queue.DownloaderMessage =
            fun conversionId ->
              { ConversionId = conversionId
                File = Queue.File.Link url }

          return! saveAndQueueConversion sentMessageId getDownloaderMessage
        }

      links |> Seq.map sendUrlToQueue |> Task.WhenAll |> Task.ignore

    let processDocument (_, tranf: Translation.FormatTranslation) fileId fileName =
      task {
        let! sentMessageId = replyToMessage (tranf (Telegram.Resources.DocumentDownload, [| fileName |]))

        let getDownloaderMessage: string -> Queue.DownloaderMessage =
          fun conversionId ->
            { ConversionId = conversionId
              File = Queue.File.Document(fileId, fileName) }

        return! saveAndQueueConversion sentMessageId getDownloaderMessage
      }

    let processVideo (_, tranf: Translation.FormatTranslation) fileId fileName =
      task {
        let! sentMessageId = replyToMessage (tranf (Telegram.Resources.VideoDownload, [| fileName |]))

        let getDownloaderMessage: string -> Queue.DownloaderMessage =
          fun conversionId ->
            { ConversionId = conversionId
              File = Queue.File.Document(fileId, fileName) }

        return! saveAndQueueConversion sentMessageId getDownloaderMessage
      }

    let processCommand =
      fun cmd ->
        task {
          Logf.logfi _logger "Processing command"

          let! tran, tranf =
            message.From
            |> Option.ofObj
            |> Option.bind (_.LanguageCode >> Option.ofObj)
            |> getLocaleTranslations

          return!
            match cmd with
            | Command.Start -> sendMessage (tran Telegram.Resources.Welcome)
            | Command.Links links -> processLinks (tran, tranf) links
            | Command.Document(fileId, fileName) -> processDocument (tran, tranf) fileId fileName
            | Command.Video(fileId, fileName) -> processVideo (tran, tranf) fileId fileName
        }

    let processMessage' =
      function
      | None -> Task.FromResult()
      | Some cmd ->
        match message.From |> Option.ofObj with
        | Some sender ->
          ensureUserExists (Mappings.User.fromTg sender)
          |> Task.bind (fun () -> processCommand cmd)
        | None -> processCommand cmd

    parseCommand message |> Task.bind processMessage'

  let handleUpdate (update: Update) =
    match update.Type with
    | UpdateType.Message -> processMessage update.Message
    | UpdateType.ChannelPost -> processMessage update.ChannelPost
    | _ -> Task.FromResult()

  [<Function("HandleUpdate")>]
  member this.HandleUpdate([<HttpTrigger("POST", Route = "telegram")>] request: HttpRequest, [<FromBody>] update: Update) : Task<unit> =
    task {
      try
        do! handleUpdate update

        return ()
      with e ->
        Logf.elogfe _logger e "Error during processing an update:"
        return ()
    }

  [<Function("Downloader")>]
  member this.DownloadFile
    (
      [<QueueTrigger("%Workers:Downloader:Queue%", Connection = "Workers:ConnectionString")>] message: Queue.DownloaderMessage,
      _: FunctionContext
    ) : Task<unit> =
    let sendConverterMessage = Queue.sendConverterMessage workersSettings
    let sendThumbnailerMessage = Queue.sendThumbnailerMessage workersSettings
    let loadNewConversion = Conversion.New.load _db
    let downloadLink = HTTP.downloadLink _httpClientFactory workersSettings
    let downloadFile = Telegram.downloadDocument _bot workersSettings
    let savePreparedConversion = Conversion.Prepared.save _db

    let downloadFile file =
      match file with
      | Queue.File.Document(id, name) -> downloadFile id name |> Task.map Ok
      | Queue.File.Link link -> downloadLink link

    let conversionId = ConversionId message.ConversionId

    task {
      let! userConversion = loadUserConversion conversionId

      let! tran, _ =
        userConversion.UserId
        |> Option.taskMap loadUser
        |> Task.map (Option.bind (fun u -> u.Lang))
        |> Task.bind getLocaleTranslations

      let editMessage = editBotMessage userConversion.ChatId userConversion.SentMessageId

      let! conversion = loadNewConversion message.ConversionId

      return!
        message.File
        |> downloadFile
        |> Task.bind (function
          | Ok file ->
            task {
              let preparedConversion: Conversion.Prepared =
                { Id = message.ConversionId
                  InputFile = file }

              do! savePreparedConversion preparedConversion

              let converterMessage: Queue.ConverterMessage = { Id = conversion.Id; Name = file }

              do! sendConverterMessage converterMessage

              let thumbnailerMessage: Queue.ConverterMessage = { Id = conversion.Id; Name = file }

              do! sendThumbnailerMessage thumbnailerMessage

              do! editMessage (tran Telegram.Resources.ConversionInProgress)
            }
          | Result.Error(HTTP.DownloadLinkError.Unauthorized) -> editMessage (tran Telegram.Resources.NotAuthorized)
          | Result.Error(HTTP.DownloadLinkError.NotFound) -> editMessage (tran Telegram.Resources.NotFound)
          | Result.Error(HTTP.DownloadLinkError.ServerError) -> editMessage (tran Telegram.Resources.ServerError))
    }

  [<Function("SaveConversionResult")>]
  member this.SaveConversionResult
    (
      [<QueueTrigger("%Workers:Converter:Output:Queue%", Connection = "Workers:ConnectionString")>] message: Queue.ConverterResultMessage,
      _: FunctionContext
    ) : Task<unit> =
    let loadPreparedOrThumbnailed = Conversion.PreparedOrThumbnailed.load _db
    let saveConvertedConversion = Conversion.Converted.save _db
    let saveVideo = Conversion.Prepared.saveVideo saveConvertedConversion
    let complete = Conversion.Thumbnailed.complete saveCompletedConversion

    let conversionId = ConversionId message.Id

    let processConversionResult =
      processConversionResult
        loadUserConversion
        editBotMessage
        loadPreparedOrThumbnailed
        loadUser
        getLocaleTranslations
        saveVideo
        complete
        queueUpload

    processConversionResult conversionId message.Result

  [<Function("SaveThumbnailingResult")>]
  member this.SaveThumbnailingResult
    (
      [<QueueTrigger("%Workers:Thumbnailer:Output:Queue%", Connection = "Workers:ConnectionString")>] message: Queue.ConverterResultMessage,
      _: FunctionContext
    ) : Task<unit> =
    let loadPreparedOrConverted = Conversion.PreparedOrConverted.load _db
    let saveThumbnailedConversion = Conversion.Thumbnailed.save _db
    let saveThumbnail = Conversion.Prepared.saveThumbnail saveThumbnailedConversion
    let complete = Conversion.Converted.complete saveCompletedConversion

    let processThumbnailingResult =
      processThumbnailingResult
        loadUserConversion
        editBotMessage
        loadPreparedOrConverted
        loadUser
        getLocaleTranslations
        saveThumbnail
        complete
        queueUpload

    processThumbnailingResult (ConversionId message.Id) message.Result

  [<Function("Uploader")>]
  member this.Upload
    (
      [<QueueTrigger("%Workers:Uploader:Queue%", Connection = "Workers:ConnectionString")>] message: UploaderMessage,
      _: FunctionContext
    ) : Task =
    let conversionId = message.ConversionId |> ConversionId

    let uploadSuccessfulConversion =
      uploadCompletedConversion loadUserConversion loadCompletedConversion deleteBotMessage replyWithVideo deleteVideo deleteThumbnail

    uploadSuccessfulConversion conversionId
