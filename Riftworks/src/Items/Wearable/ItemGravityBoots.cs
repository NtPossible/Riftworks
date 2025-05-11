using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riftworks.src.Items.Wearable
{
    public class ItemGravityBoots : FuelWearable
    {
        protected override float FuelHoursCapacity => 24f;

        protected override string MergeErrorItemName => "gravityboots";
    }
}
