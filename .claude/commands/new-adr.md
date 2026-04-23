---
description: Draft a new ADR in Nygard format (does not commit it)
argument-hint: <title>
allowed-tools: Bash(ls:*), Read
---

Draft an Architecture Decision Record.

Argument: `$ARGUMENTS` — the ADR title.

Steps:

1. List `docs/adr/` to find the highest existing ADR number. New number = that + 1, zero-padded to 4 digits.
2. Build the filename: `NNNN-<kebab-case-title>.md`.
3. **Do not write the file directly** — the path guard blocks agent writes under `docs/adr/`. Instead, output the full Markdown content in the chat for the user to copy into the file themselves.
4. Use this structure (Nygard format):

   ```
   # NNNN. <Title>

   Date: YYYY-MM-DD
   Status: Proposed

   ## Context

   <What is the issue that motivates this decision?>

   ## Decision

   <What is the change being proposed or made?>

   ## Consequences

   <What becomes easier or harder as a result?>
   ```

5. Fill in Context, Decision, and Consequences based on the current conversation. If the conversation doesn't contain enough detail for any section, write `TODO: <what's missing>` rather than inventing content.
6. Remind the user at the end: "Copy this into `docs/adr/NNNN-....md` when you're ready. ADRs are human-committed."
