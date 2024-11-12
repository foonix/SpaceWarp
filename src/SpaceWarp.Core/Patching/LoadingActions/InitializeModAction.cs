using KSP.Game.Flow;
using SpaceWarp.API.Mods;
using System;

namespace SpaceWarp.Patching.LoadingActions
{
    internal sealed class InitializeModAction : FlowAction
    {
        private readonly SpaceWarpPluginDescriptor _plugin;

        public InitializeModAction(SpaceWarpPluginDescriptor plugin) : base($"Initialization for plugin {plugin.Name}")
        {
            _plugin = plugin;
        }

        protected override void DoAction(Action resolve, Action<string> reject)
        {
            try
            {
                if (_plugin.DoLoadingActions)
                {
                    _plugin.Plugin.OnInitialized();
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