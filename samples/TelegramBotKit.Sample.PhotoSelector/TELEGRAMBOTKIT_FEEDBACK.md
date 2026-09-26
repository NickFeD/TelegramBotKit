# TelegramBotKit 0.4.0-preview feedback

- Polling hosting, scoped middleware, delegate routing, callback parsing, DI access, and the `IMessageSender` photo/text operations compose cleanly for this sample.
- `IMessageSender` has no document send operation and `EditPhoto` cannot represent `InputMediaDocument`. The sample must use the public `BotContext.BotClient` escape hatch for image documents and for media edits that may switch between photo and document.
- Telegram returns document thumbnails with a `Thumbnail`-typed `file_id`. Bot API rejects that identifier in `sendPhoto` ("can't use file of type Thumbnail as Photo"), so it can be retained only as metadata. The sample lazily downloads documents up to 20 MB, uploads them as a photo/video preview, and persists the reusable preview `file_id`; larger or unsupported files fall back to the original document.
- The requested NuGet version `0.4.0-preview` restores as published version `0.4.0-preview.1` and produces `NU1601` warnings. Publishing an exact `0.4.0-preview` package or documenting `.1` as the intended version would avoid ambiguity.
