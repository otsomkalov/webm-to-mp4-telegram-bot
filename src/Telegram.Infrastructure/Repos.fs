namespace Telegram.Infrastructure

open Domain.Core
open FSharp
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Telegram.Repos
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

module Repos =
  [<RequireQualifiedAccess>]
  module UserConversion =
    let load (db: IMongoDatabase) : UserConversion.Load =
      let collection = db.GetCollection "users-conversions"

      fun conversionId ->
        let (ConversionId conversionId) = conversionId
        let filter = Builders<Database.Conversion>.Filter.Eq((fun c -> c.Id), conversionId)

        collection.Find(filter).SingleOrDefaultAsync()
        |> Task.map Mappings.UserConversion.fromDb

    let save (db: IMongoDatabase) : UserConversion.Save =
      let collection = db.GetCollection "users-conversions"

      fun conversion ->
        let entity = conversion |> Mappings.UserConversion.toDb
        task { do! collection.InsertOneAsync(entity) }

  [<RequireQualifiedAccess>]
  module User =
    let load (db: IMongoDatabase) : User.Load =
      let collection = db.GetCollection "users"

      fun userId ->
        let userId' = userId |> UserId.value
        let filter = Builders<Database.User>.Filter.Eq((fun c -> c.Id), userId')

        collection.Find(filter).SingleOrDefaultAsync() |> Task.map Mappings.User.fromDb

    let ensureExists (db: IMongoDatabase) (loggerFactory: ILoggerFactory) : User.EnsureExists =
      let logger = loggerFactory.CreateLogger(nameof User.EnsureExists)
      let collection = db.GetCollection "users"

      fun user ->
        let userId' = user.Id |> UserId.value

        let filter = Builders<Database.User>.Filter.Eq((fun u -> u.Id), userId')

        let setOnInsert =
          [ Builders<Database.User>.Update.SetOnInsert((fun u -> u.Id), userId')
            Builders<Database.User>.Update
              .SetOnInsert((fun u -> u.Lang), (user.Lang |> Option.toObj)) ]

        Logf.logfi logger "Upserting user with id %i" userId'

        task{
          do!
            collection.UpdateOneAsync(filter, Builders.Update.Combine(setOnInsert), UpdateOptions(IsUpsert = true))
            |> Task.ignore

          Logf.logfi logger "Upserted user with id %i" userId'

          return ()
        }
