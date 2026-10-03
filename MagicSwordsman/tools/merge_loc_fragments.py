#!/usr/bin/env python3
"""Merge content-agent localization fragments into the main localization files.

Layout (see FRAMEWORK.md, "Localization fragments"):
    loc_fragments/<group>/<kor|eng>/<table>.json      (fragment, written by a content agent)
    MagicSwordsman/localization/<kor|eng>/<table>.json (main file, only the integrator edits it)

Usage (from the MagicSwordsman project folder or anywhere):
    python3 tools/merge_loc_fragments.py            # dry run: report what would change, exit 1 on conflicts
    python3 tools/merge_loc_fragments.py --write    # apply
    python3 tools/merge_loc_fragments.py --group gram --write
    python3 tools/merge_loc_fragments.py --write --overwrite   # fragment value wins over an existing main value

Rules:
  * New keys are appended to the main file (existing key order is kept).
  * A key that already exists with the SAME value is skipped (re-running is safe).
  * A key that exists with a DIFFERENT value is a conflict (error) unless --overwrite.
  * Two fragments defining the same key with different values is always an error.
  * kor/eng of one group should have the same keys: differences are reported as warnings.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import OrderedDict
from pathlib import Path

LANGS = ("kor", "eng")
RECOMMENDED_TABLES = {"cards", "powers", "relics", "events", "potions", "card_keywords"}

ROOT = Path(__file__).resolve().parent.parent
FRAGMENTS = ROOT / "loc_fragments"
MAIN = ROOT / "MagicSwordsman" / "localization"


def load(path: Path) -> "OrderedDict[str, str]":
    text = path.read_text(encoding="utf-8-sig")
    if not text.strip():
        return OrderedDict()
    data = json.loads(text, object_pairs_hook=OrderedDict)
    if not isinstance(data, dict):
        raise ValueError(f"{path}: top level must be a JSON object")
    return data


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write", action="store_true", help="write the merged main files (default: dry run)")
    ap.add_argument("--overwrite", action="store_true", help="fragment values replace different main values")
    ap.add_argument("--group", action="append", help="only merge these group folder(s)")
    args = ap.parse_args()

    if not FRAGMENTS.is_dir():
        print(f"no fragment folder: {FRAGMENTS}")
        return 0

    errors: list[str] = []
    warnings: list[str] = []
    # (lang, table) -> key -> (value, source)
    incoming: dict[tuple[str, str], "OrderedDict[str, tuple[str, str]]"] = {}
    group_keys: dict[str, dict[str, set[str]]] = {}

    for group_dir in sorted(p for p in FRAGMENTS.iterdir() if p.is_dir()):
        group = group_dir.name
        if args.group and group not in args.group:
            continue
        for lang_dir in sorted(p for p in group_dir.iterdir() if p.is_dir()):
            lang = lang_dir.name
            if lang not in LANGS:
                warnings.append(f"{lang_dir}: unknown language folder (expected kor/eng), ignored")
                continue
            for frag in sorted(lang_dir.glob("*.json")):
                table = frag.stem
                if table not in RECOMMENDED_TABLES:
                    warnings.append(f"{frag}: table '{table}' is outside the usual set {sorted(RECOMMENDED_TABLES)}")
                try:
                    data = load(frag)
                except Exception as e:  # noqa: BLE001
                    errors.append(f"{frag}: invalid JSON: {e}")
                    continue
                bucket = incoming.setdefault((lang, table), OrderedDict())
                for key, value in data.items():
                    if not isinstance(value, str):
                        errors.append(f"{frag}: value of '{key}' must be a string")
                        continue
                    if key in bucket and bucket[key][0] != value:
                        errors.append(f"key '{key}' ({lang}/{table}) defined differently in {bucket[key][1]} and {frag}")
                        continue
                    bucket[key] = (value, str(frag.relative_to(ROOT)))
                    group_keys.setdefault(group, {}).setdefault(lang, set()).add(f"{table}:{key}")

    for group, langs in group_keys.items():
        kor, eng = langs.get("kor", set()), langs.get("eng", set())
        for k in sorted(kor - eng):
            warnings.append(f"[{group}] only in kor: {k}")
        for k in sorted(eng - kor):
            warnings.append(f"[{group}] only in eng: {k}")

    changes: dict[Path, "OrderedDict[str, str]"] = {}
    added = replaced = skipped = 0
    for (lang, table), entries in sorted(incoming.items()):
        main_path = MAIN / lang / f"{table}.json"
        main = load(main_path) if main_path.exists() else OrderedDict()
        if not main_path.exists():
            warnings.append(f"{main_path.relative_to(ROOT)} does not exist yet; it will be created")
        changed = False
        for key, (value, src) in entries.items():
            if key not in main:
                main[key] = value
                added += 1
                changed = True
            elif main[key] == value:
                skipped += 1
            elif args.overwrite:
                main[key] = value
                replaced += 1
                changed = True
            else:
                errors.append(f"conflict {lang}/{table} '{key}': main has a different value (from {src}); "
                              f"use --overwrite to replace")
        if changed:
            changes[main_path] = main

    for w in warnings:
        print(f"warning: {w}")
    for e in errors:
        print(f"ERROR: {e}")
    print(f"{'merged' if args.write else 'would merge'}: {added} new, {replaced} replaced, {skipped} already present, "
          f"{len(changes)} file(s)")

    if errors:
        print("nothing written (fix the errors first)" if args.write else "")
        return 1
    if args.write:
        for path, data in changes.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            print(f"wrote {path.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
