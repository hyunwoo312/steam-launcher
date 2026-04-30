namespace Flow.Launcher.Plugin.SteamLauncher.Vdf;

public interface IVdfWriter
{
    /// <summary>
    /// Serializes a parsed VDF tree to a string that <see cref="IVdfParser.Parse"/>
    /// will round-trip. Output uses tab indentation, double-quoted keys/values,
    /// and brace-delimited nested objects on their own lines.
    /// </summary>
    string Write(VdfNode.Object root);
}
