using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    internal class ModSystemGravityBoots : ModSystem
    {
        private ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000);
        }

        double lastCheckTotalHours;
        private void OnTickServer1s(float dt)
        {
            double totalHours = sapi.World.Calendar.TotalHours;
            double hoursPassed = totalHours - lastCheckTotalHours;

            if (hoursPassed > 0.05)
            {
                foreach (IPlayer plr in sapi.World.AllOnlinePlayers)
                {
                    IInventory inv = plr.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                    if (inv == null) continue;

                    ItemSlot footSlot = inv[(int)EnumCharacterDressType.Foot];
                    if (footSlot.Itemstack?.Collectible is ItemGravityBoots gravityBoots)
                    {
                        gravityBoots.AddFuelHours(footSlot.Itemstack, -hoursPassed);
                        footSlot.MarkDirty();

                        //allow walking on walls
                    }
                }

                lastCheckTotalHours = totalHours;
            }
        }
    }

    public class ItemGravityBoots : ItemWearable
    {
        protected float fuelHoursCapacity = 24;

        public double GetFuelHours(ItemStack stack)
        {
            return Math.Max(0, stack.Attributes.GetDecimal("fuelHours"));
        }
        public void SetFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", fuelHours);
        }
        public void AddFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", Math.Max(0, fuelHours + GetFuelHours(stack)));
        }

        public float GetStackFuel(ItemStack stack)
        {
            return stack.ItemAttributes?["nightVisionFuelHours"].AsFloat(0) ?? 0;
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(sourceStack);
                if (fuel == 0) return base.GetMergableQuantity(sinkStack, sourceStack, priority);
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(op.SourceSlot.Itemstack);
                double fuelHoursLeft = GetFuelHours(op.SinkSlot.Itemstack);
                if (fuel > 0 && fuelHoursLeft + fuel / 2 < fuelHoursCapacity)
                {
                    SetFuelHours(op.SinkSlot.Itemstack, fuel + fuelHoursLeft);
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    op.SinkSlot.MarkDirty();
                    return;
                }

                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "gravitybootsfull", Lang.Get("ingameerror-gravityboots-full"));
                }
            }
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            double fuelLeft = GetFuelHours(inSlot.Itemstack);
            dsc.AppendLine(Lang.Get("Has fuel for {0:0.#} hours", fuelLeft));
            if (fuelLeft <= 0)
            {
                dsc.AppendLine(Lang.Get("Add temporal gear to refuel"));
            }
        }
    }
}
