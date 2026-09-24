#!/usr/bin/env python3
"""Prints the X/Y extent of the outer wall (WALL-OUTER) for the first layers of a G-code file.

Usage: python analyze_gcode.py file.gcode [layers]
Output: one line per layer: "<layer> <width_x> <width_y>" in millimetres.
"""
import re
import sys

path = sys.argv[1]
count = int(sys.argv[2]) if len(sys.argv) > 2 else 8
layer = None
kind = None
x = y = None
extents = {}
move = re.compile(r"^G[01]\b")
for line in open(path, encoding="utf-8", errors="replace"):
    if line.startswith(";LAYER:"):
        layer = int(line[7:])
        if layer >= count:
            break
        continue
    if line.startswith(";TYPE:"):
        kind = line[6:].strip()
        continue
    if not move.match(line):
        continue
    parts = dict((p[0], p[1:]) for p in line.split(";")[0].split()[1:] if p)
    nx = float(parts["X"]) if "X" in parts else x
    ny = float(parts["Y"]) if "Y" in parts else y
    if layer is not None and kind == "WALL-OUTER" and "E" in parts and nx is not None:
        e = extents.setdefault(layer, [nx, nx, ny, ny])
        for px, py in ((x, y), (nx, ny)):
            if px is None:
                continue
            e[0] = min(e[0], px); e[1] = max(e[1], px); e[2] = min(e[2], py); e[3] = max(e[3], py)
    x, y = nx, ny
for layer in sorted(extents):
    e = extents[layer]
    print("%d %.3f %.3f" % (layer, e[1] - e[0], e[3] - e[2]))
