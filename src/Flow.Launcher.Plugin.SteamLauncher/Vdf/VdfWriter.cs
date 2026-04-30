using System.Text;

namespace Flow.Launcher.Plugin.SteamLauncher.Vdf;

public sealed class VdfWriter : IVdfWriter
{
    public string Write(VdfNode.Object root)
    {
        var sb = new StringBuilder();
        WriteObjectChildren(sb, root, depth: 0);
        return sb.ToString();
    }

    private static void WriteObjectChildren(StringBuilder sb, VdfNode.Object obj, int depth)
    {
        foreach (var (key, value) in obj.Children)
        {
            Indent(sb, depth);
            AppendQuoted(sb, key);

            switch (value)
            {
                case VdfNode.String s:
                    // Two tabs between key/value matches Valve's pretty-printer; keeps output byte-shape identical to Steam's own writes.
                    sb.Append("\t\t");
                    AppendQuoted(sb, s.Value);
                    sb.Append('\n');
                    break;
                case VdfNode.Object child:
                    sb.Append('\n');
                    Indent(sb, depth);
                    sb.Append("{\n");
                    WriteObjectChildren(sb, child, depth + 1);
                    Indent(sb, depth);
                    sb.Append("}\n");
                    break;
                default:
                    throw new InvalidOperationException($"Unknown VDF node type: {value.GetType()}");
            }
        }
    }

    private static void Indent(StringBuilder sb, int depth)
    {
        for (var i = 0; i < depth; i++) sb.Append('\t');
    }

    private static void AppendQuoted(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append('\\').Append('\\'); break;
                case '"': sb.Append('\\').Append('"'); break;
                case '\n': sb.Append('\\').Append('n'); break;
                case '\t': sb.Append('\\').Append('t'); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }
}
