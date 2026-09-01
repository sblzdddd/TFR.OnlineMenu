"""Print Hex-Rays pseudocode for an Assembly-CSharp method in GameAssembly.dll.i64."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from ida_domain import Database
    from ida_domain.database import IdaCommandOptions
    from ida_domain.pseudocode import PseudocodeError
except ImportError as e:
    print(f"cannot load ida_domain: {e}")
    sys.exit(1)

ROOT = Path(__file__).resolve().parent
DEFAULT_DB = ROOT.parent / "TFR_OL" / "GameAssembly.dll.i64"

# Game (Assembly-CSharp) symbols already present in the IDB. Engine/runtime prefixes
# are skipped when scanning so we do not dump Unity/mscorlib by accident.
DEFAULT_FUNCTION = "FRNetworkManager$$OnStartServer"
NAME_VARIANTS = (
    "{cls}$${method}",
    "{cls}.{method}",
    "{cls}::{method}",
    "{cls}_{method}",
    "{cls}__{method}",
)
ENGINE_PREFIXES = (
    "UnityEngine",
    "Unity.",
    "System_",
    "System.",
    "Mono.",
    "il2cpp_",
    "mscorlib",
    "Newtonsoft",
)


def parse_class_method(query: str) -> tuple[str, str] | None:
    for sep in ("$$", "::", "."):
        if sep in query:
            left, right = query.split(sep, 1)
            if left and right:
                return left, right
    if "_" in query:
        left, right = query.split("_", 1)
        if left and right:
            return left, right
    return None


def candidate_names(query: str) -> list[str]:
    names = [query]
    parsed = parse_class_method(query)
    if parsed:
        cls, method = parsed
        names.extend(pattern.format(cls=cls, method=method) for pattern in NAME_VARIANTS)
    seen: set[str] = set()
    unique: list[str] = []
    for name in names:
        if name not in seen:
            seen.add(name)
            unique.append(name)
    return unique


def is_engine_symbol(name: str) -> bool:
    return name.startswith(ENGINE_PREFIXES)


def is_game_method(name: str) -> bool:
    if is_engine_symbol(name):
        return False
    return "$$" in name or "." in name


def find_by_exact_names(db: Database, names: list[str]):
    for name in names:
        func = db.functions.get_by_name(name)
        if func is not None:
            return func, db.functions.get_name(func) or name
    return None, None


def find_by_substring(db: Database, query: str, limit: int = 20) -> list[tuple[object, str]]:
    needle = query.lower()
    matches: list[tuple[object, str]] = []
    for ea, name in db.names.get_all():
        if needle not in name.lower() or is_engine_symbol(name):
            continue
        func = db.functions.get_at(ea)
        if func is None or func.start_ea != ea:
            continue
        matches.append((func, name))
        if len(matches) >= limit:
            break
    return matches


def print_pseudocode(db: Database, func, name: str) -> None:
    signature = db.functions.get_signature(func)
    print(f"function: {name}")
    print(f"address:  0x{func.start_ea:X} - 0x{func.end_ea:X}")
    if signature:
        print(f"type:     {signature}")
    print()
    try:
        print("\n".join(db.functions.get_pseudocode(func).to_text()))
    except PseudocodeError as e:
        print(f"decompilation failed: {e}")
        sys.exit(1)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Decompile an Assembly-CSharp function from GameAssembly.dll.i64"
    )
    parser.add_argument(
        "name",
        nargs="?",
        default=DEFAULT_FUNCTION,
        help=f"function name or substring (default: {DEFAULT_FUNCTION})",
    )
    parser.add_argument(
        "-f",
        "--database",
        default=str(DEFAULT_DB),
        help="path to GameAssembly.dll.i64",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="list matching game methods instead of decompiling",
    )
    args = parser.parse_args()

    db_path = Path(args.database)
    if not db_path.exists():
        print(f"database not found: {db_path}")
        return 1

    print(f"opening {db_path} ...", flush=True)
    opts = IdaCommandOptions(auto_analysis=False, new_database=False)
    with Database.open(str(db_path), opts, save_on_close=False) as db:
        if args.list:
            matches = find_by_substring(db, args.name)
            if not matches:
                print(f"no Assembly-CSharp-like methods matching {args.name!r}")
                return 1
            for func, name in matches:
                print(f"0x{func.start_ea:X}  {name}")
            return 0

        queries = candidate_names(args.name)
        parsed = parse_class_method(args.name)
        if parsed:
            queries.append(f"{parsed[0]}$$")
        func, resolved = find_by_exact_names(db, queries)
        if func is None:
            matches: list[tuple[object, str]] = []
            seen: set[str] = set()
            for query in queries:
                for item in find_by_substring(db, query):
                    if item[1] not in seen:
                        seen.add(item[1])
                        matches.append(item)
            if parsed:
                method = parsed[1].lower()
                method_hits = [(f, n) for f, n in matches if method in n.lower()]
                if method_hits:
                    matches = method_hits
            game_matches = [(f, n) for f, n in matches if is_game_method(n)] or matches
            if not game_matches:
                print(f"function not found: {args.name}")
                return 1
            if len(game_matches) > 1:
                print(f"multiple matches for {args.name!r}; using the first:\n")
                for _, name in game_matches:
                    print(f"  {name}")
                print()
            func, resolved = game_matches[0]

        print_pseudocode(db, func, resolved)
    return 0


if __name__ == "__main__":
    sys.exit(main())
