# Documentation

This folder contains the project documentation.

If you are new to TelegramBotKit, start with:

- [Quick Start — minimal polling bot](quickstart.md)
- [Processing pipeline — how updates flow](processing-pipeline.md)

## Start here

Upgrading from 0.3.2? Read the [0.4.0 migration guide](migration-0.3.2-to-0.4.0.md)
for package changes, route replacements and generated command registration.

1. [Quick Start — minimal polling bot](quickstart.md)
2. [Processing pipeline — middleware → update routes → terminal/fallback](processing-pipeline.md)

## Core topics

- [Commands and routing](commands-and-routing.md) — commands, routing sugar, default handlers
- [Generated commands](generated-commands.md) — bundled compiler, packaging and migration
- [Middleware](middleware.md) — middleware pipeline and examples
- [Hosting](hosting.md) — shared execution and polling
- [Webhooks](webhooks.md) — ASP.NET Core, delivery contracts and polling migration
- [Update routes](updates.md) — built-in/custom descriptors, local middleware and terminals
- [Conversations](conversations.md) — `WaitForUserResponse` patterns and gotchas
- [Messaging](messaging.md) — sending, editing, callbacks and the optional queue
- [Keyboards](keyboards.md) — inline and reply keyboard helpers

## Reference

- [Public API map](public-api.md) — conceptual supported surface

## Project docs

- [ConsolePolling verification](samples/console-polling.md) — manual scenarios, expected results and sample dependencies
- [PhotoSelector sample](samples/photo-selector.md) — media browsing, selection and export
- [Webhook sample](samples/webhook.md) — configuration and explicit webhook registration
- [Benchmarks](benchmarks.md) — reproducible performance measurements and their limits
- [Releasing](releasing.md) — publishing packages

---

### Quick navigation

- **Understanding the pipeline** → [Processing pipeline](processing-pipeline.md)
- **Adding commands** → [Commands and routing](commands-and-routing.md)
- **Adding middleware** → [Middleware](middleware.md)
- **Handling more Telegram update types** → [Updates](updates.md)
