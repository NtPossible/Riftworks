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

        protected override EnumCharacterDressType Slot => EnumCharacterDressType.Foot;

        protected override void HandleItem(IPlayer plr, ItemGravityBoots gravityBoots, ItemSlot footSlot, double hoursPassed, float dt)
        {
            if (hoursPassed > 0.05)
            {
                gravityBoots.AddFuelHours(footSlot.Itemstack, -hoursPassed);
                footSlot.MarkDirty();
                // allow walking on walls
            }
        }
    }
}