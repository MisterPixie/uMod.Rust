using Facepunch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using uMod.Libraries;
using uMod.Libraries.Universal;
using uMod.Logging;
using uMod.Plugins;

namespace uMod.Rust
{
    /// <summary>
    /// The core Rust plugin
    /// </summary>
    public partial class Rust : CSPlugin
    {
        #region Initialization

        /// <summary>
        /// Initializes a new instance of the Rust class
        /// </summary>
        public Rust()
        {
            // Set plugin info attributes
            Title = "Rust";
            Author = RustExtension.AssemblyAuthors;
            Version = RustExtension.AssemblyVersion;
        }

        // Instances
        internal static readonly RustProvider Universal = RustProvider.Instance;
        internal readonly PluginManager pluginManager = Interface.uMod.RootPluginManager;
        internal readonly IServer Server = Universal.CreateServer();

        // Libraries
        internal readonly Lang lang = Interface.uMod.GetLibrary<Lang>();
        internal readonly Permission permission = Interface.uMod.GetLibrary<Permission>();
        internal readonly Universal universal = Interface.uMod.GetLibrary<Universal>();

        internal bool serverInitialized;

        #endregion Initialization

        #region Core Hooks

        /// <summary>
        /// Called when the plugin is initializing
        /// </summary>
        [HookMethod("Init")]
        private void Init()
        {
            // Configure remote error logging
            RemoteLogger.SetTag("game", Title.ToLower());
            RemoteLogger.SetTag("game version", Server.Version);
            universal.DefaultCommandHandler.Initialize(this, VersionCommand);
            
            // Register messages for localization
            foreach (KeyValuePair<string, Dictionary<string, string>> language in Localization.languages)
            {
                lang.RegisterMessages(language.Value, this, language.Key);
            }

            // Set up default permission groups
            if (permission.IsLoaded)
            {
                int rank = 0;
                foreach (string defaultGroup in Interface.uMod.Config.Options.DefaultGroups)
                {
                    if (!permission.GroupExists(defaultGroup))
                    {
                        permission.CreateGroup(defaultGroup, defaultGroup, rank++);
                    }
                }

                permission.RegisterValidate(s =>
                {
                    ulong temp;
                    if (!ulong.TryParse(s, out temp))
                    {
                        return false;
                    }

                    int digits = temp == 0 ? 1 : (int)Math.Floor(Math.Log10(temp) + 1);
                    return digits >= 17;
                });

                permission.CleanUp();
            }
        }

        /// <summary>
        /// Called when another plugin has been loaded
        /// </summary>
        /// <param name="plugin"></param>
        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded(Plugin plugin)
        {
            if (serverInitialized)
            {
                // Call OnServerInitialized for hotloaded plugins
                plugin.CallHook("OnServerInitialized", false);
            }
        }

        /// <summary>
        /// Called when the server is first initialized
        /// </summary>
        [HookMethod("IOnServerInitialized")]
        private void IOnServerInitialized()
        {
            if (!serverInitialized)
            {
                // Let plugins know server startup is complete
                serverInitialized = true;
                Interface.CallHook("OnServerInitialized", serverInitialized);

                Interface.uMod.LogInfo($"uMod version {uMod.Version} running on {Universal.GameName} server version {Server.Version}");
                Analytics.Collect();

                if (!Interface.uMod.Config.Options.Modded)
                {
                    Interface.uMod.LogWarning("The server is currently listed under Community. Please be aware that Facepunch only allows admin tools" +
                      "(that do not affect gameplay or make the server appear modded) under the Community section");
                }
            }
        }

        /// <summary>
        /// Called when the server is saved
        /// </summary>
        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            // Trigger save process
            Interface.uMod.OnSave();

            // Save groups, users, and other data
            Universal.PlayerManager.SavePlayerData();
        }

        /// <summary>
        /// Called when the server is shutting down
        /// </summary>
        [HookMethod("OnServerShutdown")]
        private void OnServerShutdown()
        {
            // Trigger shutdown process
            Interface.uMod.OnShutdown();

            // Save groups, users, and other data
            Universal.PlayerManager.SavePlayerData();
        }

        #endregion Core Hooks

        #region Commands

        #region Version Command

        /// <summary>
        /// Called when the "version" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("VersionCommand")]
        private void VersionCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                player.Reply($"Protocol: {Server.Protocol}\nBuild Date: {BuildInfo.Current.BuildDate}\n" +
                $"Unity Version: {UnityEngine.Application.unityVersion}\nChangeset: {BuildInfo.Current.Scm.ChangeId}\n" +
                $"Branch: {BuildInfo.Current.Scm.Branch}\nuMod.Rust Version: {RustExtension.AssemblyVersion}");
            }
            else
            {
                universal.DefaultCommandHandler.VersionCommand(player, command, args);
            }
        }

        #endregion Version Command
        
        #endregion Commands
    }
}
