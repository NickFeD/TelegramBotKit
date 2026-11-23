using System.Text;

namespace TelegramBotKit.Generators;

/// <summary>Keeps generated newlines and string encoding independent of the host platform.</summary>
internal sealed class SourceWriter
{
    private readonly StringBuilder _builder = new();

    internal SourceWriter Append(string value)
    {
        _builder.Append(value);
        return this;
    }

    internal SourceWriter AppendLine(string value = "")
    {
        _builder.Append(value).Append('\n');
        return this;
    }

    public override string ToString() => _builder.ToString();
}
