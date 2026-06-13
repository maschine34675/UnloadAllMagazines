using BepInEx;
using BepInEx.Logging;
using UnloadAllMagazines.Patches;

namespace UnloadAllMagazines
{
    [BepInPlugin("com.maschine.UnloadAllMagazines", "maschine-UnloadAllMagazines", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new UnloadAllMagazinesButtonPatch().Enable();
            Log.LogInfo("UnloadAllMagazines 1.0.0 loaded.");
        }
    }
}
