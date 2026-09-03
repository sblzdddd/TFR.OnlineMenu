# When client exits the room and rejoins, an error is thrown in client side:

```
[16:28:56.102] [ERROR] [Il2CppInterop] During invoking native->managed trampoline
Il2CppInterop.Runtime.Il2CppException: System.NullReferenceException: Object reference not set to an instance of an object.
--- BEGIN IL2CPP STACK TRACE ---
System.NullReferenceException: Object reference not set to an instance of an object.
  at CharacterSelectionBehaviour.Loaded (System.Int32 maxHumans) [0x00000] in <00000000000000000000000000000000>:0
  at MainMenuManager.OnSceneLoaded () [0x00000] in <00000000000000000000000000000000>:0
--- END IL2CPP STACK TRACE ---

   at Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(IntPtr returnedException) in /home/runner/work/Il2CppInterop/Il2CppInterop/Il2CppInterop.Runtime/Il2CppException.cs:line 36
   at DMD<Il2Cpp.MainMenuManager::OnSceneLoaded>(MainMenuManager this)
   at (il2cpp -> managed) OnSceneLoaded(IntPtr , Il2CppMethodInfo* )
```

no further effects observed

(Fixed) only self player selection box is seen in character selection grid

# client side device type label showing DEBUGME on selection scene load (shouldnt be shown)

# client side cannot exit device selection and back to character selection / exit the room

# player label is still CPU instead of nickname

# player race time not displaying error

# host re-start map causing softlocks

# numpad enter force start causing memory / GO duplication

# quitting from map selection cannot re-select / re-enter map selection / selection scene glitch (missing re-sync)

# client joining selection scene does not remove the "disabled" frame

# backing to selection scene after race ends causing de-sync of current selected characters (host -> client)

# soft-locking in map after restarting
