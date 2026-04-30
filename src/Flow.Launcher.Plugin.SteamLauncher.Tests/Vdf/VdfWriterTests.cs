using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Vdf;

public sealed class VdfWriterTests
{
    private readonly IVdfParser _parser = new VdfParser();
    private readonly IVdfWriter _writer = new VdfWriter();

    [Fact]
    public void Write_SingleStringChild_EmitsQuotedKeyAndValueOnOneLine()
    {
        var input = """
            "name" "Half-Life 2"
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);
        var roundTripped = _parser.Parse(rendered);

        roundTripped.GetString("name").Should().Be("Half-Life 2");
    }

    [Fact]
    public void Write_StringChild_UsesDoubleTabBetweenKeyAndValue()
    {
        var tree = new VdfNode.Object(new Dictionary<string, VdfNode>(StringComparer.Ordinal)
        {
            ["AccountName"] = new VdfNode.String("hyunwoo312")
        });

        var rendered = _writer.Write(tree);

        rendered.Should().Be("\"AccountName\"\t\t\"hyunwoo312\"\n");
    }

    [Fact]
    public void Write_NestedObject_RoundTripsThroughParser()
    {
        var input = """
            "AppState"
            {
                "appid" "220"
                "name"  "Half-Life 2"
                "config"
                {
                    "language" "english"
                }
            }
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);
        var roundTripped = _parser.Parse(rendered);

        var app = roundTripped.GetObject("AppState");
        app.Should().NotBeNull();
        app!.GetString("appid").Should().Be("220");
        app.GetString("name").Should().Be("Half-Life 2");
        app.GetObject("config")!.GetString("language").Should().Be("english");
    }

    [Fact]
    public void Write_StringWithEmbeddedQuotes_IsEscapedAndRoundTrips()
    {
        var input = """
            "say" "He said \"hi\""
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);

        rendered.Should().Contain("\\\"hi\\\"");
        var roundTripped = _parser.Parse(rendered);
        roundTripped.GetString("say").Should().Be("He said \"hi\"");
    }

    [Fact]
    public void Write_StringWithBackslash_IsEscapedAndRoundTrips()
    {
        var input = """
            "path" "C:\\Steam"
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);
        var roundTripped = _parser.Parse(rendered);

        roundTripped.GetString("path").Should().Be(@"C:\Steam");
    }

    [Fact]
    public void Write_EmptyObject_EmitsBracesAndRoundTrips()
    {
        var input = """
            "thing"
            {
            }
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);
        var roundTripped = _parser.Parse(rendered);

        roundTripped.GetObject("thing").Should().NotBeNull();
        roundTripped.GetObject("thing")!.Children.Should().BeEmpty();
    }

    [Fact]
    public void Write_DeepNesting_PreservesAllLeaves()
    {
        var input = """
            "UserLocalConfigStore"
            {
                "friends"
                {
                    "PersonaStateDesired" "1"
                    "VoiceReceiveVolume"  "100"
                }
                "broadcast"
                {
                    "Permissions" "0"
                }
            }
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);
        var roundTripped = _parser.Parse(rendered);

        var store = roundTripped.GetObject("UserLocalConfigStore");
        store!.GetObject("friends")!.GetString("PersonaStateDesired").Should().Be("1");
        store.GetObject("friends")!.GetString("VoiceReceiveVolume").Should().Be("100");
        store.GetObject("broadcast")!.GetString("Permissions").Should().Be("0");
    }

    [Fact]
    public void Write_MutatedTree_RoundTripsWithNewValue()
    {
        var input = """
            "UserLocalConfigStore"
            {
                "friends"
                {
                    "PersonaStateDesired" "1"
                }
            }
            """;
        var tree = _parser.Parse(input);

        var mutated = ReplaceDeepLeaf(tree, ["UserLocalConfigStore", "friends", "PersonaStateDesired"], "7");
        var rendered = _writer.Write(mutated);
        var roundTripped = _parser.Parse(rendered);

        roundTripped.GetObject("UserLocalConfigStore")!
            .GetObject("friends")!
            .GetString("PersonaStateDesired").Should().Be("7");
    }

    [Fact]
    public void Write_RealLocalConfigShape_IsParseable()
    {
        var input = """
            "UserLocalConfigStore"
            {
                "Software"
                {
                    "Valve"
                    {
                        "Steam"
                        {
                            "LastGameID" "730"
                        }
                    }
                }
                "friends"
                {
                    "PersonaStateDesired" "1"
                    "ShowAvatars"         "1"
                }
                "system"
                {
                    "EnableGameOverlay" "1"
                }
            }
            """;
        var tree = _parser.Parse(input);

        var rendered = _writer.Write(tree);

        var act = () => _parser.Parse(rendered);
        act.Should().NotThrow();
    }

    private static VdfNode.Object ReplaceDeepLeaf(VdfNode.Object root, IReadOnlyList<string> path, string newValue)
    {
        if (path.Count == 0)
            throw new ArgumentException("Path must have at least one segment", nameof(path));

        var head = path[0];
        var tail = path.Skip(1).ToList();
        var newChildren = root.Children.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        if (tail.Count == 0)
        {
            newChildren[head] = new VdfNode.String(newValue);
        }
        else
        {
            var child = root.GetObject(head)
                        ?? throw new InvalidOperationException($"Missing object at {head}");
            newChildren[head] = ReplaceDeepLeaf(child, tail, newValue);
        }

        return new VdfNode.Object(newChildren);
    }
}
