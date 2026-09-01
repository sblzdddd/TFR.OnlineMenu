using Il2Cpp;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TFROnlineMenu;

public sealed partial class OnlineMenuMod
{
    private bool _bindingLocalInput;

    private bool EnsureLocalGamePlayer()
    {
        var players = GameManager.players;
        if (players is null || players.Length == 0)
        {
            _message = "GameManager has no local player slot.";
            return false;
        }

        return players[0]?.input || EnsureLocalPlayerInput();
    }

    private bool EnsureLocalPlayerInput()
    {
        var manager = RacingInputManager.instance;
        if (!manager || !manager.manager)
        {
            _message = "RacingInputManager is not available.";
            return false;
        }

        InputDevice? device = null;
        string? controlScheme = null;
        var staleInputs = new List<PlayerInput>();
        var inputs = PlayerInput.all;

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (!input)
            {
                continue;
            }

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
            _message = "No keyboard or gamepad is available.";
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
                0,
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
            _message = "PlayerInput was not created.";
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
        if (_bindingLocalInput && input.playerIndex == 0 && !IsLocalPlayerInputBound(input))
        {
            BindLocalPlayerInput(manager, input);
        }
    }

    private static bool IsLocalPlayerInputBound(PlayerInput input)
    {
        var localInput = GameManager.players[0]?.input;
        return localInput && localInput == input;
    }

    private void BindLocalPlayerInput(RacingInputManager manager, PlayerInput input)
    {
        var localPlayer = GameManager.players[0] ?? GameManager.AddHuman(0);
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

        manager._humanInputIndex[0] = 0;
        manager._humanCount = Math.Max(manager._humanCount, 1);
        LoggerInstance.Msg($"Bound local PlayerInput: {input.currentControlScheme}.");
    }

    private static PlayerInput? FindJoinedPlayerInput()
    {
        var inputs = PlayerInput.all;
        for (var index = 0; index < inputs.Count; index++)
        {
            if (inputs[index] && inputs[index].playerIndex == 0)
            {
                return inputs[index];
            }
        }

        return null;
    }

    internal void OnLocalPlayerPossessed(HumanGamePlayer player, PlayerRacer racer, int humanIndex)
    {
        if (humanIndex != 0 || !Il2CppMirror.NetworkClient.active)
        {
            return;
        }

        var input = player.input;
        input.enabled = true;
        input.ActivateInput();
        input.actions.Enable();

        var humanInput = input.GetComponent<HumanPlayerInput>();
        humanInput.Assign(player);
        humanInput.enabled = true;

        var controller = input.GetComponent<PlayerRacingController>();
        controller.enabled = true;
        if (PlayerInput.GetPlayerByIndex(humanIndex) != input)
        {
            controller.Init(player, racer);
        }

        _message = "Local driving input is ready.";
    }
}
