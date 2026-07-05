using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using System.Globalization;

// TODO: Afk jumpscares?

namespace FoxyJumpscare
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("lammas123.CrabDevKit")]
    public sealed class FoxyJumpscare : BasePlugin
    {
        internal static FoxyJumpscare Instance { get; private set; }

        internal ConfigEntry<bool> Networked;
        internal ConfigEntry<int> Chance;

        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            Networked = Config.Bind("FoxyJumpscare", "Networked", true, new ConfigDescription("If networking is enabled on the host, all clients that also have networking enabled will only be jumpscared when the host is jumpscared. This means everyone gets jumpscared together. If networking is disabled on you or the host's end, you'll be jumpscared individually."));
            Chance = Config.Bind("FoxyJumpscare", "Chance", 10_000, new ConfigDescription("The 1 in X chance every second to be jumpscared by Foxy. A chance of 0 (or negative) disables this chance.", new AcceptableValueRange<int>(0, int.MaxValue)));

            JumpscareManager.Init();
            JumpscareNet.Init();

            Log.LogInfo($"Initialized [{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION}]");
        }
    }
}