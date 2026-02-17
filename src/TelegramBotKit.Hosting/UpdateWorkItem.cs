using Telegram.Bot.Types;

namespace TelegramBotKit.Hosting;

internal readonly record struct UpdateWorkItem(Update Update, CancellationToken Ct);
