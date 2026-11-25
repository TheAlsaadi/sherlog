# Sherlog

**Windows PC health, explained.** Sherlog is a free, open-source Windows app that answers three questions in plain language:

- **Why did it crash?**
- **Why is it slow?**
- **Is my PC safe and healthy?**

Then it offers a fix for the cause, and you can undo every fix.

> **Status: early development.** Nothing here is ready to install yet. The first milestone is a command-line tool that reads the crash records Windows already keeps and explains them. Follow along in [plan.md](plan.md).

## Why

Windows already writes down most of the answers: crash records, Reliability Monitor, performance counters, drive health. They're just scattered, technical, and never explained. Cleaner apps treat symptoms, expert tools show raw data, and forum advice is often a guess.

Sherlog works like a flight recorder. It quietly keeps track of crashes, performance and every change made to the PC (driver updates, new apps, new startup items). When something goes wrong, the evidence is already there, and lining up "what changed" against "when it broke" usually points straight at the cause.

## How it behaves

- It names the cause in one sentence and tells you how sure it is: *Confirmed*, *Likely* or *Unclear*. If it doesn't know, it says so.
- Before a fix runs, Sherlog tells you what will change and saves an undo record. Afterwards the fix stays in your history with an Undo button.
- Fixes come from a public library of reviewed recipes, and a recipe can only use actions from a short approved list. Sherlog never runs code from the library.
- Analysis happens on your PC and crash dumps never leave it. There are no accounts, ads or tracking.
- The recorder is built to use very little CPU and memory, and it backs off while you play.
- It measures games from the outside, through Windows' own logs and tracing. It never injects code into them, which keeps it clear of anti-cheat systems.

Gamers are the first audience, but the same app works for anyone on Windows.

## Learn more

- [Product concept](docs/concept.md): the full idea, the three questions and the fix system
- [Build plan](plan.md): phases, gates and what's being worked on now
- [Contributing](CONTRIBUTING.md) · [Security policy](SECURITY.md) · [Code of conduct](CODE_OF_CONDUCT.md)

## License

Code: [MIT](LICENSE). Recipe library (`library/`): [CC BY 4.0](library/LICENSE).
