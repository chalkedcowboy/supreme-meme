---
name: bloodborn
description: Use when asked where code came from, why a line, function, or file looks the way it does, who introduced a bug, or what its ancestry is across renames, moves, and refactors
---

# Bloodborn

## Overview
Trace a symbol's full ancestry through git, following renames and moves, until the originating commit and each mutation are known. Report facts from history; never infer from current code alone.

## Procedure
1. Locate target: `file:line`, symbol, or file.
2. Line/function history: `git log -L :<symbol>:<file> --follow` or `git log -L <start>,<end>:<file>`.
3. Whole-file lineage: `git log --follow --name-status -- <file>`.
4. Deleted/moved symbol: `git log -S'<text>' --all --oneline`, then `-G'<regex>'` for edits.
5. Per-line origin: `git blame -w -C -C -C -L <start>,<end> <file>`; repeat on the parent (`<sha>^`) until the line stops changing meaning.
6. Read each candidate commit: `git show <sha> --stat` plus relevant hunk.

## Output
Chronological chain, oldest first, one line each:
`<sha7> <date> <author> — <what changed and why>`
End with: origin commit, last meaningful mutation, and any gap (squash, shallow clone, history rewrite).

## Common Mistakes
| Mistake | Fix |
|---|---|
| Blaming the reformat commit | Use `-w` and `-C`; skip to parent |
| Missing renames | `--follow`, `-M`/`-C` |
| Shallow clone hides origin | `git fetch --unshallow`, state it if impossible |
| Stopping at first hit | Walk back to the introducing commit |
