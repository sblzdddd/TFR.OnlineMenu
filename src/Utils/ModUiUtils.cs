using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TFROnlineMenu.Utils;

internal static class ModUiUtils
{
    private static readonly Dictionary<string, TMP_FontAsset> FontCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> SpriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static string? _modVersion;
    private static MelonLogger.Instance LoggerInstance => OnlineMenuMod.Instance.LoggerInstance;

    public static string GetModVersion()
    {
        if (_modVersion is not null)
        {
            return _modVersion;
        }

        var attrs = typeof(OnlineMenuMod).Assembly.GetCustomAttributes(typeof(MelonInfoAttribute), false);
        if (attrs.Length > 0 && attrs[0] is MelonInfoAttribute info && !string.IsNullOrEmpty(info.Version))
        {
            _modVersion = info.Version;
            return _modVersion;
        }

        _modVersion = typeof(OnlineMenuMod).Assembly.GetName().Version?.ToString() ?? "0";
        return _modVersion;
    }

    public static TMP_FontAsset? LoadTmpFont(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var key = name.Trim();
        if (FontCache.TryGetValue(key, out var cached) && cached)
        {
            return cached;
        }

        var loaded = FindLoadedTmpFont(key);
        if (loaded)
        {
            FontCache[key] = loaded!;
            return loaded;
        }

        foreach (var address in BuildFontAddresses(key))
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<TMP_FontAsset>(address);
                var font = handle.WaitForCompletion();
                if (!font)
                {
                    continue;
                }

                FontCache[key] = font;
                return font;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[ModUiUtils] font Addressables load failed ({address}): {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Load an embedded image (PNG/JPG) as a Sprite.
    /// Accepts a full manifest name (e.g. TFROnlineMenu.LinkSplash.png) or a short file name.
    /// </summary>
    public static Sprite? LoadEmbeddedSprite(string resourceName, Assembly? assembly = null)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        var key = resourceName.Trim();
        if (SpriteCache.TryGetValue(key, out var cached) && cached)
        {
            return cached;
        }

        assembly ??= typeof(ModUiUtils).Assembly;
        using var stream = OpenEmbeddedResource(assembly, key);
        if (stream is null)
        {
            LoggerInstance.Warning($"[ModUiUtils] embedded image not found: {key}");
            return null;
        }

        var managed = new byte[stream.Length];
        var read = stream.Read(managed, 0, managed.Length);
        if (read <= 0)
        {
            return null;
        }

        var bytes = new Il2CppStructArray<byte>(managed.Length);
        for (var i = 0; i < managed.Length; i++)
        {
            bytes[i] = managed[i];
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes))
        {
            UnityEngine.Object.Destroy(texture);
            LoggerInstance.Warning($"[ModUiUtils] failed to decode embedded image: {key}");
            return null;
        }

        var spriteName = Path.GetFileNameWithoutExtension(key);
        if (string.IsNullOrEmpty(spriteName))
        {
            spriteName = key;
        }

        texture.name = spriteName;
        texture.hideFlags = HideFlags.HideAndDontSave;
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        SpriteCache[key] = sprite;
        return sprite;
    }

    private static Stream? OpenEmbeddedResource(Assembly assembly, string resourceName)
    {
        var direct = assembly.GetManifestResourceStream(resourceName);
        if (direct is not null)
        {
            return direct;
        }

        var names = assembly.GetManifestResourceNames();
        foreach (var name in names)
        {
            if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("." + resourceName, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
            {
                return assembly.GetManifestResourceStream(name);
            }
        }

        return null;
    }

    private static TMP_FontAsset? FindLoadedTmpFont(string name)
    {
        TMP_FontAsset? containsMatch = null;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (fonts is null)
        {
            return null;
        }

        foreach (var font in fonts)
        {
            if (!font || font.name is null)
            {
                continue;
            }

            if (font.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return font;
            }

            if (containsMatch is null &&
                font.name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                containsMatch = font;
            }
        }

        return containsMatch;
    }

    private static IEnumerable<string> BuildFontAddresses(string name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in EnumerateFontAddressCandidates(name))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateFontAddressCandidates(string name)
    {
        yield return $"Assets/Fonts/{name}.asset";

        if (!name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            // common TMP naming patterns in this game's catalog
            if (!name.Contains("SDF", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"Assets/Fonts/{name} SDF.asset";
                yield return $"Assets/Fonts/{name} SDF Menu.asset";
                yield return $"Assets/Fonts/{name} SDF 2.asset";
            }

            yield return $"Assets/Fonts/{name} Menu.asset";
        }
    }
}
