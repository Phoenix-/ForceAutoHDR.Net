# The AOT link step needs `vswhere.exe` on PATH, and says so badly

Hit 2026-09-08 on a machine where `dotnet build` and `dotnet test` were perfectly happy.

## The symptom

`dotnet publish src/ForceAutoHDR.App -c Release` gets all the way to native codegen and then:

```
"vswhere.exe" is not recognized as an internal or external command
C:\Users\...\microsoft.dotnet.ilcompiler\10.0.11\build\Microsoft.NETCore.Native.targets(396,5):
error MSB3073: ... link.exe @"obj\...\native\link.rsp"" exited with code 123
```

Two things make this worse than it needs to be:

- **The message names `link.exe`, not the actual culprit.** The failing command line has a full,
  correct path to `link.exe` in it, so the obvious reading is "the linker is broken". It is not:
  ILCompiler shells out to `vswhere` *first*, to locate the MSVC toolchain, and invokes it by bare
  name. The quoted `link.exe` path is just the rest of a command that never ran.
- **Piping the publish through anything hides it.** `dotnet publish ... | tail` reports exit code
  0, because that is `tail`'s exit code. The failure is only visible in the text.

## The fix

`vswhere.exe` ships at a fixed location and is not added to PATH by the Visual Studio installer:

```
C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe
```

Either publish from a Developer PowerShell (which sets PATH up), or prepend that directory:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish src/ForceAutoHDR.App -c Release
```

Nothing in the repo needs changing -- this is machine setup, not project configuration. It is
recorded because the error text sends the diagnosis at the linker, and because it appeared on a
machine where the AOT gate had already passed once (2026-09-07): a Visual Studio update moved the
toolchain to VS 18 / MSVC 14.51 and the earlier publish had evidently run from a developer shell.
