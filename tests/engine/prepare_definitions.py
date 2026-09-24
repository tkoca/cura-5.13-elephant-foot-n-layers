#!/usr/bin/env python3
"""Prepares definition files for command-line slicing tests.

Usage: python prepare_definitions.py <fdmprinter.def.json> <fdmextruder.def.json> <out dir>

When CuraEngine is driven from the command line (without the Cura front end) it cannot
evaluate "limit_to_extruder" expressions, and settings limited to an extruder such as
flooring_layer_count cannot be resolved. The tests use a single extruder, so these
limits are removed from the copies used for testing. Both engines under test read the
same files.
"""
import json
import sys
from pathlib import Path


def strip(node):
    if isinstance(node, dict):
        node.pop("limit_to_extruder", None)
        for value in node.values():
            strip(value)


def main():
    printer, extruder, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    out.mkdir(parents=True, exist_ok=True)
    for source in (printer, extruder):
        data = json.loads(source.read_text(encoding="utf-8"))
        strip(data)
        (out / source.name).write_text(json.dumps(data, indent=1, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
