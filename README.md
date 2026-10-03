# Agent Wall

Run up to N Claude Code agents in parallel and watch them on a live terminal grid.
Python 3.8+, stdlib only. Needs the `claude` CLI (not needed for `--demo`).

```sh
./agent_wall.py --demo                          # 10 simulated agents
./agent_wall.py --tasks tasks.example.json      # 10 real agents (default model alias: opus)
./agent_wall.py -n 10 "Review src/ for bugs"    # one prompt, 10 agents
./agent_wall.py --last                          # re-show the wall + results of the last run
```

Each cell shows the agent's status, elapsed time, tool count, and live output.
Ctrl-C stops all agents. Logs land in `runs/<timestamp>/` (`results.md`, `run.json`, raw `*.jsonl`).

| Option | Purpose |
|---|---|
| `-m, --model` | model alias/ID (default `$AGENT_WALL_MODEL` or `opus`) |
| `-p, --parallel` | max agents running at once |
| `--permission-mode` | default `acceptEdits` (or `$AGENT_WALL_PERMISSION_MODE`); `bypassPermissions`, `plan`, or `''` to omit |
| `--claude-args` | extra `claude` flags, e.g. `"--allowedTools 'Read Grep'"` |
| `--cols` | fixed grid columns |
| `--plain` | no grid; print status changes (auto when not a TTY) |

Tasks file: JSON list of prompts or `{"name", "prompt", "cwd"}` objects.
