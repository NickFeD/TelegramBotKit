using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramBotKit.DependencyInjection;
using TelegramBotKit.Dispatching;
using TelegramBotKit.Middleware;
using TelegramBotKit.Routing;

namespace TelegramBotKit.Benchmarks;

public enum DispatchMode { Fallback, TypedRoute, GenericCommand, FluentCommand, Callback }
public class DispatchBenchmarks
{
    [ParamsAllValues] public DispatchMode Mode { get; set; }
    private ServiceProvider _provider = null!;
    private IUpdateDispatcher _dispatcher = null!;
    private Update _update = null!;
    [GlobalSetup] public async Task Setup()
    {
        var (services, bot) = Fixtures.Services();
        _update = Fixtures.Update();
        switch (Mode)
        {
            case DispatchMode.TypedRoute: bot.Route(UpdateRoutes.Message).HandleWith<NoopCommand>(); break;
            case DispatchMode.GenericCommand: services.AddMessageCommand<NoopCommand>("/work"); break;
            case DispatchMode.FluentCommand: bot.UseMessageCommand("/work", static (_, _) => ValueTask.CompletedTask); break;
            case DispatchMode.Callback:
                services.AddCallbackCommand<NoopCommand>("work");
                _update = new Update { Id = 1, CallbackQuery = new CallbackQuery { Id = "cb", From = new User { Id = 7 }, Data = "work one two" } };
                break;
        }
        _provider = services.BuildServiceProvider();
        _dispatcher = _provider.GetRequiredService<IUpdateDispatcher>();
        await _dispatcher.DispatchAsync(_update);
    }
    [Benchmark] public Task Dispatch() => _dispatcher.DispatchAsync(_update);
    [GlobalCleanup] public async Task Cleanup() => await _provider.DisposeAsync();
}

public enum MiddlewareMode { GlobalTask, GlobalValueTask, GlobalResolved, RouteInline, RouteResolved, RouteAdapter, ShortCircuit }
public class MiddlewareBenchmarks
{
    [Params(0, 5)] public int Depth { get; set; }
    [ParamsAllValues] public MiddlewareMode Mode { get; set; }
    private ServiceProvider _provider = null!;
    private IUpdateDispatcher _dispatcher = null!;
    private readonly Update _update = Fixtures.Update();
    [GlobalSetup] public async Task Setup()
    {
        var (services, bot) = Fixtures.Services();
        var route = bot.Route(UpdateRoutes.Message).HandleWith<NoopCommand>();
        for (var i = 0; i < Depth; i++)
            switch (Mode)
            {
                case MiddlewareMode.GlobalTask: bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)(static (ctx, next) => next(ctx))); break;
                case MiddlewareMode.GlobalValueTask: bot.UseMiddleware((Func<BotContext, BotContextDelegate, ValueTask>)(static (ctx, next) => new(next(ctx)))); break;
                case MiddlewareMode.GlobalResolved: bot.UseMiddleware<ResolvedMiddleware>(); break;
                case MiddlewareMode.RouteInline: route.Use(static (ctx, next) => next(ctx)); break;
                case MiddlewareMode.RouteResolved: route.Use<NoopRouteMiddleware>(); break;
                case MiddlewareMode.RouteAdapter: route.UseUpdateMiddleware(static (ctx, next) => next(ctx)); break;
                case MiddlewareMode.ShortCircuit: bot.UseMiddleware((Func<BotContext, BotContextDelegate, Task>)(static (_, _) => Task.CompletedTask)); break;
            }
        _provider = services.BuildServiceProvider();
        _dispatcher = _provider.GetRequiredService<IUpdateDispatcher>();
        await _dispatcher.DispatchAsync(_update);
    }
    [Benchmark] public Task Dispatch() => _dispatcher.DispatchAsync(_update);
    [GlobalCleanup] public async Task Cleanup() => await _provider.DisposeAsync();
    public sealed class ResolvedMiddleware : IUpdateMiddleware
    {
        public Task InvokeAsync(BotContext context, BotContextDelegate next) => next(context);
    }
}
