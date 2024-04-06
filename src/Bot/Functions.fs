namespace Bot.Functions

open System.Threading.Tasks
open Bot
open Bot.Domain
open Bot.Database
open Bot.Workflows
open Domain.Workflows
open FSharp
open Infrastructure.Core
open Infrastructure.Queue
open Infrastructure.Settings
open Microsoft.AspNetCore.Http
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Core
open Telegram.Infrastructure.Startup
open Telegram.Workflows
open shortid
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open Domain.Repos
open Domain.Core

type Functions
  (
    workersSettings: WorkersSettings,
    _db: IMongoDatabase,
    _logger: ILogger<Functions>,
    sendUserMessage: SendUserMessage,
    replyToUserMessage: ReplyToUserMessage,
    inputValidationSettings: Settings.InputValidationSettings,
    loggerFactory: ILoggerFactory,
    deleteBotMessage: DeleteBotMessage,
    replyWithVideo: ReplyWithVideo,
    deleteVideo: Conversion.Completed.DeleteVideo,
    deleteThumbnail: Conversion.Completed.DeleteThumbnail,
    getLocaleTranslations: Translation.GetLocaleTranslations,
    queueUpload: Conversion.Completed.QueueUpload,
    completeThumbnailedConversion: Conversion.Thumbnailed.Complete,
    completeConvertedConversion: Conversion.Converted.Complete,
    loadPreparedOrThumbnailed: Conversion.PreparedOrThumbnailed.Load,
    loadPreparedOrConverted: Conversion.PreparedOrConverted.Load,
    saveVideo: Conversion.Prepared.SaveVideo,
    saveThumbnail: Conversion.Prepared.SaveThumbnail,
    downloadLink: Conversion.New.InputFile.DownloadLink,
    downloadDocument: Conversion.New.InputFile.DownloadDocument,
    savePreparedConversion: Conversion.Prepared.Save,
    env: TGEnv
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
    let parseCommand = Workflows.parseCommand inputValidationSettings

    let saveAndQueueConversion sentMessageId getDownloaderMessage =
      task {
        let newConversion: Conversion.New = { Id = ShortId.Generate() |> ConversionId }

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

          let getDownloaderMessage =
            fun conversionId ->
              { ConversionId = conversionId
                File = Conversion.New.InputFile.Link { Url = url } }
              : Queue.DownloaderMessage

          return! saveAndQueueConversion sentMessageId getDownloaderMessage
        }

      links |> Seq.map sendUrlToQueue |> Task.WhenAll |> Task.ignore

    let processDocument (_, tranf: Translation.FormatTranslation) fileId fileName =
      task {
        let! sentMessageId = replyToMessage (tranf (Telegram.Resources.DocumentDownload, [| fileName |]))

        let getDownloaderMessage =
          fun conversionId ->
            { ConversionId = conversionId
              File = Conversion.New.InputFile.Document { Id = fileId; Name = fileName } }
            : Queue.DownloaderMessage

        return! saveAndQueueConversion sentMessageId getDownloaderMessage
      }

    let processVideo (_, tranf: Translation.FormatTranslation) fileId fileName =
      task {
        let! sentMessageId = replyToMessage (tranf (Telegram.Resources.VideoDownload, [| fileName |]))

        let getDownloaderMessage =
          fun conversionId ->
            { ConversionId = conversionId
              File = Conversion.New.InputFile.Document { Id = fileId; Name = fileName } }
            : Queue.DownloaderMessage

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
        Logf.logfi _logger "Processing message command"

        match message.From |> Option.ofObj with
        | Some sender ->
          task {
            do! ensureUserExists (Mappings.User.fromTg sender)
            do! processCommand cmd
          }
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
    let queueConversion = Conversion.Prepared.queueConversion workersSettings
    let queueThumbnailing = Conversion.Prepared.queueThumbnailing workersSettings

    let prepareConversion =
      Conversion.New.prepare downloadLink downloadDocument savePreparedConversion queueConversion queueThumbnailing

    let downloadFileAndQueueConversion =
      downloadFileAndQueueConversion env

    downloadFileAndQueueConversion message.ConversionId message.File

  [<Function("SaveConversionResult")>]
  member this.SaveConversionResult
    (
      [<QueueTrigger("%Workers:Converter:Output:Queue%", Connection = "Workers:ConnectionString")>] message: Queue.ConverterResultMessage,
      _: FunctionContext
    ) : Task<unit> =
    let processConversionResult =
      processConversionResult
        env
        loadPreparedOrThumbnailed
        saveVideo
        completeThumbnailedConversion
        queueUpload

    processConversionResult (ConversionId message.Id) message.Result

  [<Function("SaveThumbnailingResult")>]
  member this.SaveThumbnailingResult
    (
      [<QueueTrigger("%Workers:Thumbnailer:Output:Queue%", Connection = "Workers:ConnectionString")>] message: Queue.ConverterResultMessage,
      _: FunctionContext
    ) : Task<unit> =
    let processThumbnailingResult =
      processThumbnailingResult
        env
        loadPreparedOrConverted
        saveThumbnail
        completeConvertedConversion
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
      uploadCompletedConversion env deleteBotMessage replyWithVideo deleteVideo deleteThumbnail

    uploadSuccessfulConversion conversionId
