using KSP.Game.Flow;
using SpaceWarp.API.Mods;
using System;

namespace SpaceWarp.Patching.LoadingActions
{
    internal sealed class PreInitializeModAction : FlowAction
    {
        private readonly SpaceWarpPluginDescriptor _plugin;

        public PreInitializeModAction(SpaceWarpPluginDescriptor plugin)
            : base($"Pre-initialization for plugin {plugin.Name}")
        {
            _plugin = plugin;
        }

        protected override void DoAction(Action resolve, Action<string> reject)
        {
            SpaceWarpPlugin.Instance.SWLogger.LogInfo($"Pre-initializing: {_plugin.Name}?");
            try
            {
                if (_plugin.DoLoadingActions)
                {
                    SpaceWarpPlugin.Instance.SWLogger.LogInfo($"YES! {_plugin.Plugin}");
                    _plugin.Plugin.OnPreInitialized();
                }
                else
                {
                    SpaceWarpPlugin.Instance.SWLogger.LogInfo("NO!!");
                }

                resolve();
            }
            catch (Exception e)
            {
                (_plugin.Plugin ?? SpaceWarpPlugin.Instance).SWLogger.LogError(e.ToString());
                reject(null);
            }
        }
    }
}