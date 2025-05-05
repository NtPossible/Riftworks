using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;

namespace Riftworks.src.Patches
{
    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    public class Patch_Entity_LightHsv
    {
        static bool Prefix(Entity __instance, ref byte[] __result)
        {
            if (__instance is not EntityPlayer entityPlayer) return true;

            // Only override if helmet light flag is enabled
            bool helmetLightEnabled = entityPlayer.WatchedAttributes.GetBool("riftworksHelmetLight");
            if (!helmetLightEnabled) return true;

            // Emit light
            __result = new byte[] { 25, 5, 5 }; 
            return false; // Skip original getter
        }
    }
}
