using System.Globalization;
using MelonLoader;

namespace TFROnlineMenu.Utils;

internal enum LaunchAutoMode
{
    None,
    Host,
    Join,
}

/// <summary>
/// Shared launch switches: <c>--tfr-res</c>, <c>--tfr-pos</c>, <c>--tfr-skip-splash</c>,
/// <c>--tfr-host</c>, <c>--tfr-join</c> (optional address, default 127.0.0.1).
/// </summary>
internal static class LaunchArgs
{
    private static bool _parsed;

    internal static int? Width { get; private set; }
    internal static int? Height { get; private set; }
    internal static int? PosX { get; private set; }
    internal static int? PosY { get; private set; }
    internal static bool SkipSplash { get; private set; }
    internal static LaunchAutoMode Auto { get; private set; }
    internal static string? JoinAddress { get; private set; }

    internal static bool HasWindowOverride =>
        Width is not null || Height is not null || PosX is not null || PosY is not null;

    internal static void EnsureParsed()
    {
        if (_parsed)
        {
            return;
        }

        _parsed = true;
        Parse(Environment.GetCommandLineArgs());
    }

    private static void Parse(string[] args)
    {
        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsFlag(arg, "--tfr-skip-splash"))
            {
                SkipSplash = true;
            }
            else if (IsFlag(arg, "--tfr-host"))
            {
                Auto = LaunchAutoMode.Host;
            }
            else if (TryTakeValue(args, ref i, "--tfr-join", optional: true, out var join))
            {
                Auto = LaunchAutoMode.Join;
                if (!string.IsNullOrWhiteSpace(join))
                {
                    JoinAddress = join.Trim();
                }
            }
            else if (TryTakeValue(args, ref i, "--tfr-res", optional: false, out var res))
            {
                ParseRes(res);
            }
            else if (TryTakeValue(args, ref i, "--tfr-pos", optional: false, out var pos))
            {
                ParsePos(pos);
            }
        }
    }

    private static bool IsFlag(string arg, string name)
    {
        return arg.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryTakeValue(string[] args, ref int i, string name, bool optional, out string value)
    {
        var arg = args[i];
        if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
            {
                value = args[++i];
                return true;
            }

            value = "";
            return optional;
        }

        var prefix = name + "=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            return true;
        }

        value = "";
        return false;
    }

    private static void ParseRes(string value)
    {
        var parts = value.Split('x', 'X', '*');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
            width <= 0 || height <= 0)
        {
            MelonLogger.Warning($"[LaunchArgs] ignored --tfr-res '{value}'");
            return;
        }

        Width = width;
        Height = height;
    }

    private static void ParsePos(string value)
    {
        var parts = value.Split(',', ';');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            MelonLogger.Warning($"[LaunchArgs] ignored --tfr-pos '{value}'");
            return;
        }

        PosX = x;
        PosY = y;
    }
}
