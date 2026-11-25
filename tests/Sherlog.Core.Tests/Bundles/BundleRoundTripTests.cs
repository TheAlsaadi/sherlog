using System.IO.Compression;
using Sherlog.Core.Bundles;
using Sherlog.Core.Evidence;
using Sherlog.Core.Grouping;

namespace Sherlog.Core.Tests.Bundles;

public sealed class BundleRoundTripTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = T0.AddDays(-90);
    private static readonly SystemSummary System = new("Windows 11 Pro", "26100", "Test CPU", 32,
        [new GpuInfo("Test GPU", "32.0.15.1234", "2026-11-01")]);

    private readonly string _dir = Directory.CreateTempSubdirectory("sherlog-bundle-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string ZipPath => Path.Combine(_dir, "evidence.zip");

    private static readonly Redactor TestRedactor =
        new(new RedactionTargets("TESTPC", "TESTDOMAIN", ["TESTUSER", "سامي"]));

    private static LabelFile Label(params CrashLabel[] labels) => new(OwnerAnswers.Empty, new InvestigatorSection(false, labels));

    [Fact]
    public void Bundle_round_trip_preserves_records_and_manifest()
    {
        var source = new FakeRecordSource().AddAppError(1, T0).AddReliabilityAppFailure(901, T0.AddDays(-2));
        source.Events.Add(Fixture.Event("System", "display-4101.xml", "Display driver nvlddmkm stopped responding"));

        BundleWriter.Write(ZipPath, source, Since, System, Label(), TestRedactor, "0.1.0-test", T0);
        var bundle = BundleReader.Open(ZipPath);

        Assert.Equal(1, bundle.Manifest.Format);
        Assert.True(bundle.Manifest.Redacted);
        Assert.Equal(2, bundle.Manifest.EventCount);
        Assert.Equal(1, bundle.Manifest.ReliabilityCount);
        Assert.Equal(T0, bundle.Manifest.CreatedUtc);
        Assert.Equal(Since, bundle.Manifest.SinceUtc);
        Assert.Equal("0.1.0-test", bundle.Manifest.SherlogVersion);
        Assert.Equal(System.Gpus, bundle.System.Gpus);
        Assert.Equal(2, bundle.Records.ReadEvents(DateTimeOffset.MinValue).Count());
        Assert.Equal("Display driver nvlddmkm stopped responding",
            bundle.Records.ReadEvents(DateTimeOffset.MinValue).Single(r => r.Log == "System").Message);
        Assert.Single(bundle.Records.ReadReliability(DateTimeOffset.MinValue));
    }

    [Fact]
    public void Bundle_contains_no_original_identifiers()
    {
        var source = new FakeRecordSource().AddAppError(1, T0);
        source.Events[0] = source.Events[0] with
        {
            Message = @"Faulting application path: C:\Users\TESTUSER\Games\x.exe on TESTPC by TESTDOMAIN\TESTUSER, سامي",
        };
        source.AddReliabilityAppFailure(901, T0.AddDays(-2));
        var props = new Dictionary<string, string?>(source.Reliability[0].Properties)
        {
            ["Message"] = @"C:\Users\TESTUSER\AppData\Local\x.dll crashed. Contact testuser@mail.example.com",
        };
        source.Reliability[0] = new RawReliabilityRecord(props);
        var system = System with { Cpu = "TESTPC's CPU" };
        var label = new LabelFile(OwnerAnswers.Empty with { Note = "My name is TESTUSER" }, new InvestigatorSection(false, []));

        BundleWriter.Write(ZipPath, source, Since, system, label, TestRedactor, "0.1.0-test", T0);

        using var zip = ZipFile.OpenRead(ZipPath);
        string[] forbidden = ["TESTUSER", "TESTPC", "TESTDOMAIN", "سامي", "S-1-5-21-1111111111", "mail.example.com"];
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            foreach (var word in forbidden)
            {
                Assert.False(text.Contains(word, StringComparison.OrdinalIgnoreCase), $"'{word}' survived in {entry.FullName}");
            }
        }
    }

    [Fact]
    public void Owner_answers_survive_a_profile_named_like_a_label_key()
    {
        // OEM PCs often have a profile named "Owner"; redacting the YAML text would turn "owner:" into "USER:".
        var redactor = new Redactor(new RedactionTargets("TESTPC", null, ["Owner", "note", "doing"]));
        var label = new LabelFile(
            OwnerAnswers.Empty with { Doing = "Playing a game", Note = "The Owner of this PC plays a lot" },
            new InvestigatorSection(false, [new CrashLabel("g-74537a", "Owner.exe", "unknown", "Owner said so", "unknown")]));

        BundleWriter.Write(ZipPath, new FakeRecordSource(), Since, System, label, redactor, "0.1.0-test", T0);
        var read = BundleReader.Open(ZipPath).Label;

        Assert.Equal("Playing a game", read.Owner.Doing);
        Assert.Equal("The USER of this PC plays a lot", read.Owner.Note);
        Assert.Equal("USER said so", Assert.Single(read.Investigator.Labels).Evidence);
    }

    [Fact]
    public void Bundle_reader_source_diagnoses_like_live()
    {
        var source = new FakeRecordSource().AddAppError(1, T0).AddAppError(2, T0.AddDays(-1), module: "Other.dll");
        source.Events.Add(Fixture.Event("System", "display-4101.xml"));

        BundleWriter.Write(ZipPath, source, Since, System, Label(), TestRedactor, "0.1.0-test", T0);
        var fromBundle = CrashGrouper.Group(new EvidenceReader(BundleReader.Open(ZipPath).Records).ReadCrashes(Since));
        var fromLive = CrashGrouper.Group(new EvidenceReader(source).ReadCrashes(Since));

        Assert.Equal(fromLive.Select(g => g.Id), fromBundle.Select(g => g.Id));
    }

    [Fact]
    public void Records_older_than_since_are_not_written()
    {
        var source = new FakeRecordSource().AddAppError(1, T0).AddAppError(2, Since.AddDays(-1));

        BundleWriter.Write(ZipPath, source, Since, System, Label(), TestRedactor, "0.1.0-test", T0);

        Assert.Equal(1, BundleReader.Open(ZipPath).Manifest.EventCount);
    }

    [Fact]
    public void Label_is_readable_from_the_bundle()
    {
        var label = Label(new CrashLabel("g-74537a", "SherlogTestGame.exe", "unknown", null, "unknown"));

        BundleWriter.Write(ZipPath, new FakeRecordSource(), Since, System, label, TestRedactor, "0.1.0-test", T0);

        Assert.Equal(label.Investigator.Labels, BundleReader.Open(ZipPath).Label.Investigator.Labels);
    }

    [Fact]
    public void A_failed_write_leaves_no_partial_file()
    {
        var folderAsTarget = Directory.CreateDirectory(Path.Combine(_dir, "taken.zip")).FullName;

        // Windows reports this as access denied (UnauthorizedAccessException); what matters is the cleanup.
        Assert.ThrowsAny<Exception>(() =>
            BundleWriter.Write(folderAsTarget, new FakeRecordSource(), Since, System, Label(), TestRedactor, "t", T0));

        Assert.False(File.Exists(folderAsTarget + ".partial"));
    }

    [Fact]
    public void Not_a_bundle_is_invalid_data()
    {
        File.WriteAllText(ZipPath, "not a zip");

        Assert.Throws<InvalidDataException>(() => BundleReader.Open(ZipPath));
    }
}
