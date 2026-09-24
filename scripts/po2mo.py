#!/usr/bin/env python3
"""Deterministic .po -> .mo compiler (same output format as CPython's msgfmt.py).

Usage: python po2mo.py input.po output.mo
Fuzzy entries and entries without translation are skipped, like msgfmt.
"""
import ast
import struct
import sys


def parse_po(path):
    messages = {}
    ctx = msgid = msgstr = None
    section = None
    fuzzy = False

    def flush():
        nonlocal ctx, msgid, msgstr, fuzzy
        if msgid is not None and msgstr is not None and not fuzzy and (msgstr or msgid == ""):
            key = msgid if ctx is None else ctx + "\x04" + msgid
            messages[key] = msgstr
        ctx = msgid = msgstr = None
        fuzzy = False

    with open(path, encoding="utf-8-sig") as handle:
        for raw in handle:
            line = raw.strip()
            if line.startswith("#,") and "fuzzy" in line:
                if msgstr is not None:
                    flush()
                fuzzy = True
                continue
            if not line or line.startswith("#"):
                if msgstr is not None and not line:
                    flush()
                continue
            if line.startswith("msgctxt "):
                if msgstr is not None:
                    flush()
                section, ctx = "ctx", ast.literal_eval(line[8:])
            elif line.startswith("msgid "):
                if msgstr is not None:
                    flush()
                section, msgid = "id", ast.literal_eval(line[6:])
            elif line.startswith("msgid_plural") or line.startswith("msgstr["):
                raise SystemExit("plural forms are not supported by this helper")
            elif line.startswith("msgstr "):
                section, msgstr = "str", ast.literal_eval(line[7:])
            elif line.startswith('"'):
                value = ast.literal_eval(line)
                if section == "ctx":
                    ctx += value
                elif section == "id":
                    msgid += value
                elif section == "str":
                    msgstr += value
        flush()
    return messages


def write_mo(messages, path):
    keys = sorted(messages)
    ids = b""
    strs = b""
    offsets = []
    for key in keys:
        k = key.encode("utf-8")
        v = messages[key].encode("utf-8")
        offsets.append((len(ids), len(k), len(strs), len(v)))
        ids += k + b"\0"
        strs += v + b"\0"
    keystart = 7 * 4 + 16 * len(keys)
    valuestart = keystart + len(ids)
    koffsets, voffsets = [], []
    for o1, l1, o2, l2 in offsets:
        koffsets += [l1, o1 + keystart]
        voffsets += [l2, o2 + valuestart]
    output = struct.pack("Iiiiiii", 0x950412DE, 0, len(keys), 7 * 4, 7 * 4 + len(keys) * 8, 0, 0)
    output += struct.pack("%di" % len(koffsets), *koffsets)
    output += struct.pack("%di" % len(voffsets), *voffsets)
    output += ids + strs
    with open(path, "wb") as handle:
        handle.write(output)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    write_mo(parse_po(sys.argv[1]), sys.argv[2])
