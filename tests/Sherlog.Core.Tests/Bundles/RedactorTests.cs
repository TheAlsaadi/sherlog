using Sherlog.Core.Bundles;

namespace Sherlog.Core.Tests.Bundles;

public class RedactorTests
{
    private static readonly Redactor Redactor = new(new RedactionTargets("TESTPC", "CORP", ["TESTUSER", "OTHERUSER", "Sam", "Sam Smith", "jo", "王伟", "j"]));

    [Theory]
    [InlineData(@"C:\Users\TESTUSER\Games\x.exe", @"C:\Users\USER\Games\x.exe")]
    [InlineData(@"c:\users\Someone Else\AppData\x.dll", @"c:\users\USER\AppData\x.dll")]
    [InlineData("C:/Users/TESTUSER/Games/x.exe", "C:/Users/USER/Games/x.exe")]
    [InlineData(@"{""path"":""C:\\Users\\TESTUSER\\x.exe""}", @"{""path"":""C:\\Users\\USER\\x.exe""}")]
    [InlineData(@"D:\Users\TESTUSER\x", @"D:\Users\USER\x")]
    public void Profile_path_user_is_replaced(string input, string expected)
    {
        Assert.Equal(expected, Redactor.Redact(input));
    }

    [Fact]
    public void Other_local_profiles_are_replaced()
    {
        Assert.Equal("USER signed in, then USER.", Redactor.Redact("OTHERUSER signed in, then testuser."));
    }

    [Fact]
    public void Machine_name_in_xml_Computer_element_is_replaced()
    {
        Assert.Equal("<Computer>PC</Computer> \\\\PC\\share", Redactor.Redact("<Computer>TESTPC</Computer> \\\\TESTPC\\share"));
    }

    [Fact]
    public void Domain_name_is_replaced()
    {
        Assert.Equal(@"DOMAIN\USER", Redactor.Redact(@"CORP\TESTUSER"));
    }

    [Fact]
    public void Account_sid_is_replaced_system_sid_kept()
    {
        var text = "<Security UserID=\"S-1-5-21-1111111111-2222222222-3333333333-1001\"/> S-1-5-18 S-1-12-1-111-222-333-444";

        var redacted = Redactor.Redact(text);

        Assert.Equal("<Security UserID=\"S-1-5-REDACTED\"/> S-1-5-18 S-1-5-REDACTED", redacted);
    }

    [Fact]
    public void Names_are_case_insensitive_whole_words()
    {
        Assert.Equal("USER's PC has a Samsung SSD", Redactor.Redact("sam's PC has a Samsung SSD"));
    }

    [Fact]
    public void Two_letter_names_are_replaced_as_whole_words()
    {
        Assert.Equal("USER went home, USER note, joke", Redactor.Redact("jo went home, 王伟 note, joke"));
    }

    [Fact]
    public void One_letter_name_only_replaced_in_paths()
    {
        Assert.Equal(@"j went home C:\Users\USER\x", Redactor.Redact(@"j went home C:\Users\j\x"));
    }

    [Theory]
    [InlineData(@"\Device\HarddiskVolume3\Users\Someone\x.dll", @"\Device\HarddiskVolume3\Users\USER\x.dll")]
    [InlineData(@"\\host\c$\Users\Someone\x.dll", @"\\host\c$\Users\USER\x.dll")]
    [InlineData(@"C:\Documents and Settings\Someone\x.dll", @"C:\Documents and Settings\USER\x.dll")]
    public void Profile_paths_without_a_plain_drive_letter_are_replaced(string input, string expected)
    {
        Assert.Equal(expected, Redactor.Redact(input));
    }

    [Fact]
    public void Path_rule_stops_at_the_end_of_the_name()
    {
        Assert.Equal("C:\\Users\\USER\u001fgame.exe\u001f1.0", Redactor.Redact("C:\\Users\\AJ\u001fgame.exe\u001f1.0"));
        Assert.Equal(@"files in C:\Users\USER are gone", Redactor.Redact(@"files in C:\Users\Someone are gone"));
    }

    [Fact]
    public void Known_name_with_a_space_is_replaced_whole_inside_paths()
    {
        Assert.Equal(@"C:\Users\USER\x.exe", Redactor.Redact(@"C:\Users\Sam Smith\x.exe"));
    }

    [Fact]
    public void Computer_element_is_replaced_whatever_its_content()
    {
        // The NetBIOS name is cut to 15 characters; the event holds the full DNS name.
        var redactor = new Redactor(new RedactionTargets("BOBS-GAMING-DES", null, []));

        Assert.Equal("<Computer>PC</Computer>", redactor.Redact("<Computer>BOBS-GAMING-DESKTOP.corp.contoso.com</Computer>"));
    }

    [Fact]
    public void Full_host_and_dns_domain_names_are_replaced()
    {
        var redactor = new Redactor(new RedactionTargets("BOBS-GAMING-DES", null, [], ["BOBS-GAMING-DESKTOP"], ["corp.contoso.com"]));

        Assert.Equal("Reached PC.DOMAIN and PC", redactor.Redact("Reached BOBS-GAMING-DESKTOP.corp.contoso.com and bobs-gaming-desktop"));
    }

    [Fact]
    public void Email_addresses_are_replaced()
    {
        Assert.Equal("Signed in as user@example.invalid.", Redactor.Redact("Signed in as test.user+x@mail.example.com."));
    }

    [Fact]
    public void Text_without_identifiers_is_unchanged()
    {
        const string text = "Faulting application name: SherlogTestGame.exe, version: 1.2.3.4, exception code 0xc0000005";

        Assert.Equal(text, Redactor.Redact(text));
    }

    [Fact]
    public void ForThisPc_includes_current_user_and_machine()
    {
        // Real identifiers: assert on booleans so a failure never prints them (Assert.Equal/Contains would).
        var targets = RedactionTargets.ForThisPc();

        var machineTargeted = string.Equals(targets.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        var userTargeted = targets.UserNames.Any(n => string.Equals(n, Environment.UserName, StringComparison.OrdinalIgnoreCase));
        var userRedacted = Environment.UserName.Length < 3
                           || new Redactor(targets).Redact($"hello {Environment.UserName}") == "hello USER";

        Assert.True(machineTargeted, "This PC's name is not a redaction target.");
        Assert.True(userTargeted, "The signed-in user's name is not a redaction target.");
        Assert.True(userRedacted, "The signed-in user's name survives redaction.");
        Assert.DoesNotContain("Public", targets.UserNames);
    }
}
