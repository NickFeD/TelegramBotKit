# Webhook sample

This ASP.NET Core application maps `/telegram/update` and uses shared Hosting
execution. Configure secrets outside the repository:

```powershell
dotnet user-secrets set "TelegramBotKit:Token" "<bot-token>" --project samples/TelegramBotKit.Sample.Webhook
dotnet user-secrets set "Webhook:SecretToken" "<random-secret-with-letters-digits-underscores-hyphens>" --project samples/TelegramBotKit.Sample.Webhook
dotnet user-secrets set "Webhook:Url" "https://your-public-host/telegram/update" --project samples/TelegramBotKit.Sample.Webhook
dotnet run --project samples/TelegramBotKit.Sample.Webhook
```

The server needs a public HTTPS URL, commonly through your reverse proxy or tunnel.
`Webhook:Url` is the externally visible URL; `Webhook:Path` is the application path.
Account for proxy prefixes when configuring them.

To deliberately register that URL with Telegram, start with `--RegisterWebhook=true`
once. Normal startup and shutdown do not register or delete the webhook. In a
multi-replica deployment, manage registration as a deployment operation. To switch
back to polling, explicitly remove the webhook first.

HTTP 200 acknowledges in-memory admission, not durable storage or completed
processing. Updates use the same global middleware, route/command pipeline, keyed
actors and conversation-response scheduling as polling. There is no inbox,
deduplication, restart recovery or guarantee against loss on process failure.

See [webhook documentation](../webhooks.md) for the full contract.
