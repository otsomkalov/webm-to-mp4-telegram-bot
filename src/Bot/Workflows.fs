﻿module Bot.Workflows

open System.Text.RegularExpressions
open Bot.Domain
open System.Threading.Tasks
open Domain.Core
open Helpers
open Microsoft.Extensions.Logging
open Telegram.Core

[<RequireQualifiedAccess>]
module User =
  type Save = User -> Task<unit>
  type EnsureExists = User -> Task<unit>

[<RequireQualifiedAccess>]
module UserConversion =
  type Save = UserConversion -> unit Task

[<RequireQualifiedAccess>]
module Conversion =
  [<RequireQualifiedAccess>]
  module New =
    type Save = Conversion.New -> unit Task

  [<RequireQualifiedAccess>]
  module Prepared =
    type Load = string -> Conversion.Prepared Task

let parseCommand (settings: Settings.InputValidationSettings) : ParseCommand =
  let linkRegex = Regex(settings.LinkRegex)

  fun message ->
    match message with
    | FromBot ->
      None |> Task.FromResult
    | Text messageText ->
      match messageText with
      | StartsWith "/start" ->
        Command.Start |> Some |> Task.FromResult
      | Regex linkRegex matches ->
        matches |> Command.Links |> Some |> Task.FromResult
      | _ ->
        None |> Task.FromResult
    | Document settings.MimeTypes doc ->
      Command.Document(doc.FileId, doc.FileName) |> Some |> Task.FromResult
    | Video settings.MimeTypes vid ->
      Command.Video(vid.FileId, vid.FileName) |> Some |> Task.FromResult
    | _ ->
      None |> Task.FromResult
