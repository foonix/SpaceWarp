using System;
using System.Reflection;
using PlasticPipe.Server;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace SpaceWarp.InternalUtilities;


#if UNITY_EDITOR
internal static class AddressablesFixes
{
    internal static MethodInfo OriginalThunderkitMethod;

    static AddressablesFixes()
    {
        Type t = Type.GetType("ThunderKit.Addressable.Tools.AddressableGraphicsSettings, ThunderKit.Addressable.Tools")!;
        OriginalThunderkitMethod = t.GetMethod("RedirectInternalIdsToGameDirectory", BindingFlags.Static | BindingFlags.NonPublic)!;
    }
    
    
    internal static string RedirectInternalIdsToGameDirectoryFixed(IResourceLocation location)
    {
        if (location.InternalId.Contains("Mods")) return location.InternalId;
        return (string)OriginalThunderkitMethod.Invoke(null, new object[] {location});
    }
}
#endif