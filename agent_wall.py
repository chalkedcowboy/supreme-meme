#!/usr/bin/env python3
"""Agent status wall: run Claude Code agents in parallel and watch them on a live grid.

Examples:
  ./agent_wall.py --demo                       # 10 simulated agents, no Claude needed
  ./agent_wall.py --tasks tasks.example.json   # 10 real agents
  ./agent_wall.py -n 5 "Review src/ for bugs"  # same prompt, 5 agents
  ./agent_wall.py --last                       # show the wall of the last run
"""
from __future__ import annotations

import argparse
import json
import math
import os
import queue
import random
import re
import shlex
import shutil
import signal
import subprocess
import sys
import threading
import time
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

MAX_LINES = 200
MIN_CELL_W = 44
ANSI_RE = re.compile(r"\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b[@-_]")
CTRL_RE = re.compile(r"[\x00-\x08\x0b-\x1f\x7f]")
STATUS_COLOR = {"queued": "90", "running": "33", "done": "32", "failed": "31", "stopped": "35"}


@dataclass
class Agent:
    idx: int
    name: str
    prompt: str
    cwd: str | None = None
    status: str = "queued"
    lines: list = field(default_factory=list)
    tools: int = 0
    cost: float | None = None
    started: float | None = None
    ended: float | None = None
    result: str = ""
    proc: subprocess.Popen | None = None
    log_path: Path | None = None
    lock: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def add(self, text: str) -> None:
        with self.lock:
            for raw in str(text).splitlines() or [""]:
                self.lines.append(clean(raw))
            del self.lines[:-MAX_LINES]

    def snapshot(self) -> list:
        with self.lock:
            return list(self.lines)

    def elapsed(self) -> float:
        if not self.started:
            return 0.0
        return (self.ended or time.time()) - self.started


# ---------------------------------------------------------------- text utils

def clean(s: str) -> str:
    return CTRL_RE.sub("", ANSI_RE.sub("", s.replace("\t", "    ")))


def char_w(c: str) -> int:
    if unicodedata.combining(c):
        return 0
    return 2 if unicodedata.east_asian_width(c) in "WF" else 1


def disp_w(s: str) -> int:
    return sum(char_w(c) for c in s)


def fit(s: str, w: int) -> str:
    """Truncate/pad s to exactly w display columns."""
    out, used = [], 0
    for c in s:
        cw = char_w(c)
        if used + cw > w:
            break
        out.append(c)
        used += cw
    return "".join(out) + " " * (w - used)


def wrap(s: str, w: int) -> list:
    if w <= 0:
        return []
    rows, cur, used = [], [], 0
    for c in s:
        cw = char_w(c)
        if used + cw > w:
            rows.append("".join(cur))
            cur, used = [], 0
        cur.append(c)
        used += cw
    rows.append("".join(cur))
    return rows


def color(s: str, code: str | None, enabled: bool = True) -> str:
    return f"\x1b[{code}m{s}\x1b[0m" if code and enabled else s


def mmss(sec: float) -> str:
    sec = int(sec)
    return f"{sec // 60:02d}:{sec % 60:02d}"


