module Tests.Conversion.Thumbnailed

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Core.Conversion
open Domain.Workflows
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``Thumbnailed conversion completes with converted file`` () =
  let conversionId = Guid.NewGuid().ToString() |> ConversionId
  let testOutput = "test-output.mp4"
  let testThumbnail = "test-thumbnail.jpg"

  let expected =
    { Id = conversionId
      OutputFile = Video(testOutput)
      ThumbnailFile = Thumbnail(testThumbnail) }

  let saveConversion (conversion: Conversion) =
    conversion |> should equal (Conversion.Completed expected)
    Task.FromResult()

  let sut = Conversion.Thumbnailed.complete saveConversion

  task {
    let! result =
      sut
        { Id = conversionId
          ThumbnailName = testThumbnail }
        testOutput

    result |> should equal expected
  }
