namespace Infrastructure

open MongoDB.Driver
open Domain.Repos
open Infrastructure.Mappings
open Infrastructure.Core
open otsom.fs.Extensions

module Repos =
  [<RequireQualifiedAccess>]
  module Conversion =
    [<RequireQualifiedAccess>]
    module New =
      let load (db: IMongoDatabase) =
        let collection = db.GetCollection "conversions"

        fun conversionId ->
          let filter = Builders<Database.Conversion>.Filter.Eq((fun c -> c.Id), (conversionId |> ConversionId.value))

          collection.Find(filter).SingleOrDefaultAsync()
          |> Task.map Conversion.New.fromDb

    [<RequireQualifiedAccess>]
    module Prepared =
      let save (db: IMongoDatabase) : Conversion.Prepared.Save =
        let collection = db.GetCollection "conversions"

        fun conversion ->
          let filter =
            Builders<Database.Conversion>.Filter
              .Eq((fun c -> c.Id), (conversion.Id |> ConversionId.value))

          let entity = conversion |> Conversion.Prepared.toDb
          collection.ReplaceOneAsync(filter, entity) |> Task.ignore
