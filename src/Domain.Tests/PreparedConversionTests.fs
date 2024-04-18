module Tests.Conversion.Prepared

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Core.Conversion
open Domain.Workflows
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``Converted file successfully added to Prepared conversion`` () =
  let conversionId = Guid.NewGuid().ToString() |> ConversionId
  let testInputFile = "test-file.webm"
  let testOutput = "test-output.mp4"

  let expected = {
    Id = conversionId
    OutputFile = testOutput
  }

  let saveConversion (conversion: Conversion) =
    conversion |> should equal (Conversion.Converted expected)
    Task.FromResult()

  let sut = Conversion.Prepared.saveVideo saveConversion

  task {
    let! result = sut {Id = conversionId; InputFile = testInputFile } testOutput

    result |> should equal expected
  }

[<Fact>]
let ``Thumbnail successfully added to Prepared conversion`` () =
  let conversionId = Guid.NewGuid().ToString() |> ConversionId
  let testInputFile = "test-file.webm"
  let testThumbnail = "test-thumbnail.jpg"

  let expected = {
    Id = conversionId
    ThumbnailName = testThumbnail
  }

  let saveConversion (conversion: Conversion) =
    conversion |> should equal (Conversion.Thumbnailed expected)
    Task.FromResult()

  let sut = Conversion.Prepared.saveThumbnail saveConversion

  task {
    let! result = sut {Id = conversionId; InputFile = testInputFile } testThumbnail

    result |> should equal expected
  }