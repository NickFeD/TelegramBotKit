using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotKit.Hosting;
using TelegramBotKit.Options;

namespace TelegramBotKit.Webhook.AspNetCore;

/// <summary>Maps the Telegram webhook endpoint into an existing ASP.NET Core application.</summary>
public static class TelegramBotKitWebhookEndpointExtensions
{
    /// <summary>Maps a POST endpoint at WebhookOptions.Path, authenticated by Telegram's secret header.</summary>
    /// <param name="endpoints">The application or route group containing the endpoint.</param>
    /// <returns>The endpoint builder for additional application conventions.</returns>
    /// <remarks>
    /// HTTP 200 means admission by IUpdateReceiver, not completion of handlers. The default
    /// receiver is in memory. No HttpContext, request stream or request cancellation is retained
    /// by the default receiver after admission. This method never registers the URL with Telegram.
    /// </remarks>
    public static RouteHandlerBuilder MapTelegramBotKitWebhook(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<WebhookOptions>>().Value;
        var secret = Encoding.UTF8.GetBytes(options.SecretToken!);
        Func<HttpContext, Task<IResult>> handle = context => HandleAsync(context, options, secret);
        return endpoints.MapPost(options.Path, handle)
            .AllowAnonymous()
            .DisableAntiforgery()
            .Accepts<Update>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithSummary("Accept a Telegram update for background processing")
            .WithDescription("Authenticates X-Telegram-Bot-Api-Secret-Token. Success acknowledges admission, not durable storage or handler completion.");
    }

    private static async Task<IResult> HandleAsync(HttpContext context, WebhookOptions options, byte[] secret)
    {
        var request = context.Request;
        if (!request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var supplied) ||
            supplied.Count != 1 || !CryptographicOperations.FixedTimeEquals(secret, Encoding.UTF8.GetBytes(supplied[0] ?? "")))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!request.HasJsonContentType())
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.ContentLength > options.MaxRequestBodyBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        Update? update;
        try
        {
            // Enforce the same limit for chunked bodies, not only requests with Content-Length.
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = (int)Math.Min(buffer.Length, options.MaxRequestBodyBytes - body.Length + 1);
                var read = await request.Body.ReadAsync(buffer.AsMemory(0, count), context.RequestAborted).ConfigureAwait(false);
                if (read == 0) break;
                if (body.Length + read > options.MaxRequestBodyBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                body.Write(buffer, 0, read);
            }
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("update_id", out var id) || id.ValueKind != JsonValueKind.Number ||
                !id.TryGetInt32(out var value) || value < 0)
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            update = root.Deserialize<Update>(JsonBotAPI.Options);
        }
        catch (JsonException)
        {
            return Results.StatusCode(StatusCodes.Status400BadRequest);
        }
        if (update is null) return Results.StatusCode(StatusCodes.Status400BadRequest);

        try
        {
            await context.RequestServices.GetRequiredService<IUpdateReceiver>()
                .ReceiveAsync(update, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        return Results.Ok();
    }
}
