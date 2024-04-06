namespace Telegram

open System.Threading.Tasks
open Domain.Core
open Telegram.Core
open otsom.fs.Telegram.Bot.Core
open otsom.fs.Extensions
open Domain.Repos

module Workflows =
  type DeleteBotMessage = UserId -> BotMessageId -> Task
  type ReplyWithVideo = UserId -> UserMessageId -> Conversion.Video -> Conversion.Thumbnail -> Task<unit>

  [<RequireQualifiedAccess>]
  module UserConversion =
    type Load = ConversionId -> Task<UserConversion>

  [<RequireQualifiedAccess>]
  module User =
    type Load = UserId -> Task<User>

  type IUserConversionLoader =
    abstract member LoadUserConversion: UserConversion.Load
  type IMessageEditor =
    abstract member EditBotMessage: EditBotMessage

  type IUserLoader =
    abstract member LoadUser: User.Load

  type ICompletedConversionLoader =
    abstract member LoadCompletedConversion: Conversion.Completed.Load

  type ITranslationsLoader =
    abstract member GetLocaleTranslations: Translation.GetLocaleTranslations

  type INewConversionPreparator =
    abstract member Prepare: Conversion.New.Prepare

  let downloadFileAndQueueConversion
    (env: #IUserConversionLoader & #IMessageEditor & #IUserLoader & #ITranslationsLoader & #INewConversionPreparator)
    : DownloadFileAndQueueConversion =

    let onSuccess editMessage tran =
      fun _ -> editMessage (tran Resources.ConversionInProgress)

    let onError editMessage tran =
      fun error ->
        match error with
        | Conversion.New.DownloadLinkError.Unauthorized -> editMessage (tran Resources.NotAuthorized)
        | Conversion.New.DownloadLinkError.NotFound -> editMessage (tran Resources.NotFound)
        | Conversion.New.DownloadLinkError.ServerError -> editMessage (tran Resources.ServerError)

    fun conversionId file ->
      task {
        let! userConversion = env.LoadUserConversion conversionId

        let! tran, _ =
          userConversion.UserId
          |> Option.taskMap env.LoadUser
          |> Task.map (Option.bind (_.Lang))
          |> Task.bind env.GetLocaleTranslations

        let editMessage = env.EditBotMessage userConversion.ChatId userConversion.SentMessageId

        let onSuccess = (onSuccess editMessage tran)
        let onError = (onError editMessage tran)

        return! env.Prepare conversionId file |> TaskResult.taskEither onSuccess onError
      }

  let processConversionResult
    (env: #IUserConversionLoader & #IMessageEditor & #IUserLoader & #ITranslationsLoader)
    (loadPreparedOrThumbnailed: Conversion.PreparedOrThumbnailed.Load)
    (saveVideo: Conversion.Prepared.SaveVideo)
    (complete: Conversion.Thumbnailed.Complete)
    (queueUpload: Conversion.Completed.QueueUpload)
    : ProcessConversionResult =

    let processResult editMessage tran conversion =
      function
      | ConversionResult.Success file ->
        match conversion with
        | Choice1Of2 preparedConversion ->
          saveVideo preparedConversion file
          |> Task.bind (fun _ -> editMessage (tran Resources.VideoConverted))
        | Choice2Of2 thumbnailedConversion ->
          complete thumbnailedConversion file
          |> Task.bind queueUpload
          |> Task.bind (fun _ -> editMessage (tran Resources.Uploading))
      | ConversionResult.Error error -> editMessage error

    fun conversionId result ->
      task {
        let! userConversion = env.LoadUserConversion conversionId

        let editMessage = env.EditBotMessage userConversion.ChatId userConversion.SentMessageId

        let! tran, _ =
          userConversion.UserId
          |> Option.taskMap env.LoadUser
          |> Task.map (Option.bind (_.Lang))
          |> Task.bind env.GetLocaleTranslations

        let! conversion = loadPreparedOrThumbnailed conversionId

        return! processResult editMessage tran conversion result
      }

  let processThumbnailingResult
    (env: #IUserConversionLoader & #IMessageEditor & #IUserLoader & #ITranslationsLoader)
    (loadPreparedOrConverted: Conversion.PreparedOrConverted.Load)
    (saveThumbnail: Conversion.Prepared.SaveThumbnail)
    (complete: Conversion.Converted.Complete)
    (queueUpload: Conversion.Completed.QueueUpload)
    : ProcessThumbnailingResult =

    let processResult editMessage tran conversion =
      function
      | ConversionResult.Success file ->
        match conversion with
        | Choice1Of2 preparedConversion ->
          saveThumbnail preparedConversion file
          |> Task.bind (fun _ -> editMessage (tran Resources.ThumbnailGenerated))
        | Choice2Of2 convertedConversion ->
          complete convertedConversion file
          |> Task.bind queueUpload
          |> Task.bind (fun _ -> editMessage (tran Resources.Uploading))
      | ConversionResult.Error error -> editMessage error

    fun conversionId result ->
      task {
        let! userConversion = env.LoadUserConversion conversionId

        let editMessage = env.EditBotMessage userConversion.ChatId userConversion.SentMessageId

        let! tran, _ =
          userConversion.UserId
          |> Option.taskMap env.LoadUser
          |> Task.map (Option.bind (_.Lang))
          |> Task.bind env.GetLocaleTranslations

        let! conversion = loadPreparedOrConverted conversionId

        return! processResult editMessage tran conversion result
      }

  let uploadCompletedConversion
    (env: #IUserConversionLoader & #ICompletedConversionLoader)
    (deleteBotMessage: DeleteBotMessage)
    (replyWithVideo: ReplyWithVideo)
    (deleteVideo: Conversion.Completed.DeleteVideo)
    (deleteThumbnail: Conversion.Completed.DeleteThumbnail)
    : UploadCompletedConversion =
    fun id ->
      task {
        let! userConversion = env.LoadUserConversion id
        let! conversion = env.LoadCompletedConversion id

        do! replyWithVideo userConversion.ChatId userConversion.ReceivedMessageId conversion.OutputFile conversion.ThumbnailFile

        do! deleteVideo conversion.OutputFile
        do! deleteThumbnail conversion.ThumbnailFile
        do! deleteBotMessage userConversion.ChatId userConversion.SentMessageId
      }
