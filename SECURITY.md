# Security policy

Sherlog will run a background service with administrator rights and apply fixes to people's PCs, so we take security reports seriously.

## Reporting a vulnerability

Please report security problems **privately** through GitHub:

1. Go to the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Describe the issue, how to reproduce it, and what an attacker could do with it.

Please don't open a public issue, discussion or pull request for a security problem until it has been fixed.

We aim to acknowledge reports within 7 days and to keep you updated until the issue is resolved. We're happy to credit you in the release notes if you'd like.

## What's in scope

- The Sherlog service, its local communication channel (IPC) and the fix engine
- The approved-actions list and how recipes are loaded, validated and signed
- The command-line tool, the app and the MCP server
- Release and update workflows

## Supported versions

Sherlog has no released versions yet. Once it does, security fixes will go to the latest release.
