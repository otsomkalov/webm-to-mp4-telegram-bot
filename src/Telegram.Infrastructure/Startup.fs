namespace Telegram.Infrastructure

open Domain.Repos
open Infrastructure.Repos
open Infrastructure.Settings
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Telegram.Bot
open Telegram.Core
open Telegram.Infrastructure.Workflows
open Telegram.Workflows
open otsom.fs.Extensions.DependencyInjection
open otsom.fs.Telegram.Bot.Core
open Infrastructure.Workflows
open Domain.Core
open Domain.Workflows

module Startup =
  type TGEnv(db: IMongoDatabase, editBotMessage: EditBotMessage, loggerFactory: ILoggerFactory, prepareConversion: Conversion.New.Prepare) =
    interface IUserConversionLoader with
      member this.LoadUserConversion = UserConversion.load db

    interface IMessageEditor with
      member this.EditBotMessage = editBotMessage

    interface IUserLoader with
      member this.LoadUser = User.load db

    interface ICompletedConversionLoader with
      member this.LoadCompletedConversion = Conversion.Completed.load db

    interface ITranslationsLoader with
      member this.GetLocaleTranslations = Translation.getLocaleTranslations db loggerFactory

    interface INewConversionPreparator with
      member this.Prepare = prepareConversion

  let addTelegram (services: IServiceCollection) =
    services
      .BuildSingleton<UserConversion.Load, IMongoDatabase>(UserConversion.load)
      .BuildSingleton<DeleteBotMessage, ITelegramBotClient>(deleteBotMessage)
      .BuildSingleton<ReplyWithVideo, WorkersSettings, ITelegramBotClient>(replyWithVideo)
      .BuildSingleton<Translation.GetLocaleTranslations, IMongoDatabase, ILoggerFactory>(Translation.getLocaleTranslations)
      .BuildSingleton<User.Load, IMongoDatabase>(User.load)
      .BuildSingleton<Conversion.New.InputFile.DownloadDocument, ITelegramBotClient, WorkersSettings>(Conversion.New.InputFile.downloadDocument)