def slug(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")[:40] or "agent"


def summarize_input(inp) -> str:
    if isinstance(inp, dict):
        for key in ("command", "file_path", "pattern", "url", "query", "path", "description", "prompt"):
            if inp.get(key):
                return str(inp[key]).replace("\n", " ")
        return json.dumps(inp)
    return str(inp)


# ---------------------------------------------------------------- events

def handle_event(agent: Agent, ev: dict) -> None:
    t = ev.get("type")
    if t == "system" and ev.get("subtype") == "init":
        agent.add(f"· started  model={ev.get('model', '?')}")
    elif t == "assistant":
        for block in ev.get("message", {}).get("content", []):
            if block.get("type") == "text" and block.get("text", "").strip():
                agent.add(block["text"])
            elif block.get("type") == "tool_use":
                agent.tools += 1
                agent.add(f"→ {block.get('name')} {summarize_input(block.get('input'))}")
    elif t == "user":
        content = ev.get("message", {}).get("content", [])
        if isinstance(content, list):
            for block in content:
                if isinstance(block, dict) and block.get("type") == "tool_result" and block.get("is_error"):
                    agent.add("! tool error")
    elif t == "result":
        agent.cost = ev.get("total_cost_usd")
        agent.result = ev.get("result") or ""
        agent.status = "failed" if ev.get("is_error") else "done"
        agent.add(f"{'✗' if ev.get('is_error') else '✓'} {ev.get('subtype', 'finished')}"
                  f"  turns={ev.get('num_turns', '?')}")


# ---------------------------------------------------------------- runners

def run_claude(agent: Agent, args, stop: threading.Event) -> None:
    cmd = [args.claude_bin, "-p", agent.prompt, "--output-format", "stream-json", "--verbose"]
    if args.model:
        cmd += ["--model", args.model]
    if args.permission_mode:
        cmd += ["--permission-mode", args.permission_mode]
    cmd += shlex.split(args.claude_args)
    agent.status, agent.started = "running", time.time()
    try:
        agent.proc = subprocess.Popen(
            cmd, cwd=agent.cwd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, bufsize=1, start_new_session=True)
    except OSError as e:
        agent.add(f"! {e}")
        agent.status, agent.ended = "failed", time.time()
        return
    with open(agent.log_path, "w", encoding="utf-8") as log:
        for line in agent.proc.stdout:
            log.write(line)
            line = line.strip()
            if not line:
                continue
            try:
                ev = json.loads(line)
            except json.JSONDecodeError:
                agent.add(line)
                continue
            if isinstance(ev, dict):
                handle_event(agent, ev)
    rc = agent.proc.wait()
    if stop.is_set() and agent.status == "running":
        agent.status = "stopped"
    elif agent.status == "running":
        agent.status = "done" if rc == 0 else "failed"
        if rc:
            agent.add(f"! exit code {rc}")
    agent.ended = time.time()


DEMO_TOOLS = [
    ("Read", {"file_path": "src/server.py"}), ("Grep", {"pattern": "TODO|FIXME"}),
    ("Bash", {"command": "pytest -q tests/"}), ("Edit", {"file_path": "src/api/routes.py"}),
    ("Glob", {"pattern": "**/*.ts"}), ("Bash", {"command": "npm run lint"}),
    ("WebSearch", {"query": "python asyncio timeout best practice"}),
]
DEMO_TEXT = [
    "Looking at the project layout first.", "Found 3 call sites that need updating.",
    "Tests pass locally; checking edge cases.", "The config loader ignores env overrides.",
    "Refactoring the retry loop into a helper.", "No regressions in the lint output.",
]


def run_demo(agent: Agent, args, stop: threading.Event) -> None:
    rnd = random.Random(agent.idx * 7919 + int(time.time()))
    agent.status, agent.started = "running", time.time()
    with open(agent.log_path, "w", encoding="utf-8") as log:
        def emit(ev):
            log.write(json.dumps(ev) + "\n")
            handle_event(agent, ev)

        emit({"type": "system", "subtype": "init", "model": "demo"})
        for _ in range(rnd.randint(5, 14)):
            if stop.wait(rnd.uniform(0.2, 1.2)):
                agent.status = "stopped"
                break
            if rnd.random() < 0.55:
                name, inp = rnd.choice(DEMO_TOOLS)
                block = {"type": "tool_use", "name": name, "input": inp}
            else:
                block = {"type": "text", "text": rnd.choice(DEMO_TEXT)}
            emit({"type": "assistant", "message": {"content": [block]}})
        else:
            failed = rnd.random() < 0.1
            emit({"type": "result", "subtype": "error_during_execution" if failed else "success",
                  "is_error": failed, "num_turns": rnd.randint(2, 9),
                  "total_cost_usd": round(rnd.uniform(0.01, 0.25), 4),
                  "result": f"Demo agent {agent.idx} {'failed' if failed else 'finished'}: {agent.prompt}"})
    agent.ended = time.time()


# ---------------------------------------------------------------- rendering

def render_cell(a: Agent, w: int, h: int, use_color: bool) -> list:
    iw = w - 2
    meta = f" {a.status} {mmss(a.elapsed())} tools:{a.tools} "
    title = f" {a.idx}. {a.name} "
    title = fit(title, max(0, iw - disp_w(meta) - 1)).rstrip() + " "
    top = "┌" + title + "─" * max(0, iw - disp_w(title) - disp_w(meta)) + meta + "┐"
    top = fit(top, w)
    border = STATUS_COLOR.get(a.status)
    out = [color(top, border, use_color)]

    cw = w - 4
    body_h = h - 2
    rows = []
    for line in a.snapshot():
        style = "36" if line.startswith("→") else "31" if line.startswith(("!", "✗")) else \
            "32" if line.startswith("✓") else "90" if line.startswith("·") else None
        rows += [(r, style) for r in wrap(line, cw)]
    if not rows and a.status == "queued":
        rows = [("waiting…", "90")]
    rows = rows[-body_h:] if body_h > 0 else []
    rows += [("", None)] * (body_h - len(rows))
    side = color("│", border, use_color)
    for text, style in rows:
        out.append(f"{side} {color(fit(text, cw), style, use_color)} {side}")
    out.append(color("└" + "─" * iw + "┘", border, use_color))
    return out


def render(agents: list, width: int, height: int, cols: int, clock: float,
           use_color: bool = True, label: str = "AGENT WALL") -> list:
    n = len(agents)
    cols = cols or max(1, min(n, width // MIN_CELL_W))
    rows = math.ceil(n / cols)
    cell_w = width // cols
    cell_h = max(4, (height - 1) // rows)

    counts = {s: sum(a.status == s for a in agents) for s in STATUS_COLOR}
    cost = sum(a.cost or 0 for a in agents)
    parts = [color(f"{counts[s]} {s}", STATUS_COLOR[s], use_color) for s in STATUS_COLOR if counts[s]]
    header = f" {label}  {mmss(clock)}  ${cost:.2f}  "
    lines = [color(header, "1", use_color) + " · ".join(parts)]

    for r in range(rows):
        cells = [render_cell(a, cell_w, cell_h, use_color) for a in agents[r * cols:(r + 1) * cols]]
        for i in range(cell_h):
            lines.append("".join(c[i] for c in cells))
    return lines[:height]


class Screen:
    def __enter__(self):
        sys.stdout.write("\x1b[?1049h\x1b[?25l")
        sys.stdout.flush()
        return self

    def draw(self, lines: list) -> None:
        sys.stdout.write("\x1b[H" + "\n".join(l + "\x1b[0m\x1b[K" for l in lines) + "\x1b[J")
        sys.stdout.flush()

    def __exit__(self, *exc):
        sys.stdout.write("\x1b[0m\x1b[?25h\x1b[?1049l")
        sys.stdout.flush()


# ---------------------------------------------------------------- run / summary

def load_agents(args) -> list:
    items = []
    if args.tasks:
        data = json.loads(Path(args.tasks).read_text(encoding="utf-8"))
        items = data.get("tasks", []) if isinstance(data, dict) else data
    items += args.prompts
    if len(items) == 1 and args.agents > 1:
        items = items * args.agents
    if not items and args.demo:
        items = [{"name": f"demo-{i}", "prompt": f"simulated task {i}"} for i in range(1, args.agents + 1)]
    if not items:
        sys.exit("agent_wall: no tasks. Pass prompts, --tasks FILE, or --demo.")

    agents = []
    for i, it in enumerate(items, 1):
        if isinstance(it, str):
            it = {"prompt": it}
        prompt = it["prompt"]
        name = it.get("name") or (prompt[:30] + ("…" if len(prompt) > 30 else ""))
        agents.append(Agent(idx=i, name=name, prompt=prompt, cwd=it.get("cwd") or args.cwd))
    return agents


def stop_all(agents: list) -> None:
    for a in agents:
        p = a.proc
        if p and p.poll() is None:
            try:
                os.killpg(p.pid, signal.SIGTERM)
            except (ProcessLookupError, PermissionError):
                pass


def save_run(agents: list, run_dir: Path, run_start: float) -> None:
    meta = {"started": run_start, "agents": [
        {"idx": a.idx, "name": a.name, "prompt": a.prompt, "status": a.status, "tools": a.tools,
         "cost": a.cost, "elapsed": round(a.elapsed(), 2), "log": a.log_path.name if a.log_path else None}
        for a in agents]}
    (run_dir / "run.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    md = [f"# Agent wall run {run_dir.name}\n"]
    for a in agents:
        md.append(f"## {a.idx}. {a.name} — {a.status} ({mmss(a.elapsed())}, "
                  f"{'$%.4f' % a.cost if a.cost is not None else 'n/a'})\n\n"
                  f"**Prompt:** {a.prompt}\n\n{a.result or '_no result_'}\n")
    (run_dir / "results.md").write_text("\n".join(md), encoding="utf-8")


def print_summary(agents: list, run_dir: Path, use_color: bool) -> None:
    name_w = min(32, max(disp_w(a.name) for a in agents))
    print(f"\n{'#':>3}  {fit('agent', name_w)}  {'status':<8} {'time':>5} {'tools':>5} {'cost':>8}")
    for a in agents:
        cost = f"${a.cost:.4f}" if a.cost is not None else "-"
        status = color(f"{a.status:<8}", STATUS_COLOR.get(a.status), use_color)
        print(f"{a.idx:>3}  {fit(a.name, name_w)}  {status} {mmss(a.elapsed()):>5} {a.tools:>5} {cost:>8}")
    total = sum(a.cost or 0 for a in agents)
    print(f"\ntotal ${total:.4f} · logs: {run_dir}/ (results.md, run.json, *.jsonl)")


def latest_run(log_dir: Path) -> Path:
    runs = sorted(p for p in log_dir.glob("*") if (p / "run.json").is_file())
    if not runs:
        sys.exit(f"agent_wall: no previous runs in {log_dir}/")
    return runs[-1]


def show_last(args) -> int:
    run_dir = Path(args.run) if args.run else latest_run(Path(args.log_dir))
    meta = json.loads((run_dir / "run.json").read_text(encoding="utf-8"))
    agents = []
    for m in meta["agents"]:
        a = Agent(idx=m["idx"], name=m["name"], prompt=m["prompt"])
        a.log_path = run_dir / m["log"] if m.get("log") else None
        if a.log_path and a.log_path.is_file():
            for line in a.log_path.read_text(encoding="utf-8").splitlines():
                try:
                    ev = json.loads(line)
                except json.JSONDecodeError:
                    ev = None
                if isinstance(ev, dict):
                    handle_event(a, ev)
                elif line.strip():
                    a.add(line)
        a.status, a.tools, a.cost = m["status"], m["tools"], m["cost"]
        a.started, a.ended = 1.0, 1.0 + m["elapsed"]
        agents.append(a)
    use_color = sys.stdout.isatty() and not args.no_color
    size = shutil.get_terminal_size((160, 48))
    clock = max((a.elapsed() for a in agents), default=0)
    lines = render(agents, size.columns, size.lines - 1, args.cols, clock, use_color,
                   label=f"LAST RUN {run_dir.name}")
    print("\n".join(l + ("\x1b[0m" if use_color else "") for l in lines))
    print_summary(agents, run_dir, use_color)
    return 0


def parse_args(argv=None):
    p = argparse.ArgumentParser(description="Run Claude Code agents in parallel on a live status wall.")
    p.add_argument("prompts", nargs="*", help="agent prompts (one agent each)")
    p.add_argument("-t", "--tasks", help="JSON file: list of prompts or {name, prompt, cwd}")
    p.add_argument("-n", "--agents", type=int, default=10, help="agent count for --demo or a single prompt (default 10)")
    p.add_argument("-p", "--parallel", type=int, default=0, help="max agents running at once (default: all)")
    p.add_argument("-m", "--model", default=os.environ.get("AGENT_WALL_MODEL", "opus"),
                   help="model alias or ID passed to claude (default: $AGENT_WALL_MODEL or 'opus')")
    p.add_argument("--permission-mode", default=os.environ.get("AGENT_WALL_PERMISSION_MODE", "acceptEdits"),
                   help="passed to claude (default: $AGENT_WALL_PERMISSION_MODE or acceptEdits; '' to omit)")
    p.add_argument("--claude-args", default="", help="extra args for claude, e.g. \"--allowedTools 'Read Grep'\"")
    p.add_argument("--claude-bin", default=os.environ.get("CLAUDE_BIN", "claude"))
    p.add_argument("--cwd", help="working directory for all agents")
    p.add_argument("--cols", type=int, default=0, help="grid columns (default: fit to terminal)")
    p.add_argument("--refresh", type=float, default=0.25, help="redraw interval in seconds")
    p.add_argument("--log-dir", default="runs", help="where run logs go (default: runs/)")
    p.add_argument("--demo", action="store_true", help="simulate agents instead of calling claude")
    p.add_argument("--last", action="store_true", help="show the wall and results of the last run")
    p.add_argument("--run", help="with --last: a specific run directory")
    p.add_argument("--plain", action="store_true", help="no live grid; print status changes only")
    p.add_argument("--no-color", action="store_true")
    return p.parse_args(argv)


def _raise_interrupt(*_):
    raise KeyboardInterrupt


def main(argv=None) -> int:
    args = parse_args(argv)
    signal.signal(signal.SIGTERM, _raise_interrupt)
    if args.last:
        return show_last(args)

    agents = load_agents(args)
    run_dir = Path(args.log_dir) / time.strftime("%Y%m%d-%H%M%S")
    run_dir.mkdir(parents=True, exist_ok=True)
    for a in agents:
        a.log_path = run_dir / f"{a.idx:02d}-{slug(a.name)}.jsonl"

    stop = threading.Event()
    runner = run_demo if args.demo else run_claude
    q = queue.Queue()
    for a in agents:
        q.put(a)

    def worker():
        while not stop.is_set():
            try:
                a = q.get_nowait()
            except queue.Empty:
                return
            runner(a, args, stop)

    n_workers = min(args.parallel or len(agents), len(agents))
    threads = [threading.Thread(target=worker, daemon=True) for _ in range(n_workers)]
    run_start = time.time()
    for t in threads:
        t.start()

    live = sys.stdout.isatty() and not args.plain
    use_color = sys.stdout.isatty() and not args.no_color
    seen = {}
    screen = Screen() if live else None
    try:
        if screen:
            screen.__enter__()
        while True:
            alive = any(t.is_alive() for t in threads)
            if screen:
                size = shutil.get_terminal_size()
                screen.draw(render(agents, size.columns, size.lines, args.cols, time.time() - run_start, use_color))
            else:
                for a in agents:
                    if seen.get(a.idx) != a.status:
                        seen[a.idx] = a.status
                        print(f"[{mmss(time.time() - run_start)}] {a.idx}. {a.name}: {a.status}", flush=True)
            if not alive:
                break
            time.sleep(args.refresh)
        if screen:
            time.sleep(1.0)
    except KeyboardInterrupt:
        stop.set()
        stop_all(agents)
        for t in threads:
            t.join(timeout=5)
        for a in agents:
            if a.status == "running":
                a.status, a.ended = "stopped", time.time()
    finally:
        if screen:
            screen.__exit__(None, None, None)

    save_run(agents, run_dir, run_start)
    print_summary(agents, run_dir, use_color)
    return 0 if all(a.status == "done" for a in agents) else 1


if __name__ == "__main__":
    sys.exit(main())
