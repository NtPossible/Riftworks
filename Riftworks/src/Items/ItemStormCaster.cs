using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Riftworks.src.Items
{
    internal class ItemStormCaster : Item
    {
        private ICoreServerAPI sapi;
        bool isRaining = false;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var weatherThresholds = new (double Threshold, string EventName)[]
            {
                (75.0, "noevent"),
                (50.0, "lightthunder"),
                (25.0, "heavythunder"),
                (12.5, "smallhail"),
                (0,  "largehail")
            };

            Random random = new();
            int chance = random.Next(1, 100);

            if (!isRaining)
            {
                sapi.InjectConsole("/weather setprecip 1");

                string chosenEvent = weatherThresholds.FirstOrDefault(entry => chance >= entry.Threshold).EventName ?? "noevent";

                sapi.InjectConsole($"/weather setevr {chosenEvent}");

                isRaining = true;
            }
            else
            {
                isRaining = false;
                sapi.InjectConsole("/weather setprecip auto");
                sapi.InjectConsole("/weather setevr noevent");
            }
            
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
