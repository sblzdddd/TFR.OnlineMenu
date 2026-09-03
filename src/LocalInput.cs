using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod
{
    private const int LocalInputIndex = 0;
    private bool _bindingLocalInput;

    private bool EnsureLocalGamePlayer()
    {
        var players = GameManager.players;
        if (players is null || players.Length == 0)
        {
            Message = "GameManager has no local player slot.";
            return false;
        }

        return players[LocalInputIndex]?.input || EnsureLocalPlayerInput();
    }

    internal void EnsureSelectionInput()
    {
        var players = GameManager.players;
        var human = players is not null && players.Length > 0 ? players[LocalInputIndex] : null;
        var input = human?.input;
        if (input && input!.playerIndex == LocalInputIndex)
        {
            return;
        }

        EnsureLocalPlayerInput();
    }

    private bool EnsureLocalPlayerInput()
    {
        var manager = RacingInputManager.instance;
        if (!manager || !manager.manager)
        {
            Message = "RacingInputManager is not available.";
            return false;
        }

        InputDevice? device = null;
        string? controlScheme = null;
        var staleInputs = new List<PlayerInput>();
        var inputs = PlayerInput.all;

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (!input) continue;
            staleInputs.Add(input);
            if (device is null && input.devices.Count > 0)
            {
                device = input.devices[0];
                controlScheme = input.currentControlScheme;
            }
        }

        device ??= Keyboard.current;
        device ??= Gamepad.current;
        if (device is null)
        {
            Message = "No keyboard or gamepad is available.";
            return false;
        }

        foreach (var input in staleInputs)
        {
            input.DeactivateInput();
            input.enabled = false;
            UnityEngine.Object.Destroy(input.gameObject);
        }

        PlayerInput? joinedInput = null;
        _bindingLocalInput = true;
        try
        {
            joinedInput = manager.AddPlayer(
                LocalInputIndex,
                string.IsNullOrWhiteSpace(controlScheme) ? null! : controlScheme,
                device);
        }
        catch (Exception exception)
        {
            LoggerInstance.Warning($"OnPlayerJoined stopped early: {exception.Message}");
        }
        finally
        {
            _bindingLocalInput = false;
        }

        joinedInput = joinedInput ? joinedInput : FindJoinedPlayerInput();
        if (!joinedInput)
        {
            Message = "PlayerInput was not created.";
            return false;
        }

        if (!IsLocalPlayerInputBound(joinedInput!))
        {
            BindLocalPlayerInput(manager, joinedInput!);
        }

        return true;
    }

    internal void OnRacingPlayerJoined(RacingInputManager manager, PlayerInput input)
    {
        if (_bindingLocalInput && input.playerIndex == LocalInputIndex && !IsLocalPlayerInputBound(input))
        {
            BindLocalPlayerInput(manager, input);
        }
    }

    private static bool IsLocalPlayerInputBound(PlayerInput input)
    {
        var players = GameManager.players;
        if (players is null || players.Length == 0)
        {
            return false;
        }

        var localInput = players[LocalInputIndex]?.input;
        return localInput && localInput == input;
    }

    private void BindLocalPlayerInput(RacingInputManager manager, PlayerInput input)
    {
        var localPlayer = (GameManager.players is not null && GameManager.players.Length > 0
            ? GameManager.players[LocalInputIndex]
            : null) ?? GameManager.AddHuman(LocalInputIndex);
        var controls = ProfilesManager.instance.GetDefaultControls();

        if (input.actions)
        {
            input.actions.Disable();
        }

        input.actions = controls;
        controls.Enable();
        input.enabled = true;
        input.ActivateInput();
        localPlayer.SetInput(input);

        var humanInput = input.GetComponent<HumanPlayerInput>();
        humanInput.Assign(localPlayer);
        humanInput.enabled = true;
        input.transform.SetParent(manager.transform, false);

        manager._humanInputIndex[LocalInputIndex] = LocalInputIndex;
        manager._humanCount = Math.Max(manager._humanCount, 1);
        LoggerInstance.Msg($"Bound local PlayerInput: {input.currentControlScheme} (input 0).");
    }

    private static PlayerInput? FindJoinedPlayerInput()
    {
        var inputs = PlayerInput.all;
        for (var index = 0; index < inputs.Count; index++)
        {
            if (inputs[index] && inputs[index].playerIndex == LocalInputIndex)
            {
                return inputs[index];
            }
        }

        return null;
    }
}

[HarmonyPatch(typeof(RacingInputManager), "OnPlayerJoined")]
internal static class RacingInputManagerOnPlayerJoinedPatch
{
    private static void Postfix(RacingInputManager __instance, PlayerInput __0)
    {
        OnlineMenuMod.Instance?.OnRacingPlayerJoined(__instance, __0);
    }
}

/// <summary>
/// A client whose network slot is not 0 is still the machine's only local input user. Redirect menu actions
/// from local input 0 to that network slot while the online selection screen is active.
/// </summary>
internal static class LocalHumanIndex
{
    internal static void Redirect(ref int humanIndex)
    {
        if (!OnlineSelection.IsActive)
        {
            return;
        }

        var slot = OnlineSelection.LocalSlot;
        if (slot >= 0)
        {
            humanIndex = slot;
        }
    }
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Move))]
internal static class MenuEventsManagerMovePatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Submit))]
internal static class MenuEventsManagerSubmitPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Cancel))]
internal static class MenuEventsManagerCancelPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.CancelRelease))]
internal static class MenuEventsManagerCancelReleasePatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}

[HarmonyPatch(typeof(MenuEventsManager), nameof(MenuEventsManager.Config))]
internal static class MenuEventsManagerConfigPatch
{
    private static void Prefix(ref int __0) => LocalHumanIndex.Redirect(ref __0);
}
