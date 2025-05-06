using HarmonyLib;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    public class ModSystemDivingHelmet : ModSystem
    {
        ICoreServerAPI sapi;
        private const float maxOxygen = 300000f; // 5 minutes

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            new Harmony("riftworks.divinghelmet").PatchAll();
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000);
        }

        private void OnTickServer1s(float dt)
        {
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (inv == null) continue;

                ItemSlot headSlot = inv[(int)EnumCharacterDressType.ArmorHead];
                EntityPlayer entity = player.Entity;
                EntityBehaviorBreathe breathe = entity.GetBehavior<EntityBehaviorBreathe>();
                if (breathe == null) continue;

                ITreeAttribute oxygenTree = entity.WatchedAttributes.GetTreeAttribute("oxygen");
                if (oxygenTree == null) continue;

                // Store default oxygen max so I can default to it later
                if (!oxygenTree.HasAttribute("DefaultMaxOxygen"))
                {
                    oxygenTree.SetFloat("DefaultMaxOxygen", breathe.MaxOxygen);
                }

                if (headSlot?.Itemstack?.Collectible is ItemDivingHelmet)
                {
                    breathe.MaxOxygen = maxOxygen;

                    if (entity.Swimming)
                    {
                        entity.WatchedAttributes.SetBool("riftworksHelmetLight", true);
                    }
                    else
                    {
                        entity.WatchedAttributes.SetBool("riftworksHelmetLight", false);
                    }

                }
                else
                {
                    float defaultMax = oxygenTree.GetFloat("DefaultMaxOxygen");
                    breathe.MaxOxygen = defaultMax;

                    if (breathe.Oxygen > defaultMax)
                    {
                        breathe.Oxygen = defaultMax;
                    }

                    entity.WatchedAttributes.SetBool("riftworksHelmetLight", false);

                }
            }
        }

    }

    public class ItemDivingHelmet : ItemWearable
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine(Lang.Get("Activates light and increases breath time to 5 minutes when underwater."));
        }
    }
}
