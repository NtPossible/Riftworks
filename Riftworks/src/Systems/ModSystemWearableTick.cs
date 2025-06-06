﻿using System.Linq;
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

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000, 200);
        }

        private void OnTickServer1s(float dt)
        {
            double hoursPassed = dt / 3600.0;

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
        }

        protected abstract void HandleItem(IPlayer player, TItem item, ItemSlot slot, double hoursPassed, float dt);

        protected virtual void HandleMissing(IPlayer player) { }
    }
}