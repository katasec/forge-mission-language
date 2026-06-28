#!/usr/bin/env python3
import json, os, sys

base = os.path.expanduser("~/progs/mission-control-language")
memory = os.path.expanduser("~/.claude/projects/-Users-ameerdeen-progs-mission-control-language/memory/MEMORY.md")

files = [
    os.path.join(base, "docs/plan.md"),
    memory,
]

parts = []
for f in files:
    try:
        with open(f) as fh:
            parts.append(f"=== {os.path.basename(f)} ===\n{fh.read()}")
    except OSError:
        pass

print(json.dumps({"context": "\n\n".join(parts)}))
