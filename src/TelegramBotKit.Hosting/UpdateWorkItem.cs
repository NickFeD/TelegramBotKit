namespace TelegramBotKit.Hosting;

internal sealed record UpdateWorkItem(CancellationToken Ct, Func<Task> Execute)
{
    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
