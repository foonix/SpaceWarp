﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SpaceWarpPatcher;
using Newtonsoft.Json;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Loading;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Mods.JSON;
using SpaceWarp.API.UI.Appbar;
using SpaceWarp.API.Versions;
using SpaceWarp.Backend.Patching;
using SpaceWarp.Backend.UI.Appbar;
using SpaceWarp.UI.Console;
using SpaceWarp.UI.ModList;
using UnityEngine;

namespace SpaceWarp;

/// <summary>
///     Handles all the SpaceWarp initialization and mod processing.
/// </summary>
internal static class SpaceWarpManager
{
    internal static ManualLogSource Logger;
    internal static ConfigurationManager.ConfigurationManager ConfigurationManager;

    internal static string SpaceWarpFolder;
    // The plugin can be null
    internal static IReadOnlyList<SpaceWarpPluginDescriptor> SpaceWarpPlugins;
    internal static IReadOnlyList<BaseUnityPlugin> NonSpaceWarpPlugins;
    internal static IReadOnlyList<(BaseUnityPlugin, ModInfo)> NonSpaceWarpInfos;

    internal static IReadOnlyList<(PluginInfo, ModInfo)> DisabledInfoPlugins;
    internal static IReadOnlyList<PluginInfo> DisabledNonInfoPlugins;
    internal static IReadOnlyList<(string, bool)> PluginGuidEnabledStatus;

    internal static readonly Dictionary<string, bool> ModsOutdated = new();
    internal static readonly Dictionary<string, bool> ModsUnsupported = new();

    private static GUISkin _skin;

    public static ModListController ModListController { get; internal set; }
    
    public static SpaceWarpConsole SpaceWarpConsole { get; internal set; }

    
    [Obsolete("Spacewarps support for IMGUI will not be getting updates, please use UITK instead")]
    public static GUISkin Skin
    {
        get
        {
            if (!_skin)
            {
                AssetManager.TryGetAsset("spacewarp/swconsoleui/spacewarpconsole.guiskin", out _skin);
            }

            return _skin;
        }
    }

    internal static void GetSpaceWarpPlugins()
    {
        var pluginGuidEnabledStatus = new List<(string, bool)>();
        // obsolete warning for Chainloader.Plugins, is fine since we need ordered list
        // to break this we would likely need to upgrade to BIE 6, which isn't happening
#pragma warning disable CS0618
        var spaceWarpPlugins = Chainloader.Plugins.OfType<BaseSpaceWarpPlugin>().ToList();
        // SpaceWarpPlugins = spaceWarpPlugins;
        var spaceWarpInfos = new List<SpaceWarpPluginDescriptor>();
        var ignoredGUIDs = new List<string>();

        foreach (var plugin in spaceWarpPlugins.ToArray())
        {
            var folderPath = Path.GetDirectoryName(plugin.Info.Location);
            plugin.PluginFolderPath = folderPath;
            if (Path.GetFileName(folderPath) == "plugins")
            {
                Logger.LogError(
                    $"Found Space Warp mod {plugin.Info.Metadata.Name} in the BepInEx/plugins directory. This mod will not be initialized.");
                ignoredGUIDs.Add(plugin.Info.Metadata.GUID);
                continue;
            }

            var modInfoPath = Path.Combine(folderPath!, "swinfo.json");
            
            if (!File.Exists(modInfoPath))
            {
                Logger.LogError(
                    $"Found Space Warp plugin {plugin.Info.Metadata.Name} without a swinfo.json next to it. This mod will not be initialized.");
                ignoredGUIDs.Add(plugin.Info.Metadata.GUID);
                continue;
            }

            ModInfo metadata;
            try
            {
                metadata = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(modInfoPath));
            }
            catch
            {
                Logger.LogError(
                    $"Error reading metadata for spacewarp plugin {plugin.Info.Metadata.Name}. This mod will not be initialized");
                ignoredGUIDs.Add(plugin.Info.Metadata.GUID);
                continue;
            }

            if (metadata.Spec >= SpecVersion.V1_3)
            {
                // Enforce Mod ID here
                var modID = metadata.ModID;
                if (modID != plugin.Info.Metadata.GUID)
                {
                    Logger.LogError(
                        $"Found Space Warp plugin {plugin.Info.Metadata.Name} that has an swinfo.json w/ spec version >= 1.3 that's ModID is not the same as the plugins GUID, This mod will not be initialized.");
                    ignoredGUIDs.Add(plugin.Info.Metadata.GUID);
                    continue;
                }
                // We should also enforce equality between versions w/ this spec
                if (new Version(metadata.Version) != plugin.Info.Metadata.Version)
                {
                    Logger.LogError($"Found Space Warp plugin {plugin.Info.Metadata.Name} that's swinfo version does not match the plugin version, this mod will not be initialized");
                    ignoredGUIDs.Add(plugin.Info.Metadata.GUID);
                    continue;
                }
            }
            
            

            plugin.SpaceWarpMetadata = metadata;
            var directoryInfo = new FileInfo(modInfoPath).Directory;
            spaceWarpInfos.Add(new(plugin, plugin.Info.Metadata.GUID, metadata,directoryInfo));
            pluginGuidEnabledStatus.Add((plugin.Info.Metadata.GUID, true));
        }

