using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Vellum.Networking;

namespace Vellum.Extension
{
    public class Host : InternalPlugin, IHost
    {
        public RunConfiguration RunConfig { get; internal set; }
        // Plugin host interface
        private List<IPlugin> _activePlugins = new List<IPlugin>();
        private string _pluginDir;
        public string PluginDirectory { get { return _pluginDir; } }

        public void SetPluginDirectory(string directory)
        {
            _pluginDir = directory;
        }

        public uint LoadPlugins()
        {
            uint pluginCount = 0;
            foreach (string pluginPath in Directory.GetFiles(_pluginDir, "*.dll", SearchOption.AllDirectories))
            {
                // System.Console.WriteLine("Loading plugin(s) from \"{0}\"...", pluginPath);
                Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
                foreach (Type type in pluginAssembly.GetTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(type))
                    {
                        Version minVersion = null;
#if !DEBUG
                        minVersion = (Version)type.GetField("MinVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(type);
#endif                        

                        if (minVersion == null || Assembly.GetExecutingAssembly().GetName().Version >= minVersion)
                        {
                            if (!RunConfig.Plugins.ContainsKey(type.Name))
                            {
                                RunConfig.Plugins.Add(type.Name, new PluginConfig()
                                {
                                    Enable = true,
                                    Config = type.GetMethod("GetDefaultRunConfiguration", BindingFlags.Public | BindingFlags.Static).Invoke(null, null)
                                });
                            }

                            if (RunConfig.Plugins[type.Name].Enable)
                            {
                                IPlugin plugin = (IPlugin)pluginAssembly.CreateInstance(type.FullName);
                                // plugin.PluginType = PluginType.EXTERNAL;
                                _activePlugins.Add(plugin);
                                pluginCount++;
                            }
                        } else
                        {
                            Console.WriteLine($"Plugin \"{type.Name}\" incompatible, requires host version v{UpdateChecker.ParseVersion(minVersion, VersionFormatting.MAJOR_MINOR_REVISION)}");
                        }
                    }
                }
            }

            // Initialize loaded plugins
            foreach (IPlugin plugin in _activePlugins)
                plugin.Initialize(this);

            return pluginCount;
        }

        public void AddPlugin(IPlugin plugin)
        {
            _activePlugins.Add(plugin);
            plugin.Initialize(this);
        }

        public List<IPlugin> GetPlugins()
        {
            return _activePlugins;
        }

        public IPlugin GetPluginByName(string name)
        {
            IPlugin plugin = null;

            foreach (IPlugin p in _activePlugins)
            {
                if (p.GetType().Name == name)
                {
                    plugin = p;
                    break;
                }                    
            }

            return plugin;
        }

        public T LoadPluginConfiguration<T>(Type type)
        {
            if (RunConfig.Plugins[type.Name].Config.GetType() == typeof(JObject))
                return ((JObject)RunConfig.Plugins[type.Name].Config).ToObject<T>();
            else
                return (T)(RunConfig.Plugins[type.Name].Config);
        }
    }
}