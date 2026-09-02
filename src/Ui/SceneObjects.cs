using UnityEngine;

namespace TFROnlineMenu.Ui;

internal static class SceneObjects
{
    internal static GameObject Find(string name)
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.name == name && candidate.scene.IsValid() && candidate.scene.isLoaded)
            {
                return candidate;
            }
        }

        return GameObject.Find(name);
    }
}
