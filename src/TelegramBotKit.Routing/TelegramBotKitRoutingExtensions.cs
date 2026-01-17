using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.Commands;
using TelegramBotKit.DependencyInjection;

namespace TelegramBotKit.Routing;

/// <summary>
/// Adds ASP.NET-style <c>Use*</c> sugar for simple command routing.
/// <para/>
/// This package is optional and keeps TelegramBotKit core focused on extensibility and performance.
/// </summary>
public static class TelegramBotKitRoutingExtensions
{
    // ------------------------------
    // Message (slash) commands
    // ------------------------------

    /// <summary>
    /// Registers a slash-message command handler (e.g. <c>"/start"</c>).
    /// </summary>
    /// <param name="builder">TelegramBotKit builder.</param>
    /// <param name="command">Slash command (with or without leading <c>/</c>).</param>
    /// <param name="handler">Handler delegate.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    public static TelegramBotKitBuilder UseMessageCommand(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, ValueTask> handler)
        => AddMessageCommand(builder, command, (MessageCommandInvoker)((m, ctx) => handler(m, ctx)));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseMessageCommand(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, Task> handler)
        => UseMessageCommand(builder, command, (m, ctx) => new ValueTask(handler(m, ctx)));

    /// <summary>
    /// Registers a slash-message command handler and provides access to the per-update DI scope.
    /// </summary>
    /// <param name="builder">TelegramBotKit builder.</param>
    /// <param name="command">Slash command (with or without leading <c>/</c>).</param>
    /// <param name="handler">Handler delegate. <paramref name="sp"/> is resolved from <see cref="BotContext.Services"/>.</param>
    /// <returns>The same <paramref name="builder"/> instance for chaining.</returns>
    public static TelegramBotKitBuilder UseMessageCommand(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, IServiceProvider, ValueTask> handler)
        => UseMessageCommand(builder, command, (m, ctx) => handler(m, ctx, ctx.Services));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseMessageCommand(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, IServiceProvider, Task> handler)
        => UseMessageCommand(builder, command, (m, ctx, sp) => new ValueTask(handler(m, ctx, sp)));

    /// <summary>
    /// Convenience overload: resolves <typeparamref name="TService"/> from the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseMessageCommand<TService>(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, TService, ValueTask> handler)
        where TService : notnull
        => UseMessageCommand(builder, command, (m, ctx) => handler(m, ctx, ctx.Services.GetRequiredService<TService>()));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseMessageCommand<TService>(
        this TelegramBotKitBuilder builder,
        string command,
        Func<Message, BotContext, TService, Task> handler)
        where TService : notnull
        => UseMessageCommand<TService>(builder, command, (m, ctx, s) => new ValueTask(handler(m, ctx, s)));

    // ------------------------------
    // Text commands
    // ------------------------------

    /// <summary>
    /// Registers an exact text-trigger handler.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, ValueTask> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, new[] { trigger }, handler, ignoreCase);

