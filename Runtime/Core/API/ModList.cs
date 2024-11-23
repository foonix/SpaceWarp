using System.IO;
using System.Text;

namespace SpaceWarp.API;

/// <summary>
/// Contains methods related to the mod list.
/// </summary>
internal static class ModList
{
    /// <summary>
    /// The list of all disabled plugin GUIDs.
    /// </summary>
    internal static string[] DisabledPluginGuids { get; private set; }

    /// <summary>
    /// Whether the mod list changed since the last run.
    /// </summary>
    public static bool ChangedSinceLastRun { get; internal set; }

    internal static void Initialize()
    {
        if (!File.Exists(CommonPaths.DISABLED_PLUGINS))
        {
            File.Create(CommonPaths.DISABLED_PLUGINS).Dispose();
        }

        DisabledPluginGuids = File.ReadAllLines(CommonPaths.DISABLED_PLUGINS);

        var oldHash = "";
        if (File.Exists(CommonPaths.HASH_LOCATION))
        {
            oldHash = File.ReadAllText(CommonPaths.HASH_LOCATION);
            File.Delete(CommonPaths.HASH_LOCATION);
        }

        var newHash = File.ReadAllText(CommonPaths.DISABLED_PLUGINS);

        foreach (var swinfo in Directory.GetFiles(CommonPaths.MODS_FOLDER, "swinfo.json", SearchOption.AllDirectories))
        {
            newHash += File.ReadAllText(swinfo);
        }

        newHash = GetHash(newHash);
        
        File.WriteAllText(CommonPaths.HASH_LOCATION, newHash);

        ChangedSinceLastRun = newHash != oldHash;
    }

    private static string GetHash(string text)
    {
        var crypt = new System.Security.Cryptography.SHA256Managed();
        var hash = new StringBuilder();
        var crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(text));
        foreach (byte theByte in crypto)
        {
            hash.Append(theByte.ToString("x2"));
        }
        return hash.ToString();
    }
}