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
        private ICoreServerAPI sapi;
        private const float maxOxygen = 300000f; // 5 minutes

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(OnServerTick1s, 1000);
        }

        private void OnServerTick1s(float dt)
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

                    //if (entity.Swimming)
                    //{
                        
                    //}
                    //else
                    //{
                        
                    //}
                }
                else
                {
                    float defaultMax = oxygenTree.GetFloat("DefaultMaxOxygen");
                    breathe.MaxOxygen = defaultMax;

                    if (breathe.Oxygen > defaultMax)
                    {
                        breathe.Oxygen = defaultMax;
                    }
                }
            }
        }

    }

    public class ItemDivingHelmet : ItemWearable
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine(Lang.Get("Grants 5 minutes of breath underwater."));
        }
    }
}
