using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Riftworks.src.Items.Wearable;

namespace Riftworks.src.Systems
{
    public class ModSystemOreScanner : ModSystemWearableTick<ItemOreScanner>
    {
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
        }

        protected override void HandleItem(IPlayer player, ItemOreScanner oreScanner, ItemSlot slot, double hoursPassed, float dt)
        {
            if (hoursPassed > 0.05)
            {
                oreScanner.AddFuelHours(slot.Itemstack, -hoursPassed);
                slot.MarkDirty();
                // scan for ores
            }
        }
    }

}
