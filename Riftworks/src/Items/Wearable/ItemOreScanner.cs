using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Riftworks.src.Items.Wearable
{
    public class ItemOreScanner : FuelWearable
    {
        protected override float FuelHoursCapacity => 24f;
        protected override string MergeErrorItemName => "orescanner";

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world,bool debug)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, debug);
            dsc.AppendLine(Lang.Get("Scans for ores within 10 blocks"));
        }
    }
}
