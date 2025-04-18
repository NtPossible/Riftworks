﻿using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Riftworks.src.Inventory
{
    /// <summary>
    /// Inventory with one normal slot, one gear slot and 9 output slots
    /// </summary>
    class InventoryTemporalDisassembler : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        public ItemSlot[] Slots { get { return slots; } }

        public InventoryTemporalDisassembler(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            // slot 0 = input
            // slot 1 = gear slot
            // slot 2-11 = output slots
            slots = GenEmptySlots(11);
            ConfigureSlotLimits(); 

        }

        public InventoryTemporalDisassembler(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(11);
            ConfigureSlotLimits(); 
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
                if (value == null) throw new ArgumentNullException(nameof(value));
                slots[slotId] = value;
            }
        }

        private void ConfigureSlotLimits()
        {
            // Allow only 1 item per slot for input and gear slot
            slots[0].MaxSlotStackSize = 1;
            slots[1].MaxSlotStackSize = 1;
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
            return new ItemSlotSurvival(this);
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
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
}
