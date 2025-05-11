using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    public abstract class ModSystemWearableTick<TItem> : ModSystem
        where TItem : ItemWearable
    {
        protected abstract EnumCharacterDressType Slot { get; }

        private ICoreServerAPI sapi;
        private double lastTotalHours;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000);
        }

        private void OnTickServer1s(float dt)
        {
            double now = sapi.World.Calendar.TotalHours;
            double hoursPassed = now - lastTotalHours;
            if (hoursPassed <= 0) return;

            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (inv == null) continue;

                ItemSlot slot = inv[(int)Slot];
                if (slot.Itemstack?.Collectible is TItem item)
                {
                    HandleItem(player, item, slot, hoursPassed, dt);
                }
                else
                {
                    HandleMissing(player, Slot);
                }
            }

            lastTotalHours = now;
        }

        protected abstract void HandleItem(IPlayer player, TItem item, ItemSlot slot, double hoursPassed, float dt);

        protected virtual void HandleMissing(IPlayer player, EnumCharacterDressType slot) { }
    }
}