﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace SpaceWarp.Patching.LoadingActions
{
    internal static class FunctionalLoadingActions
    {
        internal static List<(string name, UnityObject asset)> AssetBundleLoadingAction(
            string internalPath,
            string filename
        )
        {
            var assetBundle = AssetBundle.LoadFromFile(filename);
            if (assetBundle == null)
            {
                throw new Exception($"Failed to load AssetBundle {internalPath} ({filename})");
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
                try
                {
                    SpaceWarpPlugin.Instance.SWLogger.LogInfo($"Attempting to load asset {name} from bundle at {filename}");
                    var asset = assetBundle.LoadAsset(name);
                    assets.Add((path, asset));
                }
                catch (Exception e)
                {
                    SpaceWarpPlugin.Instance.SWLogger.LogInfo($"Error loading asset {name} from bundle at {filename}: {e}");
                }
            }

            return assets;
        }


        internal static List<(string name, UnityObject asset)> ImageLoadingAction(string internalPath, string filename)
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
    }
}