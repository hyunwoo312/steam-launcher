using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SteamLauncher.Services;
using Flow.Launcher.Plugin.SteamLauncher.Steam;
using Flow.Launcher.Plugin.SteamLauncher.Vdf;
using NSubstitute;
using Xunit;

namespace Flow.Launcher.Plugin.SteamLauncher.Tests.Services;

public sealed class AccountSwitcherServiceTests : IDisposable
{
    private readonly string _root;

    public AccountSwitcherServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "switcher-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "config"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string LoginUsersPath => Path.Combine(_root, "config", "loginusers.vdf");

    private void WriteLoginUsers(string content) => File.WriteAllText(LoginUsersPath, content);

    private (AccountSwitcherService Service,
             IRegistryWriter RegistryWriter,
             IRegistryReader RegistryReader,
             ISteamProcessController ProcessController,
             ISteamPathResolver PathResolver)
        BuildService(bool steamInstalled = true)
    {
        var path = Substitute.For<ISteamPathResolver>();
        if (steamInstalled)
            path.GetSteamInstallPath().Returns(_root);
        else
            path.GetSteamInstallPath().Returns(_ => throw new SteamPathNotFoundException());

        var registryWriter = Substitute.For<IRegistryWriter>();
        var registryReader = Substitute.For<IRegistryReader>();
        registryReader
            .ReadCurrentUserString(@"Software\Valve\Steam", "AutoLoginUser")
            .Returns(_ => RegistryReaderEcho(registryWriter));
        var processController = Substitute.For<ISteamProcessController>();
        processController.IsGameRunning().Returns(false);
        var service = new AccountSwitcherService(
            path, new VdfParser(), new VdfWriter(),
            registryWriter, registryReader, processController, logException: null);
        return (service, registryWriter, registryReader, processController, path);
    }

