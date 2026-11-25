# ADR 0005: Redaction is always on, with fixed placeholders

- **Status:** Accepted
- **Date:** 2026-11-25

`sherlog collect` always redacts; `--redact` is accepted but there is no way to turn redaction off. Before anything is written to a bundle, every raw record and the system summary are scrubbed: the PC name becomes `PC`, the names of all local user profiles and the signed-in user become `USER`, Windows account SIDs become `S-1-5-REDACTED`, the domain name becomes `DOMAIN`, and any path under the profiles folder keeps its shape (`C:\Users\USER\...`). Placeholders are fixed words, not hashes, so nobody can match a bundle to a person by comparing hashes.

We chose "always on" because bundles are made by friends who will not read the fine print, and one bundle with a real name in it would break the project's privacy promise. A missed pattern is a bug with a test, never a setting.

## Consequences

- Investigators sometimes lose detail (for example which of two user accounts ran a game). That is acceptable for crash diagnosis.
- Redaction has its own fixture tests, including a "no original value survives" check over the whole bundle.
- `collect` prints what it collected and where the file is, so the friend can look inside before sending it.
