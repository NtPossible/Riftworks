using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Riftworks.src.Items.Wearable
{
    public abstract class FuelWearable : ItemWearable
    {
        protected abstract float FuelHoursCapacity { get; }

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
                if (fuel > 0 && fuelHoursLeft + fuel / 2 < FuelHoursCapacity)
                {
                    SetFuelHours(op.SinkSlot.Itemstack, fuel + fuelHoursLeft);
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    op.SinkSlot.MarkDirty();
                    return;
                }

                if (api.Side == EnumAppSide.Client)
                {
                    // TODO: Show proper error message
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "orescannerfull", Lang.Get("ingameerror-orescanner-full"));
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
