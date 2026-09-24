#!/usr/bin/env python3
"""Strict unified-diff applier (no git/patch needed).

Usage: python apply_patch.py <patch-file> <source-root> [--reverse] [--check]

Every hunk must match exactly (context and removed lines); otherwise nothing is
written. Line endings of each target file are preserved.
"""
import re
import sys
from pathlib import Path

HUNK = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def parse(text):
    files, current = [], None
    lines = text.split("\n")
    i = 0
    while i < len(lines):
        line = lines[i]
        i += 1
        if line.startswith("+++ "):
            path = line[4:].split("\t")[0]
            current = {"path": path[2:] if path.startswith("b/") else path, "hunks": []}
            files.append(current)
        elif line.startswith("@@"):
            m = HUNK.match(line)
            if not m or current is None:
                raise SystemExit("invalid hunk header: " + line)
            old_left = int(m.group(2) or "1")
            new_left = int(m.group(4) or "1")
            hunk = {"old_start": int(m.group(1)), "lines": []}
            while old_left > 0 or new_left > 0:
                if i >= len(lines):
                    raise SystemExit("truncated hunk")
                body = lines[i]
                i += 1
                if body.startswith("\\"):
                    continue
                if body == "":
                    body = " "  # blank context line whose trailing space was stripped
                kind = body[0]
                if kind == " ":
                    old_left -= 1
                    new_left -= 1
                elif kind == "-":
                    old_left -= 1
                elif kind == "+":
                    new_left -= 1
                else:
                    raise SystemExit("invalid hunk line: " + body)
                hunk["lines"].append(body)
            current["hunks"].append(hunk)
    return files


def apply(lines, hunks, reverse):
    out, pos = [], 0
    for h in hunks:
        old = [l[1:] for l in h["lines"] if l[0] in (" ", "+" if reverse else "-")]
        new = [l[1:] for l in h["lines"] if l[0] in (" ", "-" if reverse else "+")]
        start = h["old_start"] - 1
        candidates = [start] + [start + d for r in range(1, 200) for d in (r, -r)]
        for c in candidates:
            if c >= pos and lines[c:c + len(old)] == old:
                out.extend(lines[pos:c])
                out.extend(new)
                pos = c + len(old)
                break
        else:
            raise SystemExit("hunk does not apply near line %d" % h["old_start"])
    out.extend(lines[pos:])
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    reverse, check = "--reverse" in sys.argv, "--check" in sys.argv
    if len(args) != 2:
        raise SystemExit(__doc__)
    patch = Path(args[0]).read_text(encoding="utf-8").replace("\r\n", "\n")
    root = Path(args[1])
    results = []
    for f in parse(patch):
        target = root / f["path"]
        raw = target.read_bytes().decode("utf-8")
        crlf = "\r\n" in raw
        trailing = raw.endswith("\n")
        lines = raw.replace("\r\n", "\n").split("\n")
        if trailing:
            lines.pop()
        new = apply(lines, f["hunks"], reverse)
        text = "\n".join(new) + ("\n" if trailing else "")
        results.append((target, text.replace("\n", "\r\n") if crlf else text))
    if not check:
        for target, text in results:
            target.write_bytes(text.encode("utf-8"))
    for target, _ in results:
        print(("ok (check) " if check else "patched ") + str(target))


if __name__ == "__main__":
    main()
