namespace Telegram.Infrastructure

module Settings =
  [<CLIMutable>]
  type InputValidationSettings =
    { LinkRegex: string
      MimeTypes: string seq }

    static member SectionName = "Validation"

  [<CLIMutable>]
  type TelegramSettings =
    { Token: string
      ApiUrl: string }

    static member SectionName = "Telegram"