    /// <summary>
    /// Registers an exact text-trigger handler and provides access to the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, IServiceProvider, ValueTask> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, new[] { trigger }, handler, ignoreCase);

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, IServiceProvider, Task> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, trigger, (m, ctx, sp) => new ValueTask(handler(m, ctx, sp)), ignoreCase);

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, Task> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, trigger, (m, ctx) => new ValueTask(handler(m, ctx)), ignoreCase);

    /// <summary>
    /// Registers a multi-trigger text handler.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, ValueTask> handler,
        bool ignoreCase = false)
        => AddTextCommand(builder, triggers, ignoreCase, (TextCommandInvoker)((m, ctx) => handler(m, ctx)));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, Task> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, triggers, (m, ctx) => new ValueTask(handler(m, ctx)), ignoreCase);

    /// <summary>
    /// Registers a text handler and provides access to the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, IServiceProvider, ValueTask> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, triggers, (m, ctx) => handler(m, ctx, ctx.Services), ignoreCase);

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, IServiceProvider, Task> handler,
        bool ignoreCase = false)
        => UseTextCommand(builder, triggers, (m, ctx, sp) => new ValueTask(handler(m, ctx, sp)), ignoreCase);

    /// <summary>
    /// Convenience overload: resolves <typeparamref name="TService"/> from the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand<TService>(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, TService, ValueTask> handler,
        bool ignoreCase = false)
        where TService : notnull
        => UseTextCommand(builder, triggers, (m, ctx) => handler(m, ctx, ctx.Services.GetRequiredService<TService>()), ignoreCase);

    /// <summary>
    /// Convenience overload: resolves <typeparamref name="TService"/> from the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseTextCommand<TService>(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, TService, ValueTask> handler,
        bool ignoreCase = false)
        where TService : notnull
        => UseTextCommand<TService>(builder, new[] { trigger }, handler, ignoreCase);

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand<TService>(
        this TelegramBotKitBuilder builder,
        string trigger,
        Func<Message, BotContext, TService, Task> handler,
        bool ignoreCase = false)
        where TService : notnull
        => UseTextCommand<TService>(builder, trigger, (m, ctx, s) => new ValueTask(handler(m, ctx, s)), ignoreCase);

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseTextCommand<TService>(
        this TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        Func<Message, BotContext, TService, Task> handler,
        bool ignoreCase = false)
        where TService : notnull
        => UseTextCommand<TService>(builder, triggers, (m, ctx, s) => new ValueTask(handler(m, ctx, s)), ignoreCase);

    // ------------------------------
    // Callback commands
    // ------------------------------

    /// <summary>
    /// Registers a callback command handler.
    /// <para/>
    /// The core router expects <c>callback_data</c> in the form: <c>"{key} {arg1} {arg2} ..."</c>.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, ValueTask> handler)
        => AddCallbackCommand(builder, key, (CallbackCommandInvoker)((q, args, ctx) => handler(q, args, ctx)));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, Task> handler)
        => UseCallbackCommand(builder, key, (q, a, ctx) => new ValueTask(handler(q, a, ctx)));

    /// <summary>
    /// Registers a callback command handler and provides access to the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, IServiceProvider, ValueTask> handler)
        => UseCallbackCommand(builder, key, (q, args, ctx) => handler(q, args, ctx, ctx.Services));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, IServiceProvider, Task> handler)
        => UseCallbackCommand(builder, key, (q, a, ctx, sp) => new ValueTask(handler(q, a, ctx, sp)));

    /// <summary>
    /// Convenience overload: resolves <typeparamref name="TService"/> from the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand<TService>(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, TService, ValueTask> handler)
        where TService : notnull
        => UseCallbackCommand(builder, key, (q, args, ctx) => handler(q, args, ctx, ctx.Services.GetRequiredService<TService>()));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseCallbackCommand<TService>(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, string[], BotContext, TService, Task> handler)
        where TService : notnull
        => UseCallbackCommand<TService>(builder, key, (q, a, ctx, s) => new ValueTask(handler(q, a, ctx, s)));

    /// <summary>
    /// Registers a callback command handler without args.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, BotContext, ValueTask> handler)
        => UseCallbackCommand(builder, key, (q, _, ctx) => handler(q, ctx));

    /// <summary>
    /// Convenience overload: callback without args that resolves <typeparamref name="TService"/> from the per-update DI scope.
    /// </summary>
    public static TelegramBotKitBuilder UseCallbackCommand<TService>(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, BotContext, TService, ValueTask> handler)
        where TService : notnull
        => UseCallbackCommand<TService>(builder, key, (q, _, ctx, s) => handler(q, ctx, s));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseCallbackCommand<TService>(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, BotContext, TService, Task> handler)
        where TService : notnull
        => UseCallbackCommand<TService>(builder, key, (q, ctx, s) => new ValueTask(handler(q, ctx, s)));

    /// <inheritdoc />
    public static TelegramBotKitBuilder UseCallbackCommand(
        this TelegramBotKitBuilder builder,
        string key,
        Func<CallbackQuery, BotContext, Task> handler)
        => UseCallbackCommand(builder, key, (q, ctx) => new ValueTask(handler(q, ctx)));

    // ------------------------------
    // Internal helpers (keep API DRY)
    // ------------------------------

    private static TelegramBotKitBuilder AddMessageCommand(
        TelegramBotKitBuilder builder,
        string command,
        MessageCommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(invoker);
        ThrowIfNullOrWhiteSpace(command, nameof(command));

        builder.Services.AddSingleton(new MessageCommandDescriptor(command, invoker));
        return builder;
    }

    private static TelegramBotKitBuilder AddTextCommand(
        TelegramBotKitBuilder builder,
        IReadOnlyList<string> triggers,
        bool ignoreCase,
        TextCommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(triggers);
        if (triggers.Count == 0)
            throw new ArgumentException("At least one trigger is required.", nameof(triggers));

        builder.Services.AddSingleton(new TextCommandDescriptor(triggers, ignoreCase, invoker));
        return builder;
    }

    private static TelegramBotKitBuilder AddCallbackCommand(
        TelegramBotKitBuilder builder,
        string key,
        CallbackCommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(invoker);
        ThrowIfNullOrWhiteSpace(key, nameof(key));

        builder.Services.AddSingleton(new CallbackCommandDescriptor(key, invoker));
        return builder;
    }

    private static void ThrowIfNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", paramName);
    }
}
