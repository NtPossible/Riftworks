using Riftworks.src.Items.Wearable;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Riftworks.src.Systems
{
    public class ModSystemGravityBoots : ModSystemWearableTick<ItemGravityBoots>
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
        }

        protected override void HandleItem(IPlayer player, ItemGravityBoots gravityBoots, ItemSlot slot, double hoursPassed, float dt)
        {

            double fuelBefore = gravityBoots.GetFuelHours(slot.Itemstack);

            if (hoursPassed > 0)
            {
                gravityBoots.AddFuelHours(slot.Itemstack, -hoursPassed);

                double fuelAfter = gravityBoots.GetFuelHours(slot.Itemstack);

                if (System.Math.Abs(fuelAfter - fuelBefore) >= 0.02)
                {
                    slot.MarkDirty();
                }
            }

            // allow walking on walls

        }
    }
}