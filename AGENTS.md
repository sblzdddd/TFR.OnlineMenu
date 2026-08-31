# TFR Online Menu — agent notes

MelonLoader IL2CPP mod for Touhou Fumo Racing. Game logic lives in native `GameAssembly.dll`; managed wrappers are under `MelonLoader/Il2CppAssemblies`. Types and symbols are **already applied** in the IDA database — do not re-dump, re-analyze, or recreate the IDB unless the user asks.

## Layout

| Path | Role |
| --- | --- |
| `../TFR_OL/GameAssembly.dll.i64` | Analyzed IDA 9.2 database (structs + names already loaded) |
| `../TFR_OL/modded test/GameAssembly.dll` | IL2CPP native binary |
| `../TFR_OL/modded test/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll` | Il2CppInterop wrappers for **game** C# (`Il2Cpp.*`) |
| `../TFR_OL/modded test/Mods/` | MelonLoader mod output (`TFR.OnlineMenu.dll`) |
| `src/OnlineMenuMod.cs` | Melon lifecycle and menu UI |
| `src/NetworkSession.cs` | Network session setup and teardown |
| `src/LocalInput.cs` | Local input binding and possession |
| `src/RaceSession.cs` | QuickRace setup, start, and cleanup |
| `src/InputPatches.cs` | HarmonyX input lifecycle patches |
| `main.py` | Headless Hex-Rays dump via [IDA Domain](https://ida-domain.docs.hex-rays.com/llms.txt) |

## IDA Domain (library mode)

Use `ida-domain` 0.5.1+ against IDA Pro 9.1+. Always open the **existing** `.i64` — never pass `new_database=True`, and never leave auto-analysis on (that would re-analyze ~600MB).

```python
from pathlib import Path
from ida_domain import Database
from ida_domain.database import IdaCommandOptions

db_path = Path(__file__).resolve().parent.parent / "TFR_OL" / "GameAssembly.dll.i64"
opts = IdaCommandOptions(auto_analysis=False, new_database=False)
with Database.open(str(db_path), opts, save_on_close=False) as db:
    func = db.functions.get_by_name("FRNetworkManager$$OnStartServer")
    print("\n".join(db.functions.get_pseudocode(func).to_text()))
```

`Database()` + instance `.open()` is wrong: `open` is a classmethod and returns a **new** handle. Prefer `Database.open(...)` as a context manager ([getting started](https://ida-domain.docs.hex-rays.com/getting_started/index.md), [Database](https://ida-domain.docs.hex-rays.com/ref/database/index.md)).

Keep `save_on_close=False` unless the user asked to persist comments, names, or types.

### Lookup cheatsheet

| Need | API |
| --- | --- |
| Function by dumped name | `db.functions.get_by_name(name)` |
| Function containing EA | `db.functions.get_at(ea)` |
| Hex-Rays text | `db.functions.get_pseudocode(func).to_text()` or `str(...)` |
| Prototype | `db.functions.get_signature(func)` |
| Callees / callers | `db.functions.get_callees(func)` / `get_callers` |
| Named type / struct | `db.types.get_by_name("MultiplayerSystem_o")` |
| Name at address | `db.names.get_at(ea)` |
| Xrefs | `db.xrefs` |

Docs: [Functions](https://ida-domain.docs.hex-rays.com/ref/functions/index.md), [Pseudocode](https://ida-domain.docs.hex-rays.com/ref/pseudocode/index.md), [Types](https://ida-domain.docs.hex-rays.com/ref/types/index.md), [Names](https://ida-domain.docs.hex-rays.com/ref/names/index.md). Fallback to IDA Python (`ida_funcs`, `ida_hexrays`, `ida_name`) is allowed when Domain has no coverage.

Do **not** iterate `db.functions` / `db.names.get_all()` unless a targeted lookup failed. This IDB is huge.

## IL2CPP names and structs (already in the IDB)

Il2CppDumper-style names (try these before substring scans):

| Kind | Pattern | Example |
| --- | --- | --- |
| Method | `Class$$Method` | `FRNetworkManager$$OnStartServer` |
| Overload | `Class$$Method_XXXXXXXX` | RVA / token suffix |
| Object | `Class_o` | `MultiplayerSystem_o *this` |
| Class / vtable | `Class_c` | `this->klass` |
| Fields | `Class_Fields` | `this->fields.foo` |
| Static fields | `Class_StaticFields` + `Class_TypeInfo` | `Class_TypeInfo->static_fields` |
| Image | `Assembly-CSharp.dll` | Game code, not Unity / mscorlib |

Also try `Class.Method`, `Class::Method`, and `Class_Method` if `$$` misses.

**Assembly-CSharp** = game types used by the mod (`MultiplayerSystem`, `FRNetworkManager`, `GameMode`, `GameModeManager`, `QuickRace`, `NetworkingPlayer`, `ServerSync`, …). Skip `UnityEngine_*`, `System_*`, `Mono_*`, `il2cpp_*`, `mscorlib_*` unless the question is the runtime itself.

Managed MelonLoader `obj.Method()` maps to native `Class$$Method` when that type defines it. Inherited methods stay on the base (e.g. `StartHost` is `Mirror.NetworkManager$$StartHost`; the game override is `FRNetworkManager$$OnStartServer`). First argument is almost always `this` (`Class_o *`). Extra `MethodInfo *` appears on generics / inflated methods.

`python main.py` defaults to `FRNetworkManager$$OnStartServer`. Pass another dumped name or a class/method substring. `--list GameModeManager` lists matches without decompiling. Some types have structs/`TypeInfo` but no `$$` method names (`MultiplayerSystem` is one); use xrefs from the TypeInfo in that case.

When reading pseudocode:

- Ignore `klass` / `monitor`; real C# fields are under `fields`.
- Virtual calls look like `this->klass->vtable[N]->methodPtr`.
- `.cctor` / first use often hits `il2cpp_runtime_class_init` / `Class_TypeInfo`.
- `System_String_o *` is a C# string; `System_String$$CreateString` / `il2cpp_string_new` construct them.
- Unity objects in IL2CPP still go through Il2Cpp wrappers; `GameObject.Find` is engine code, callers in Assembly-CSharp are the interesting ones.

Use existing structs (`db.types.get_by_name`) instead of inventing field offsets. If Hex-Rays shows raw `*(Type **)(a1 + N)`, map `N` against `Class_Fields` rather than guessing.

## Analysis workflow for this mod

1. Identify the C# type/method from `OnlineMenuMod.cs` or `Il2Cpp` wrappers.
2. `python main.py Class$$Method` (or a unique substring) to print pseudocode.
3. Follow `get_callees` / xrefs only into other Assembly-CSharp methods unless the runtime helper is the bug.
4. Match field names to MelonLoader properties (`_system`, `instancia`, `currentGameMode`, …).
5. Change the **mod**, not GameAssembly. Do not patch the shipped binary.

`main.py` defaults to `FRNetworkManager$$OnStartServer` if no name is given.

## Build

`dotnet build` uses `../TFR_OL/modded test` as `GameRoot` and writes
`TFR.OnlineMenu.dll` to that installation's `Mods/` directory. Override a different
installation with `dotnet build -p:GameRoot="X:\path\to\game"`. Do not copy Unity /
Il2Cpp interop assemblies into `Mods/`.
