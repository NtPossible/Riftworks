using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Riftworks.src.Items.Wearable;

namespace Riftworks.src.Systems
{
    public class DivingHelmetSystem : ModSystemWearableTick<ItemDivingHelmet>
    {
        private const float maxOxygen = 300000f; // 5 minutes
        // dictionary incase for some reason a player has as different max oxygen as compared to others
        private readonly Dictionary<string, float> defaultOxygenByPlayer = new();

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            new Harmony("riftworks.divinghelmet").PatchAll();
            base.StartServerSide(api);
        }

        protected override void HandleItem(IPlayer player, ItemDivingHelmet divingHelmet, ItemSlot slot, double hoursPassed, float dt)
        {
            EntityPlayer entity = player.Entity;
            EntityBehaviorBreathe breathe = entity.GetBehavior<EntityBehaviorBreathe>();

            if (breathe == null)
            {
                return;
            }

            string uid = player.PlayerUID;

            // Cache the players default max-oxygen once
            if (!defaultOxygenByPlayer.ContainsKey(uid))
            {
                defaultOxygenByPlayer[uid] = breathe.MaxOxygen;
            }

            if (breathe.MaxOxygen != maxOxygen)
            {
                breathe.MaxOxygen = maxOxygen;
            }

            if (entity.WatchedAttributes.GetBool("riftworksHelmetLight") != entity.Swimming)
            {
                entity.WatchedAttributes.SetBool("riftworksHelmetLight", entity.Swimming);
            }
        }

        protected override void HandleMissing(IPlayer player)
        {
            EntityPlayer entity = player.Entity;
            EntityBehaviorBreathe breathe = entity.GetBehavior<EntityBehaviorBreathe>();

            if (breathe == null)
            {
                return;
            }

            string uid = player.PlayerUID;

            if (defaultOxygenByPlayer.TryGetValue(uid, out float originalMax))
            {
                breathe.MaxOxygen = originalMax;

                if (breathe.Oxygen > originalMax)
                {
                    breathe.Oxygen = originalMax;
                }
                defaultOxygenByPlayer.Remove(uid);
            }

            if (entity.WatchedAttributes.GetBool("riftworksHelmetLight"))
            {
                entity.WatchedAttributes.SetBool("riftworksHelmetLight", false);
            }
        }
    }
}
