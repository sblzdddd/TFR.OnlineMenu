using UnityEngine.SceneManagement;

namespace TFROnlineMenu.Select;

internal static class SelectionScenes
{
    internal const string Selection = "selection";

    internal static bool IsSelection(string scene) =>
        scene.Equals(Selection, StringComparison.OrdinalIgnoreCase);

    internal static bool IsMainMenu(string scene) =>
        scene.Equals("menu2", StringComparison.OrdinalIgnoreCase) ||
        scene.Equals("mainscene", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLoading(string scene) =>
        scene.Equals("loading", StringComparison.OrdinalIgnoreCase) ||
        Il2Cpp.LevelManager.instance && Il2Cpp.LevelManager.instance.isLoading;

    internal static bool IsTransition(string scene) =>
        scene.Equals("loading", StringComparison.OrdinalIgnoreCase) ||
        scene.Equals("splash", StringComparison.OrdinalIgnoreCase) ||
        scene.Equals("render", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLobby(string scene) =>
        IsMainMenu(scene) || IsTransition(scene) || IsSelection(scene);
}
