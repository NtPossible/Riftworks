using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    public abstract class ModSystemWearableTick<TItem> : ModSystem
        where TItem : ItemWearable
    {
        private ICoreServerAPI sapi;
        private double lastCheckTotalHours;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000);
        }

        private void OnTickServer1s(float dt)
        {
            double totalHours = sapi.World.Calendar.TotalHours;
            double hoursPassed = totalHours - lastCheckTotalHours;

            if (hoursPassed > 0.05)
            {
                foreach (IPlayer player in sapi.World.AllOnlinePlayers)
                {
                    IInventory inventory = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                    if (inventory == null)
                    {
                        continue;
                    }

                    ItemSlot slot = inventory.FirstOrDefault(itemSlot => itemSlot.Itemstack?.Collectible is TItem);
                    if (slot != null && slot.Itemstack.Collectible is TItem item)
                    {
                        HandleItem(player, item, slot, hoursPassed, dt);
                    }
                    else
                    {
                        HandleMissing(player);
                    }
                }

                lastCheckTotalHours = totalHours;
            }
        }

        protected abstract void HandleItem(IPlayer player, TItem item, ItemSlot slot, double hoursPassed, float dt);

        protected virtual void HandleMissing(IPlayer player) { }
    }
}