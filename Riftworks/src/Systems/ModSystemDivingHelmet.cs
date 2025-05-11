using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Riftworks.src.Items.Wearable;

namespace Riftworks.src.Systems
{
    public class DivingHelmetSystem : ModSystemWearableTick<ItemDivingHelmet>
    {
        private const float maxOxygen = 300000f; // 5 minutes

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            new Harmony("riftworks.divinghelmet").PatchAll();
            base.StartServerSide(api);
        }

        protected override EnumCharacterDressType Slot => EnumCharacterDressType.ArmorHead;

        protected override void HandleItem(IPlayer player, ItemDivingHelmet helmet, ItemSlot headSlot, double hoursPassed, float dt)
        {
            EntityPlayer entity = player.Entity;
            EntityBehaviorBreathe breathe = entity.GetBehavior<EntityBehaviorBreathe>();

            if (breathe == null)
            {
                return;
            }

            ITreeAttribute oxygenTree = entity.WatchedAttributes.GetTreeAttribute("oxygen");

            if (oxygenTree == null)
            {
                return;
            }

            // Store default oxygen max so I can default to it later
            if (!oxygenTree.HasAttribute("DefaultMaxOxygen"))
            {
                oxygenTree.SetFloat("DefaultMaxOxygen", breathe.MaxOxygen);
            }

            breathe.MaxOxygen = maxOxygen;
            entity.WatchedAttributes.SetBool("riftworksHelmetLight", entity.Swimming);
        }

        protected override void HandleMissing(IPlayer player, EnumCharacterDressType slot)
        {
            EntityPlayer entity = player.Entity;
            EntityBehaviorBreathe breathe = entity.GetBehavior<EntityBehaviorBreathe>();

            if (breathe == null)
            {
                return;
            }

            ITreeAttribute oxygenTree = entity.WatchedAttributes.GetTreeAttribute("oxygen");

            if (oxygenTree == null)
            {
                return;
            }

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
