#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace TelegramBotKit.Generators;

/// <summary>Generates compile-time command registrations for attributed TelegramBotKit command types.</summary>
/// <remarks>
/// Discovery runs when the compilation changes. It includes accessible command types in the current
/// assembly and public command types in references that depend on TelegramBotKit.
/// </remarks>
[Generator]
public sealed class TelegramBotKitCommandsGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateMessageCommand = new(
        id: "TBK001",
        title: "Duplicate message command",
        messageFormat: "Duplicate message command '{0}' found on type '{1}'",
        category: "TelegramBotKit",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/NickFeD/TelegramBotKit/blob/master/docs/generated-commands.md#tbk001");

    private static readonly DiagnosticDescriptor DuplicateCallbackCommand = new(
        id: "TBK002",
        title: "Duplicate callback command",
        messageFormat: "Duplicate callback command key '{0}' found on type '{1}'",
        category: "TelegramBotKit",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/NickFeD/TelegramBotKit/blob/master/docs/generated-commands.md#tbk002");

    private static readonly DiagnosticDescriptor DuplicateTextTrigger = new(
        id: "TBK003",
        title: "Duplicate text trigger",
        messageFormat: "Duplicate text trigger '{0}' (ignoreCase={1}) found on type '{2}'",
        category: "TelegramBotKit",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/NickFeD/TelegramBotKit/blob/master/docs/generated-commands.md#tbk003");

    /// <summary>Registers the incremental pipeline that discovers commands and emits their DI registrar.</summary>
    /// <param name="context">The Roslyn initialization context for this generator.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(static (c, ct) => Collect(c, ct));
        context.RegisterSourceOutput(model, static (spc, m) => Emit(spc, m));
    }

    private static Model Collect(Compilation compilation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var msgAttr = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.MessageCommandAttribute");
        var textAttr = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.TextCommandAttribute");
        var cbAttr = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.CallbackCommandAttribute");

        if (msgAttr is null && textAttr is null && cbAttr is null)
            return Model.Empty;

        var iMessage = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.IMessageCommand");
        var iText = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.ITextCommand");
        var iCallback = compilation.GetTypeByMetadataName("TelegramBotKit.Commands.ICallbackCommand");

        // Emit callback-key mappings only when the referenced Core exposes their integration hook.
        var hasCallbackKeysHook =
            compilation.GetTypeByMetadataName("TelegramBotKit.Commands.TelegramBotKitGeneratedCallbackKeysHook") is not null;

        var assemblies = new List<IAssemblySymbol>(capacity: 8)
        {
            compilation.Assembly
        };

        foreach (var asm in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            // Core supplies the contracts; user-defined commands belong to consumer assemblies.
            if (asm.Name.Equals("TelegramBotKit", StringComparison.OrdinalIgnoreCase))
                continue;

            // Only scan assemblies that reference TelegramBotKit (heuristic to avoid scanning BCL)
            if (!ReferencesTelegramBotKit(asm))
                continue;

            assemblies.Add(asm);
        }

        var messages = new List<MessageItem>();
        var callbacks = new List<CallbackItem>();
        var texts = new List<TextItem>();

        foreach (var asm in assemblies)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var t in GetAllTypes(asm))
            {
                ct.ThrowIfCancellationRequested();

                if (t.TypeKind != TypeKind.Class) continue;
                if (t.IsAbstract) continue;
                if (t.IsGenericType) continue;

                if (!IsAccessible(compilation, t))
                    continue;

                // Match the actual attribute symbols so unrelated names do not register commands.
                if (msgAttr is not null)
                {
                    var a = GetAttribute(t, msgAttr);
                    if (a is not null)
                    {
                        if (iMessage is not null && !Implements(t, iMessage))
                            goto SkipMessage;

                        var cmd = GetStringCtorArg(a, 0);
                        if (!string.IsNullOrWhiteSpace(cmd))
                        {
                            var loc = GetAttributeLocation(a, ct);
                            messages.Add(new MessageItem(cmd!, t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), loc));
                        }
                    }
                }

            SkipMessage:

                if (cbAttr is not null)
                {
                    var a = GetAttribute(t, cbAttr);
                    if (a is not null)
                    {
                        if (iCallback is not null && !Implements(t, iCallback))
                            goto SkipCallback;

                        var key = GetStringCtorArg(a, 0);
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            var loc = GetAttributeLocation(a, ct);
                            callbacks.Add(new CallbackItem(key!, t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), loc));
                        }
                    }
                }

            SkipCallback:

                if (textAttr is not null)
                {
                    var a = GetAttribute(t, textAttr);
                    if (a is not null)
                    {
                        if (iText is not null && !Implements(t, iText))
                            continue;

                        var (ignoreCase, triggers) = ParseTextAttribute(a);
                        if (triggers.Length > 0)
                        {
                            var loc = GetAttributeLocation(a, ct);
                            texts.Add(new TextItem(ignoreCase, triggers, t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), loc));
                        }
                    }
                }
            }
        }

        return new Model(
            messages.ToImmutableArray(),
            texts.ToImmutableArray(),
            callbacks.ToImmutableArray(),
            hasCallbackKeysHook);
    }

    private static void Emit(SourceProductionContext context, Model model)
    {
        if (model.IsEmpty)
            return;

        // Report duplicate keys before the generated registrations reach the runtime registry.
        var msgSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in model.Messages)
        {
            var norm = NormalizeSlash(m.Command);
            if (!msgSet.Add(norm))
                context.ReportDiagnostic(Diagnostic.Create(DuplicateMessageCommand, m.Location ?? Location.None, norm, m.TypeName));
        }

        var cbSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in model.Callbacks)
        {
            var key = c.Key.Trim();
            if (!cbSet.Add(key))
                context.ReportDiagnostic(Diagnostic.Create(DuplicateCallbackCommand, c.Location ?? Location.None, key, c.TypeName));
        }

        // text triggers: case-sensitive and ignoreCase tracked separately
        var textExact = new HashSet<string>(StringComparer.Ordinal);
        var textExactIgnoreCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var textIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in model.Texts)
        {
            foreach (var trig in t.Triggers)
            {
                var raw = (trig ?? string.Empty).Trim();
                if (raw.Length == 0) continue;

                if (t.IgnoreCase)
                {
                    if (!textIgnore.Add(raw) || textExactIgnoreCase.Contains(raw))
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateTextTrigger, t.Location ?? Location.None, raw, true, t.TypeName));
                }
                else
                {
                    if (!textExact.Add(raw) || textIgnore.Contains(raw))
                        context.ReportDiagnostic(Diagnostic.Create(DuplicateTextTrigger, t.Location ?? Location.None, raw, false, t.TypeName));
                    textExactIgnoreCase.Add(raw);
                }
            }
        }

        var sb = new SourceWriter();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using TelegramBotKit.DependencyInjection;");
        sb.AppendLine();

        sb.AppendLine("internal static class TelegramBotKit_GeneratedCommands_Initializer");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");

        // Keep compatibility with Core versions that expose the callback-key hook.
        if (model.HasCallbackKeysHook && model.Callbacks.Length > 0)
        {
            sb.AppendLine("        global::TelegramBotKit.Commands.TelegramBotKitGeneratedCallbackKeysHook.SetResolver(static t =>");
            sb.AppendLine("        {");

            foreach (var c in model.Callbacks.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("            if (t == typeof(").Append(c.TypeName).Append(")) return \"")
                  .Append(Escape(c.Key)).AppendLine("\";");
            }

            sb.AppendLine("            return null;");
            sb.AppendLine("        });");
            sb.AppendLine();
        }

        // Emit typed registrations so runtime discovery does not need to inspect attributes.
        sb.AppendLine("        TelegramBotKitGeneratedCommandsHook.SetRegistrar(static services =>");
        sb.AppendLine("        {");
        sb.AppendLine("            if (services is null) throw new System.ArgumentNullException(nameof(services));");
        sb.AppendLine();

        foreach (var m in model.Messages.OrderBy(x => x.Command, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("            services.AddMessageCommand<").Append(m.TypeName).Append(">(\"")
              .Append(Escape(m.Command)).AppendLine("\");");
        }

        foreach (var t in model.Texts.OrderBy(x => x.TypeName, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("            services.AddTextCommand<").Append(t.TypeName).Append(">(new[] { ");
            for (int i = 0; i < t.Triggers.Length; i++)
            {
                if (i != 0) sb.Append(", ");
                sb.Append("\"").Append(Escape(t.Triggers[i])).Append("\"");
            }
            sb.Append(" }, ignoreCase: ").Append(t.IgnoreCase ? "true" : "false").AppendLine(");");
        }

        foreach (var c in model.Callbacks.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("            services.AddCallbackCommand<").Append(c.TypeName).Append(">(\"")
              .Append(Escape(c.Key)).AppendLine("\");");
        }

        sb.AppendLine();
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("TelegramBotKit.Commands.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static Location? GetAttributeLocation(AttributeData a, CancellationToken ct)
    {
        // Metadata-only attributes have no source location for diagnostics.
        var sref = a.ApplicationSyntaxReference;
        if (sref is null) return Location.None;
        try
        {
            return sref.GetSyntax(ct).GetLocation();
        }
        catch
        {
            return Location.None;
        }
    }

    private static bool ReferencesTelegramBotKit(IAssemblySymbol asm)
    {
        if (asm.Name.Equals("TelegramBotKit", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var m in asm.Modules)
        {
            foreach (var r in m.ReferencedAssemblySymbols)
            {
                if (r.Name.Equals("TelegramBotKit", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAccessible(Compilation compilation, INamedTypeSymbol t)
    {
        var inThisAssembly = SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, compilation.Assembly);

        if (inThisAssembly)
        {
            // The generated top-level class is not derived from the handler's containing types.
            // Every enclosing type must therefore permit access from a peer in this assembly.
            for (INamedTypeSymbol? cur = t; cur is not null; cur = cur.ContainingType)
            {
                switch (cur.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                    case Accessibility.Internal:
                    case Accessibility.ProtectedOrInternal: // Same-assembly access satisfies the internal branch.
                        continue;
                    default:
                        return false;
                }
            }
            return true;
        }

        // For referenced assemblies, only public (and all containing types public)
        for (INamedTypeSymbol? cur = t; cur is not null; cur = cur.ContainingType)
        {
            if (cur.DeclaredAccessibility != Accessibility.Public)
                return false;
        }
        return true;
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol iface)
        => type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));

    private static AttributeData? GetAttribute(INamedTypeSymbol type, INamedTypeSymbol attr)
        => type.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr));

    private static string? GetStringCtorArg(AttributeData a, int index)
    {
        if (a.ConstructorArguments.Length <= index)
            return null;
        return a.ConstructorArguments[index].Value as string;
    }

    private static (bool IgnoreCase, string[] Triggers) ParseTextAttribute(AttributeData a)
    {
        // TextCommandAttribute has two ctors:
        // 1) (params string[] triggers) => ignoreCase=true
        // 2) (bool ignoreCase, params string[] triggers)
        var args = a.ConstructorArguments;
        bool ignoreCase = true;
        ImmutableArray<TypedConstant> vals = default;

        if (args.Length == 1)
        {
            // triggers only
            vals = args[0].Values;
        }
        else if (args.Length >= 2)
        {
            if (args[0].Value is bool b)
                ignoreCase = b;
            vals = args[1].Values;
        }

        var list = new List<string>();
        foreach (var tc in vals)
        {
            if (tc.Value is string s && !string.IsNullOrWhiteSpace(s))
                list.Add(s);
        }

        return (ignoreCase, list.ToArray());
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(IAssemblySymbol asm)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(asm.GlobalNamespace);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();

            if (cur is INamespaceSymbol ns)
            {
                foreach (var n in ns.GetNamespaceMembers())
                    stack.Push(n);
                foreach (var t in ns.GetTypeMembers())
                    stack.Push(t);
            }
            else if (cur is INamedTypeSymbol nt)
            {
                yield return nt;
                foreach (var nested in nt.GetTypeMembers())
                    stack.Push(nested);
            }
        }
    }

    private static string NormalizeSlash(string cmd)
    {
        cmd = (cmd ?? string.Empty).Trim();
        if (cmd.Length == 0) return string.Empty;
        if (cmd[0] != '/') cmd = "/" + cmd;
        var at = cmd.IndexOf('@');
        if (at >= 0) cmd = cmd.Substring(0, at);
        return cmd;
    }

    private static string Escape(string value)
    {
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
        return literal.Substring(1, literal.Length - 2);
    }

    private sealed record Model(
        ImmutableArray<MessageItem> Messages,
        ImmutableArray<TextItem> Texts,
        ImmutableArray<CallbackItem> Callbacks,
        bool HasCallbackKeysHook)
    {
        public bool IsEmpty => Messages.Length == 0 && Texts.Length == 0 && Callbacks.Length == 0;

        public static Model Empty => new(
            ImmutableArray<MessageItem>.Empty,
            ImmutableArray<TextItem>.Empty,
            ImmutableArray<CallbackItem>.Empty,
            HasCallbackKeysHook: false);
    }

    private sealed record MessageItem(string Command, string TypeName, Location? Location);

    private sealed record CallbackItem(string Key, string TypeName, Location? Location);

    private sealed record TextItem(bool IgnoreCase, string[] Triggers, string TypeName, Location? Location);
}
