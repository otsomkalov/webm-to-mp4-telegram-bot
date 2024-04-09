namespace Telegram

open Telegram.Core
open otsom.fs.Telegram.Bot.Core

module Mappings =
  [<RequireQualifiedAccess>]
  module User =
    let fromTg (user: Telegram.Bot.Types.User) : User =
      { Id = UserId user.Id
        Lang = Option.ofObj user.LanguageCode }