    private static string? RegistryReaderEcho(IRegistryWriter writer)
    {
        var lastWrite = writer.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IRegistryWriter.WriteCurrentUserString))
            .Select(c => c.GetArguments())
            .Where(a => (string?)a[1] == "AutoLoginUser")
            .LastOrDefault();
        return lastWrite is null ? null : (string?)lastWrite[2];
    }

    [Fact]
    public void GetKnownAccounts_LoginUsersMissing_ReturnsEmpty()
    {
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Should().BeEmpty();
    }

    [Fact]
    public void GetKnownAccounts_SteamNotInstalled_ReturnsEmpty()
    {
        var (service, _, _, _, _) = BuildService(steamInstalled: false);

        var accounts = service.GetKnownAccounts();

        accounts.Should().BeEmpty();
    }

    [Fact]
    public void GetKnownAccounts_TwoAccounts_ReturnsBothWithCurrentFlagged()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName"      "alpha"
                    "PersonaName"      "Alpha-Persona"
                    "RememberPassword" "1"
                    "MostRecent"       "0"
                    "Timestamp"        "1714000000"
                }
                "76561198000000002"
                {
                    "AccountName"      "bravo"
                    "PersonaName"      "Bravo-Persona"
                    "RememberPassword" "1"
                    "MostRecent"       "1"
                    "Timestamp"        "1715000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Should().HaveCount(2);
        accounts[0].SteamId64.Should().Be(76561198000000002UL);
        accounts[0].AccountName.Should().Be("bravo");
        accounts[0].PersonaName.Should().Be("Bravo-Persona");
        accounts[0].IsCurrent.Should().BeTrue();
        accounts[0].LastLoginUnix.Should().Be(1715000000L);
        accounts[1].SteamId64.Should().Be(76561198000000001UL);
        accounts[1].AccountName.Should().Be("alpha");
        accounts[1].IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void GetKnownAccounts_OrdersByTimestampDescending()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "old"    "PersonaName" "Old"    "RememberPassword" "0" "MostRecent" "0" "Timestamp" "1700000000"
                }
                "76561198000000002"
                {
                    "AccountName" "newest" "PersonaName" "Newest" "RememberPassword" "1" "MostRecent" "1" "Timestamp" "1715000000"
                }
                "76561198000000003"
                {
                    "AccountName" "middle" "PersonaName" "Middle" "RememberPassword" "1" "MostRecent" "0" "Timestamp" "1710000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Select(a => a.AccountName).Should().Equal("newest", "middle", "old");
    }

    [Fact]
    public void GetKnownAccounts_FallsBackToAccountNameWhenPersonaMissing()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha"
                    "MostRecent"  "1"
                    "Timestamp"   "1714000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Should().HaveCount(1);
        accounts[0].AccountName.Should().Be("alpha");
        accounts[0].PersonaName.Should().Be("alpha");
    }

    [Fact]
    public void GetKnownAccounts_SkipsEntriesWithBlankAccountName()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" ""
                    "PersonaName" "Phantom"
                    "Timestamp"   "1700000000"
                }
                "76561198000000002"
                {
                    "AccountName" "real"
                    "PersonaName" "Real"
                    "Timestamp"   "1715000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Should().HaveCount(1);
        accounts[0].AccountName.Should().Be("real");
    }

    [Fact]
    public async Task SwitchToAsync_FullSequence_KillsThenRewritesVdfThenWritesRegistryThenLaunches()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "RememberPassword" "1" "MostRecent" "0" "Timestamp" "1714000000"
                }
                "76561198000000002"
                {
                    "AccountName" "bravo" "PersonaName" "B" "RememberPassword" "1" "MostRecent" "1" "Timestamp" "1715000000"
                }
            }
            """);
        var (service, registryWriter, _, processController, _) = BuildService();

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.Ok);
        result.AccountName.Should().Be("alpha");
        Received.InOrder(() =>
        {
            processController.KillAllSteamProcesses(Arg.Any<TimeSpan>());
            registryWriter.WriteCurrentUserString(@"Software\Valve\Steam", "AutoLoginUser", "alpha");
            registryWriter.WriteCurrentUserDword(@"Software\Valve\Steam", "RememberPassword", 1);
            processController.LaunchSteamCold();
        });
    }

    [Fact]
    public async Task SwitchToAsync_RewritesLoginUsersVdf_FlipsMostRecentAndAutoLoginFlags()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "RememberPassword" "0" "AllowAutoLogin" "0" "MostRecent" "0" "Timestamp" "1714000000"
                }
                "76561198000000002"
                {
                    "AccountName" "bravo" "PersonaName" "B" "RememberPassword" "1" "AllowAutoLogin" "1" "MostRecent" "1" "Timestamp" "1715000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.Ok);
        var reread = new VdfParser().Parse(await File.ReadAllTextAsync(LoginUsersPath));
        var users = reread.GetObject("users")!;
        var alpha = users.GetObject("76561198000000001")!;
        var bravo = users.GetObject("76561198000000002")!;
        alpha.GetString("MostRecent").Should().Be("1");
        alpha.GetString("AllowAutoLogin").Should().Be("1");
        alpha.GetString("RememberPassword").Should().Be("1");
        bravo.GetString("MostRecent").Should().Be("0");
        bravo.GetString("AllowAutoLogin").Should().Be("1");
        bravo.GetString("RememberPassword").Should().Be("1");
    }

    [Fact]
    public async Task SwitchToAsync_WritesBackupBeforeOverwrite()
    {
        var original = """
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "0" "Timestamp" "1714000000"
                }
                "76561198000000002"
                {
                    "AccountName" "bravo" "PersonaName" "B" "MostRecent" "1" "Timestamp" "1715000000"
                }
            }
            """;
        WriteLoginUsers(original);
        var (service, _, _, _, _) = BuildService();

        await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        var backupPath = LoginUsersPath + "_last";
        File.Exists(backupPath).Should().BeTrue();
        (await File.ReadAllTextAsync(backupPath)).Should().Be(original);
    }

    [Fact]
    public async Task SwitchToAsync_SteamNotInstalled_ReturnsSteamNotInstalledAndDoesNothing()
    {
        var (service, registryWriter, _, processController, _) = BuildService(steamInstalled: false);

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.SteamNotInstalled);
        processController.DidNotReceive().KillAllSteamProcesses(Arg.Any<TimeSpan>());
        registryWriter.DidNotReceive().WriteCurrentUserString(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        processController.DidNotReceive().LaunchSteamCold();
    }

    [Fact]
    public async Task SwitchToAsync_AccountMissing_ReturnsAccountNotFoundAndDoesNothing()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1714000000"
                }
            }
            """);
        var (service, registryWriter, _, processController, _) = BuildService();

        var result = await service.SwitchToAsync(76561198999999999UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.AccountNotFound);
        processController.DidNotReceive().KillAllSteamProcesses(Arg.Any<TimeSpan>());
        registryWriter.DidNotReceive().WriteCurrentUserString(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SwitchToAsync_KillThrows_ReturnsKillFailedAndDoesNotProceed()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1714000000"
                }
            }
            """);
        var (service, registryWriter, _, processController, _) = BuildService();
        processController
            .When(p => p.KillAllSteamProcesses(Arg.Any<TimeSpan>()))
            .Do(_ => throw new InvalidOperationException("kill blew up"));

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.KillFailed);
        registryWriter.DidNotReceive().WriteCurrentUserString(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        processController.DidNotReceive().LaunchSteamCold();
    }

    [Fact]
    public async Task SwitchToAsync_RegistryWriteThrows_ReturnsRegistryWriteFailedAndDoesNotLaunch()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1714000000"
                }
            }
            """);
        var (service, registryWriter, _, processController, _) = BuildService();
        registryWriter
            .When(w => w.WriteCurrentUserString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new UnauthorizedAccessException("no write access"));

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.RegistryWriteFailed);
        processController.DidNotReceive().LaunchSteamCold();
    }

    [Fact]
    public async Task SwitchToAsync_GameRunning_ReturnsGameRunningAndDoesNothing()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1714000000"
                }
            }
            """);
        var (service, registryWriter, _, processController, _) = BuildService();
        processController.IsGameRunning().Returns(true);

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.GameRunning);
        processController.DidNotReceive().KillAllSteamProcesses(Arg.Any<TimeSpan>());
        registryWriter.DidNotReceive().WriteCurrentUserString(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        processController.DidNotReceive().LaunchSteamCold();
    }

    [Fact]
    public async Task SwitchToAsync_RegistryReadbackMismatch_ReturnsRegistryWriteFailed()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1714000000"
                }
            }
            """);
        var (service, _, registryReader, processController, _) = BuildService();
        registryReader
            .ReadCurrentUserString(@"Software\Valve\Steam", "AutoLoginUser")
            .Returns("someoneElse");

        var result = await service.SwitchToAsync(76561198000000001UL, CancellationToken.None);

        result.Status.Should().Be(SwitchOutcome.RegistryWriteFailed);
        processController.DidNotReceive().LaunchSteamCold();
    }

    [Fact]
    public void GetKnownAccounts_PopulatesAvatarPathWhenLocalFileExists()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A" "MostRecent" "1" "Timestamp" "1715000000"
                }
                "76561198000000002"
                {
                    "AccountName" "bravo" "PersonaName" "B" "MostRecent" "0" "Timestamp" "1714000000"
                }
            }
            """);
        var avatarDir = Path.Combine(_root, "config", "avatarcache");
        Directory.CreateDirectory(avatarDir);
        var alphaAvatar = Path.Combine(avatarDir, "76561198000000001.png");
        File.WriteAllBytes(alphaAvatar, [0x89, 0x50, 0x4E, 0x47]);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Single(a => a.AccountName == "alpha").AvatarPath.Should().Be(alphaAvatar);
        accounts.Single(a => a.AccountName == "bravo").AvatarPath.Should().BeNull();
    }

    [Fact]
    public void GetKnownAccounts_ParsesRememberPasswordFlag()
    {
        WriteLoginUsers("""
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "alpha" "PersonaName" "A"
                    "RememberPassword" "1"
                    "MostRecent" "0" "Timestamp" "1714000000"
                }
                "76561198000000002"
                {
                    "AccountName" "bravo" "PersonaName" "B"
                    "RememberPassword" "0"
                    "MostRecent" "1" "Timestamp" "1715000000"
                }
            }
            """);
        var (service, _, _, _, _) = BuildService();

        var accounts = service.GetKnownAccounts();

        accounts.Should().HaveCount(2);
        accounts.Single(a => a.AccountName == "alpha").RememberPassword.Should().BeTrue();
        accounts.Single(a => a.AccountName == "bravo").RememberPassword.Should().BeFalse();
    }
}
