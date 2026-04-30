namespace Flow.Launcher.Plugin.SteamLauncher.Vdf;

public interface IVdfParser
{
    /// <summary>
    /// Parse a KeyValues document. Returns the root object.
    /// </summary>
    /// <exception cref="VdfParseException">If the document is malformed.</exception>
    VdfNode.Object Parse(string content);

    /// <summary>
    /// Convenience: read file then parse. Adds the file path to any exception.
    /// </summary>
    VdfNode.Object ParseFile(string filePath);
}

public sealed class VdfParseException(string detail, string? filePath = null, Exception? inner = null)
    : Exception(filePath is null ? detail : $"{filePath}: {detail}", inner)
{
    public string? FilePath { get; } = filePath;
}
