# Field test (Phase 1)

For the project owner. The field test collects crash records from **10–20 friends' Windows PCs** (gaming PCs
preferred), so we can check Sherlog's diagnoses against what really happened. Gate 1 (`plan.md` §7) needs at least
10 returned bundles whose cause we can label `confirmed-by-fix` or `owner-reported`. Tracking issue: #10.

Friends get a one-page guide: [field-test-friend.md](field-test-friend.md).

## 1. Get the kit

Every CI run on `main` builds the kit as the artifact `sherlog-field-test`: one self-contained `sherlog.exe` (no .NET
install needed) with the recipe `library` folder next to it. GitHub keeps artifacts for 90 days.

On the website: open the repo's **Actions** tab, open the latest green **CI** run on `main`, and download
`sherlog-field-test` from **Artifacts** at the bottom of the page.

Or in PowerShell, in one window. Get the ID of the latest green run on `main`:

```powershell
$id = gh run list --repo TheAlsaadi/sherlog --workflow CI --branch main --status success --limit 1 --json databaseId --jq '.[0].databaseId'
```

Download the kit into an empty folder **outside the repo**:

```powershell
gh run download $id --repo TheAlsaadi/sherlog --name sherlog-field-test --dir C:\SherlogField\kit
```

Zip it for sending:

```powershell
Compress-Archive -Path C:\SherlogField\kit\* -DestinationPath C:\SherlogField\sherlog-field-test.zip
```

The zip is about 32 MB, too big for most email. Share it with a cloud-drive link (OneDrive, Google Drive) or a USB
stick. A public GitHub pre-release would also work, but it makes the test build public, so that's your call: Claude
Code won't create one without your OK.

Try it once yourself first: unzip it and double-click `sherlog.exe` to see what your friends will see. It only reads
crash records and saves a file on your Desktop.

## 2. Send it to friends

Send each friend the zip and the friend guide (the link
`https://github.com/TheAlsaadi/sherlog/blob/main/docs/field-test-friend.md` or the text itself). Tell them:

- it takes about 5 minutes and changes nothing on their PC;
- the build is unsigned, so Windows will show "Windows protected your PC" and they click **More info**, then
  **Run anyway**. If there's no **Run anyway** button (Smart App Control or antivirus blocked it), they should stop
  and tell you. Never ask anyone to turn off a protection;
- double-clicking runs the questions and saves `sherlog-evidence-<date>.zip` on their Desktop, which they send back.

Friends who are comfortable with a terminal can run `sherlog collect --out <file>` instead; it's the same file.

## 3. Receive the bundles

Keep every returned bundle in one folder **outside the repo**, for example `C:\SherlogField\bundles\`. Never commit
bundles, paste their contents into issues, or attach them to pull requests.

Rename each file to a code, not the friend's name: `friend-01.zip`, `friend-02.zip` and so on. Keep the list of who is
who somewhere private, outside the repo.

### What a bundle contains (the privacy promise, in plain words)

Sherlog replaces the user name, the PC's name, the account ID (SID), user-profile folders and email addresses with
placeholders before it saves the file. What's left: which apps and games crashed and when, Reliability Monitor's list of
recent app, driver and Windows update installs, the Windows version, the hardware (processor, graphics card, memory),
and the friend's answers. Friends can open the zip and check it with Notepad before sending. Sherlog never sends
anything by itself.

## 4. Investigate

Ask Claude Code to investigate the bundles in your folder (Task 16 of `docs/plans/phase-1-prove-it.md`). It reads
only `sherlog crashes --bundle <zip> --json`, the bundle's `system.json` and the friend's answers, never the raw
records, and fills in the investigator labels in each bundle's `label.yaml` with its evidence.

## 5. Follow up a week later

Where a friend tried a fix (a new driver, turning off an overlay, removing an overclock), message them about a week
later: "Has that crash happened again since you tried the fix?" Pass the answer to Claude Code. If it stopped for
7 days or more, the label becomes `confirmed-by-fix`, the strongest kind.

## 6. Score it

When the bundles are labeled:

```powershell
dotnet run --project src/Sherlog.Cli -- eval C:\SherlogField\bundles
```

The results and every miss go into [gates/phase-1.md](gates/phase-1.md).
