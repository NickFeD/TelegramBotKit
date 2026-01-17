using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;

namespace TelegramBotKit.Routing;

/// <summary>
/// Adds ASP.NET-style "Use..." sugar for simple command routing.
/// </summary>
public static class TelegramBotKitRoutingExtensions
{
    /// <summary>
    /// Registers a slash-message command handler (e.g. "/start").
    /// </summary>
    public static TelegramBotKitBuilder UseMessageCommand(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, Task> handler)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Command is required.", nameof(command));

        builder.Services.AddSingleton(new MessageCommandDescriptor(
            command,
            (message, ctx) => new ValueTask(handler(message, ctx))));

        return builder;
    }

    /// <summary>
    /// Registers an exact text-trigger handler.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, Task> handler,
        bool ignoreCase = false)
    {
        if (string.IsNullOrWhiteSpace(trigger)) throw new ArgumentException("Trigger is required.", nameof(trigger));

        return UseTextCommand()(builder, new[] { trigger }, handler, ignoreCase);
    }

    /// <summary>
    /// Registers a multi-trigger text handler.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, Task> handler,
        bool ignoreCase = false)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (triggers is null) throw new ArgumentNullException(nameof(triggers));
        if (triggers.Count == 0) throw new ArgumentException("At least one trigger is required.", nameof(triggers));

        builder.Services.AddSingleton(new TextCommandDescriptor(
            triggers,
            ignoreCase,
            (message, ctx) => new ValueTask(handler(message, ctx))));

        return builder;
    }

    /// <summary>
    /// Registers a callback command handler.
    /// The router expects callback_data to be: "{key} {arg1} {arg2} ...".
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, Task> handler)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));

        builder.Services.AddSingleton(new CallbackCommandDescriptor(
            key,
            (query, args, ctx) => new ValueTask(handler(query, args, ctx))));

        return builder;
    }

    /// <summary>
    /// Registers a callback command handler without args.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, BotContext, Task> handler)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));

        return UseCallbackCommand(builder, key, (query, args, ctx) => new ValueTask(handler(query, ctx)));
    }
}