        var allPlugins = Chainloader.Plugins.ToList();
        List<BaseUnityPlugin> nonSWPlugins = new();
        List<(BaseUnityPlugin, ModInfo)> nonSWInfos = new();
        foreach (var plugin in allPlugins)
        {
            if (spaceWarpPlugins.Contains(plugin as BaseSpaceWarpPlugin))
            {
                continue;
            }

            var folderPath = Path.GetDirectoryName(plugin.Info.Location);
            var modInfoPath = Path.Combine(folderPath!, "swinfo.json");
            if (File.Exists(modInfoPath))
            {
                var info = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(modInfoPath));
                nonSWInfos.Add((plugin, info));
            }
            else
            {
                nonSWPlugins.Add(plugin);
            }
            pluginGuidEnabledStatus.Add((plugin.Info.Metadata.GUID, true));
        }
#pragma warning restore CS0618
        NonSpaceWarpPlugins = nonSWPlugins;
        NonSpaceWarpInfos = nonSWInfos;

        var disabledInfoPlugins = new List<(PluginInfo, ModInfo)>();
        var disabledNonInfoPlugins = new List<PluginInfo>();

        var disabledPlugins = ChainloaderPatch.DisabledPlugins;
        foreach (var plugin in disabledPlugins)
        {
            var folderPath = Path.GetDirectoryName(plugin.Location);
            var swInfoPath = Path.Combine(folderPath!, "swinfo.json");
            if (Path.GetFileName(folderPath) != "plugins" && File.Exists(swInfoPath))
            {
                try
                {
                    var swInfo = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(swInfoPath));
                    disabledInfoPlugins.Add((plugin, swInfo));
                }
                catch
                {
                    disabledNonInfoPlugins.Add(plugin);
                }
            }
            else
            {
                disabledNonInfoPlugins.Add(plugin);
            }
            pluginGuidEnabledStatus.Add((plugin.Metadata.GUID, false));
        }
        
        // Now here is where we can find "codeless" mods
        // We just loop through every swinfo we find and do stuff w/ it

        var codelessInfos = new List<SpaceWarpPluginDescriptor>();
        var pluginPath = new DirectoryInfo(Paths.PluginPath);
        foreach (var swinfo in pluginPath.GetFiles("swinfo.json", SearchOption.AllDirectories))
        {
            ModInfo swinfoData;
            try
            {
                swinfoData = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(swinfo.FullName));
            }
            catch
            {
                Logger.LogError($"Error reading metadata file: {swinfo.FullName}, this mod will be ignored");
                continue;
            }

            if (swinfoData.Spec < SpecVersion.V1_3)
            {
                Logger.LogWarning(
                    $"Found swinfo information for: {swinfoData.Name}, but its spec is less than v1.3, if this describes a \"codeless\" mod, it will be ignored");
                continue;
            }

            var guid = swinfoData.ModID;
            // If we already know about this mod, ignore it
            if (Chainloader.PluginInfos.ContainsKey(guid)) continue;
            
            // Now we can just add it to our plugin list
            codelessInfos.Add(new(null, guid, swinfoData, swinfo.Directory));
        }

        var codelessInfosInOrder =
            new List<SpaceWarpPluginDescriptor>();
        
        // Check for the dependencies on codelessInfos

        bool CodelessDependencyResolved(SpaceWarpPluginDescriptor descriptor)
        {
            foreach (var dependency in descriptor.SWInfo.Dependencies)
            {
                if (Chainloader.PluginInfos.ContainsKey(dependency.ID) && !ignoredGUIDs.Contains(dependency.ID))
                {
                    var chainloaderVersion = Chainloader.PluginInfos[dependency.ID].Metadata.Version.ToString();
                    if (!VersionUtility.IsSupported(chainloaderVersion, dependency.Version.Min, dependency.Version.Max))
                        return false;
                }

                var codelessInfo = codelessInfosInOrder.FirstOrDefault(x => x.Guid == dependency.ID);
                if (codelessInfo == null || !VersionUtility.IsSupported(codelessInfo.SWInfo.Version, dependency.Version.Min,
                        dependency.Version.Max)) return false;
            }

            return true;
        }
        
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (var i = codelessInfos.Count - 1; i >= 0; i--)
            {
                if (!CodelessDependencyResolved(codelessInfos[i])) continue;
                codelessInfosInOrder.Add(codelessInfos[i]);
                codelessInfos.RemoveAt(i);
                changed = true;
            }
        }

        if (codelessInfos.Count > 0)
        {
            foreach (var info in codelessInfos)
            {
                // TODO: Specific dependency warnings in the mod list at some point!
                Logger.LogError($"Missing dependency for codeless mod: ${info.SWInfo.Name}, this mod will not be loaded");
            }
        }


        spaceWarpInfos.AddRange(codelessInfosInOrder);
        SpaceWarpPlugins = spaceWarpInfos;
        DisabledInfoPlugins = disabledInfoPlugins;
        DisabledNonInfoPlugins = disabledNonInfoPlugins;
        PluginGuidEnabledStatus = pluginGuidEnabledStatus;
    }

    public static void Initialize(SpaceWarpPlugin spaceWarpPlugin)
    {
        Logger = SpaceWarpPlugin.Logger;

        SpaceWarpFolder = Path.GetDirectoryName(spaceWarpPlugin.Info.Location);

        AppbarBackend.AppBarInFlightSubscriber.AddListener(Appbar.LoadAllButtons);
        AppbarBackend.AppBarOABSubscriber.AddListener(Appbar.LoadOABButtons);
    }


    internal static void CheckKspVersions()
    {
        var kspVersion = typeof(VersionID).GetField("VERSION_TEXT", BindingFlags.Static | BindingFlags.Public)
            ?.GetValue(null) as string;
        foreach (var plugin in SpaceWarpPlugins)
        {
            // CheckModKspVersion(plugin.Info.Metadata.GUID, plugin.SpaceWarpMetadata, kspVersion);
            CheckModKspVersion(plugin.Guid, plugin.SWInfo, kspVersion);
        }

        foreach (var info in NonSpaceWarpInfos)
        {
            CheckModKspVersion(info.Item1.Info.Metadata.GUID, info.Item2, kspVersion);
        }

        foreach (var info in DisabledInfoPlugins)
        {
            CheckModKspVersion(info.Item1.Metadata.GUID, info.Item2, kspVersion);
        }
    }

    private static void CheckModKspVersion(string guid, ModInfo modInfo, string kspVersion)
    {
        var unsupported = true;
        try
        {
            unsupported = !modInfo.SupportedKsp2Versions.IsSupported(kspVersion);
        }
        catch (Exception e)
        {
            Logger.LogError($"Unable to check KSP version for {guid} due to error {e}");
        }

        ModsUnsupported[guid] = unsupported;
    }

    private static List<(string name, UnityObject asset)> AssetBundleLoadingAction(string internalPath, string filename)
    {
        var assetBundle = AssetBundle.LoadFromFile(filename);
        if (assetBundle == null)
        {
            throw new Exception(
                $"Failed to load AssetBundle {internalPath}");
        }

        internalPath = internalPath.Replace(".bundle", "");
        var names = assetBundle.GetAllAssetNames();
        List<(string name, UnityObject asset)> assets = new();
        foreach (var name in names)
        {
            var assetName = name;

            if (assetName.ToLower().StartsWith("assets/"))
            {
                assetName = assetName["assets/".Length..];
            }

            if (assetName.ToLower().StartsWith(internalPath + "/"))
            {
                assetName = assetName[(internalPath.Length + 1)..];
            }

            var path = internalPath + "/" + assetName;
            path = path.ToLower();
            var asset = assetBundle.LoadAsset(name);
            assets.Add((path, asset));
        }

        return assets;
    }

    private static List<(string name, UnityObject asset)> ImageLoadingAction(string internalPath, string filename)
    {
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Point
        };
        var fileData = File.ReadAllBytes(filename);
        tex.LoadImage(fileData); // Will automatically resize
        List<(string name, UnityObject asset)> assets = new() { ($"images/{internalPath}", tex) };
        return assets;
    }

    internal static void InitializeSpaceWarpsLoadingActions()
    {
        Loading.AddAssetLoadingAction("bundles", "loading asset bundles", AssetBundleLoadingAction, "bundle");
        Loading.AddAssetLoadingAction("images", "loading images", ImageLoadingAction);
    }
}