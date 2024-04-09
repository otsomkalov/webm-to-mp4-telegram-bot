namespace Telegram.Infrastructure

open Domain.Core
open Telegram.Core
open otsom.fs.Telegram.Bot.Core
open Infrastructure.Core
open Telegram.Infrastructure.Core

module Mappings =

  [<RequireQualifiedAccess>]
  module UserConversion =
    let fromDb (conversion: Database.Conversion) : UserConversion =
      { ConversionId = (conversion.Id |> ConversionId)
        UserId = (conversion.UserId |> Option.ofNullable |> Option.map UserId)
        ReceivedMessageId = (conversion.ReceivedMessageId |> UserMessageId)
        SentMessageId = BotMessageId conversion.SentMessageId
        ChatId = UserId conversion.ChatId }

    let toDb (conversion: UserConversion) : Database.Conversion =
      Database.Conversion(
        Id = (conversion.ConversionId |> ConversionId.value),
        UserId = (conversion.UserId |> Option.map UserId.value |> Option.toNullable),
        ReceivedMessageId = (conversion.ReceivedMessageId |> UserMessageId.value),
        SentMessageId = (conversion.SentMessageId |> BotMessageId.value),
        ChatId = (conversion.ChatId |> UserId.value)
      )

  [<RequireQualifiedAccess>]
  module User =
    let fromDb (user: Database.User) : User =
      { Id = UserId user.Id
        Lang = (user.Lang |> Option.ofObj) }

    let toDb (user: User) : Database.User =
      Database.User(Id = (user.Id |> UserId.value), Lang = (user.Lang |> Option.toObj))
