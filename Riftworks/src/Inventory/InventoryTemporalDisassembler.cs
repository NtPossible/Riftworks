using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Riftworks.src.Inventory
{
    /// <summary>
    /// Inventory with one normal slot, one gear slot and 9 output slots
    /// </summary>
    public class InventoryTemporalDisassembler : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        public ItemSlot[] Slots { get { return slots; } }

        public InventoryTemporalDisassembler(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            // slot 0 = input
            // slot 1 = gear slot
            // slot 2-11 = output slots
            slots = GenEmptySlots(11);
        }

        public InventoryTemporalDisassembler(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(11);
        }

        public override int Count
        {
            get { return 11; }
        }
        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= Count) return null;
                return slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
                slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        protected override ItemSlot NewSlot(int i)
        {
            if (i < 2)
            {
                return new ItemSlotSurvival(this);
            }
            else
            {
                return new ItemSlotPreviewable(this);
            }
        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return slots[0];
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return slots[3];
        }
    }

    class ItemSlotPreviewable : ItemSlot
    {
        public ItemSlotPreviewable(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            return false;
        }
    }
}
