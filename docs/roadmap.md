# TelegramBotKit roadmap

This roadmap is intentionally short. It is not a promise—just a direction.

## Current (MVP)

- Polling hosting (`GetUpdates`)
- `BotContext` + middleware pipeline
- Command routing: message slash commands, exact text commands, callback commands
- `WaitForUserResponse` (in-memory)
- Optional source-generator path for `AddCommands()` (reflection fallback is available)

## Next (towards a stable 0.x release)

- Keep improving documentation and samples
- API consistency and naming polish
- More guidance on update routing and handler mappings
- Performance tuning in hot paths (router, registry, middleware pipeline, sender)

## Medium term

- Webhook hosting integration (optional)
- Better conversation primitives (beyond a single wait)
- AOT-first configuration and trimming-friendly defaults

## Later

- A dialog/state-machine module (separate package)
- Storage abstractions and optional Redis implementation
