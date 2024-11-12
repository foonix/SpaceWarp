﻿using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using JetBrains.Annotations;
using KSP.ScriptInterop.impl.moonsharp;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using SpaceWarp.API.Loading;
using SpaceWarp.API.Lua;
using SpaceWarp.API.Mods;
using SpaceWarp.InternalUtilities;
using UitkForKsp2;
using SpaceWarp.Modules;
using SpaceWarp.Patching.LoadingActions;
using System.IO;
using System;

namespace SpaceWarp
{
    /// <summary>
    /// The main SpaceWarp plugin class.
    /// </summary>
    [BepInDependency(UitkForKsp2Plugin.ModGuid, UitkForKsp2Plugin.ModVer)]
    [BepInIncompatibility("com.shadow.quantum")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public sealed class SpaceWarpPlugin : BaseSpaceWarpPlugin
    {
        /// <summary>
        /// SpaceWarp plugin instance.
        /// </summary>
        public static SpaceWarpPlugin Instance;

        /// <summary>
        /// The GUID of the SpaceWarp plugin.
        /// </summary>
        [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;

        /// <summary>
        /// The name of the SpaceWarp plugin.
        /// </summary>
        [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;

        /// <summary>
        /// The version of the SpaceWarp plugin.
        /// </summary>
        [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

        internal ScriptEnvironment GlobalLuaState;

        internal new static ManualLogSource Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpaceWarpPlugin"/> class.
        /// </summary>
        public SpaceWarpPlugin()
        {
            // Load the type forwarders
            Assembly.LoadFile(
                $"{new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!.FullName}\\SpaceWarp.dll"
            );
            Logger = base.Logger;
            Instance = this;
        }

        private static void OnLanguageSourceAssetLoaded(LanguageSourceAsset asset)
        {
            if (!asset || LocalizationManager.Sources.Contains(asset.mSource))
            {
                return;
            }

            asset.mSource.owner = asset;
            LocalizationHelpers.AddSource(asset.mSource);
        }

        private void Awake()
        {
            BepInEx.Bootstrap.Chainloader.ManagerObject.Persist();
            // IOProvider.Init();

            Harmony.CreateAndPatchAll(typeof(SpaceWarpPlugin).Assembly, ModGuid);
            ModuleManager.LoadAllModules();

            Loading.AddAssetLoadingAction(
                "bundles",
                "loading asset bundles",
                FunctionalLoadingActions.AssetBundleLoadingAction,
                "bundle"
            );
            Loading.AddAssetLoadingAction(
                "images",
                "loading images",
                FunctionalLoadingActions.ImageLoadingAction
            );
            Loading.AddAddressablesLoadingAction<LanguageSourceAsset>(
                "localization",
                "language_source",
                OnLanguageSourceAssetLoaded
            );
        }

        private void SetupLuaState()
        {
            // I have been warned and I do not care
            UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;

            GlobalLuaState = (ScriptEnvironment)Game.ScriptEnvironment;
            GlobalLuaState.script.Globals["SWLog"] = Logger;

            // Now we loop over every assembly and import static lua classes for methods
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                UserData.RegisterAssembly(assembly);
                foreach (var type in assembly.GetTypes())
                {
                    // Now we can easily create API Objects from a [SpaceWarpLuaAPI] attribute
                    foreach (var attr in type.GetCustomAttributes())
                    {
                        if (attr is not SpaceWarpLuaAPIAttribute luaAPIAttribute) continue;
                        // As this seems to be used here
                        GlobalLuaState.script.Globals[luaAPIAttribute.LuaName] = UserData.CreateStatic(type);
                        break;
                    }
                }
            }
        }

        private void UpdateLanguagesDropdown()
        {
            Game.SettingsMenuManager._generalSettings.InitializeLanguageDropdown();
        }

        /// <inheritdoc/>
        public override void OnPreInitialized()
        {
            // Persist all game objects so I don't need to stomp on config
            ModuleManager.PreInitializeAllModules();
        }

        /// <inheritdoc/>
        public override void OnInitialized()
        {
            ModuleManager.InitializeAllModules();
            SetupLuaState();
            UpdateLanguagesDropdown();
        }

        /// <inheritdoc/>
        public override void OnPostInitialized()
        {
            ModuleManager.PostInitializeAllModules();
        }
    }
}
