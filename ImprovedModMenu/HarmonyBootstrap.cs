using System.Runtime.CompilerServices;
using HarmonyLib;

#pragma warning disable CA2255

namespace ImprovedModMenu
{
    internal static class HarmonyBootstrap
    {
        private const string HarmonyId = "improvedmodmenu";
        private static readonly Harmony HarmonyInstance = new(HarmonyId);
        private static readonly object SyncRoot = new();
        private static bool patched;

        [ModuleInitializer]
        public static void Initialize()
        {
            EnsurePatched();
        }

        public static void EnsurePatched()
        {
            lock (SyncRoot)
            {
                if (patched)
                {
                    return;
                }

                HarmonyInstance.CreateClassProcessor(typeof(ImprovedModMenuModSystem.GuiScreenModsPatches)).Patch();
                patched = true;
            }
        }

        public static void Unpatch()
        {
            lock (SyncRoot)
            {
                if (!patched)
                {
                    return;
                }

                HarmonyInstance.UnpatchAll(HarmonyInstance.Id);
                patched = false;
            }
        }
    }
}
#pragma warning restore CA2255
