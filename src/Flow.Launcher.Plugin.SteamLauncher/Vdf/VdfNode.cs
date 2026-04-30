namespace Flow.Launcher.Plugin.SteamLauncher.Vdf;

public abstract record VdfNode
{
    public sealed record String(string Value) : VdfNode;

    public sealed record Object(IReadOnlyDictionary<string, VdfNode> Children) : VdfNode
    {
        public VdfNode? this[string key] => Children.TryGetValue(key, out var child) ? child : null;

        public string? GetString(string key) =>
            this[key] is VdfNode.String s ? s.Value : null;

        public Object? GetObject(string key) =>
            this[key] is VdfNode.Object o ? o : null;
    }
}
