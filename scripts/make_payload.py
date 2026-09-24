#!/usr/bin/env python3
"""Builds the installer payload deterministically.

Usage:
  python make_payload.py <Cura-5.13.0.zip> <CuraEngine.exe> <out payload.zip>

* Takes fdmprinter.def.json, tr_TR/fdmprinter.def.json.po and expert.cfg from the
  official UltiMaker Cura 5.13.0 source archive and checks them against the
  SHA-256 of the files installed by Cura 5.13.0.
* Applies patches/Cura-5.13.0.patch and compiles the .mo file (scripts/po2mo.py).
* Writes a ZIP with fixed timestamps and a fixed entry order.
"""
import hashlib
import sys
import tempfile
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import apply_patch  # noqa: E402
import po2mo  # noqa: E402

ORIGINALS = {
    "resources/definitions/fdmprinter.def.json": "8fbbf8b779e806bd8d0b0e2b8b9be17bdd26a575b7ea1875c20481eb2c7e11ee",
    "resources/i18n/tr_TR/fdmprinter.def.json.po": "ace33455f77ad50ed5d10dbd2f1c2b5732d2c59dcc5e99a9e715ad1bd3f51a85",
    "resources/setting_visibility/expert.cfg": "9b646941fa24799eda46ce207f586ab72687d7b02f837264287bb022b720a4ba",
}
ORDER = [
    ("CuraEngine.exe", None),
    ("share/cura/resources/definitions/fdmprinter.def.json", "resources/definitions/fdmprinter.def.json"),
    ("share/cura/resources/i18n/tr_TR/fdmprinter.def.json.po", "resources/i18n/tr_TR/fdmprinter.def.json.po"),
    ("share/cura/resources/i18n/tr_TR/LC_MESSAGES/fdmprinter.def.json.mo", None),
    ("share/cura/resources/setting_visibility/expert.cfg", "resources/setting_visibility/expert.cfg"),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    cura_zip, engine, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        with zipfile.ZipFile(cura_zip) as archive:
            names = archive.namelist()
            prefix = names[0].split("/")[0] + "/"
            for rel, expected in ORIGINALS.items():
                data = archive.read(prefix + rel)
                if sha(data) != expected:
                    raise SystemExit("unexpected upstream file (not Cura 5.13.0?): " + rel)
                target = tmp / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
        sys.argv = ["apply_patch.py", str(HERE.parent / "patches" / "Cura-5.13.0.patch"), str(tmp)]
        apply_patch.main()
        mo = tmp / "resources/i18n/tr_TR/LC_MESSAGES/fdmprinter.def.json.mo"
        mo.parent.mkdir(parents=True, exist_ok=True)
        po2mo.write_mo(po2mo.parse_po(tmp / "resources/i18n/tr_TR/fdmprinter.def.json.po"), mo)

        out.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as payload:
            for name, source in ORDER:
                if name == "CuraEngine.exe":
                    data = engine.read_bytes()
                elif name.endswith(".mo"):
                    data = mo.read_bytes()
                else:
                    data = (tmp / source).read_bytes()
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                info.create_system = 0
                payload.writestr(info, data)
                print(sha(data), name.replace("/", "\\"))


if __name__ == "__main__":
    main()
