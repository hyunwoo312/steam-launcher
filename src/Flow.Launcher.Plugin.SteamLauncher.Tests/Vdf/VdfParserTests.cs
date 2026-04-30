using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Vdf;

public sealed class VdfParserTests
{
    private readonly IVdfParser _parser = new VdfParser();

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyObject()
    {
        var result = _parser.Parse("");

        result.Should().NotBeNull();
        result.Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleKeyValue_ReturnsObjectWithStringChild()
    {
        var result = _parser.Parse("\"name\" \"Half-Life 2\"");

        result.GetString("name").Should().Be("Half-Life 2");
    }

    [Fact]
    public void Parse_NestedObject_ReturnsObjectWithObjectChild()
    {
        const string input = """
            "AppState"
            {
                "appid"  "220"
                "name"   "Half-Life 2"
            }
            """;

        var result = _parser.Parse(input);

        var appState = result.GetObject("AppState");
        appState.Should().NotBeNull();
        appState!.GetString("appid").Should().Be("220");
        appState.GetString("name").Should().Be("Half-Life 2");
    }

    [Fact]
    public void Parse_EscapedQuotesInString_AreUnescaped()
    {
        var result = _parser.Parse("\"name\" \"He said \\\"hi\\\"\"");

        result.GetString("name").Should().Be("He said \"hi\"");
    }

    [Fact]
    public void Parse_EscapedNewline_BecomesActualNewline()
    {
        var result = _parser.Parse("\"path\" \"C:\\\\foo\\nbar\"");

        result.GetString("path").Should().Be("C:\\foo\nbar");
    }

    [Fact]
    public void Parse_LineComment_IsIgnored()
    {
        const string input = """
            // This is a comment
            "name" "Half-Life 2"
            // Another comment
            """;

        var result = _parser.Parse(input);

        result.GetString("name").Should().Be("Half-Life 2");
    }

    [Fact]
    public void Parse_PlatformConditional_IsIgnored()
    {
        var result = _parser.Parse("\"name\" \"Half-Life 2\" [$WIN32]");

        result.GetString("name").Should().Be("Half-Life 2");
    }

    [Fact]
    public void Parse_UnterminatedString_Throws()
    {
        var act = () => _parser.Parse("\"name\" \"unterminated");

        act.Should().Throw<VdfParseException>().WithMessage("*Unterminated*");
    }

    [Fact]
    public void Parse_UnexpectedCloseBrace_Throws()
    {
        var act = () => _parser.Parse("}");

        act.Should().Throw<VdfParseException>().WithMessage("*Unexpected '}'*");
    }

    [Fact]
    public void Parse_UnclosedObject_Throws()
    {
        var act = () => _parser.Parse("\"AppState\" { \"appid\" \"220\"");

        act.Should().Throw<VdfParseException>();
    }

    [Fact]
    public void ParseFile_RealCs2Manifest_ExtractsKeyFields()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "appmanifest_730.acf");

        var manifest = _parser.ParseFile(path);

        var appState = manifest.GetObject("AppState");
        appState.Should().NotBeNull();
        appState!.GetString("appid").Should().Be("730");
        appState.GetString("name").Should().Be("Counter-Strike 2");
        appState.GetString("installdir").Should().Be("Counter-Strike Global Offensive");
        appState.GetString("LastPlayed").Should().Be("1714312345");
    }

    [Fact]
    public void ParseFile_RealLibraryFolders_ExtractsBothLibraries()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "libraryfolders.vdf");

        var doc = _parser.ParseFile(path);

        var folders = doc.GetObject("libraryfolders");
        folders.Should().NotBeNull();
        folders!.GetObject("0")!.GetString("path").Should().Be("C:\\Program Files (x86)\\Steam");
        folders.GetObject("1")!.GetString("path").Should().Be("D:\\SteamLibrary");
    }

    [Fact]
    public void ParseFile_CorruptManifest_ThrowsWithFilePath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "appmanifest_corrupt.acf");

        var act = () => _parser.ParseFile(path);

        act.Should().Throw<VdfParseException>()
            .Where(ex => ex.FilePath == path);
    }
}
