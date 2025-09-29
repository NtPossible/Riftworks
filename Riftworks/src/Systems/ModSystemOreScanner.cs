using Riftworks.src.Items.Wearable;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Riftworks.src.Systems
{
    public class ModSystemOreScanner : ModSystemWearableTick<ItemOreScanner>
    {
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
        }

        // I wanted to detect ore visually but I suck
        protected override void HandleItem(IPlayer player, ItemOreScanner oreScanner, ItemSlot slot, double hoursPassed, float dt)
        {
            double fuelBefore = oreScanner.GetFuelHours(slot.Itemstack);

            if (hoursPassed > 0)
            {
                oreScanner.AddFuelHours(slot.Itemstack, -hoursPassed);

                double fuelAfter = oreScanner.GetFuelHours(slot.Itemstack);

                if (System.Math.Abs(fuelAfter - fuelBefore) >= 0.02)
                {
                    slot.MarkDirty();
                }
            }

            BlockPos centerPos = player.Entity.Pos.AsBlockPos;
            int scanRadius = 10;

            HashSet<string> detectedOres = new();

            for (int offsetX = -scanRadius; offsetX <= scanRadius; offsetX++)
            {
                for (int offsetY = -scanRadius; offsetY <= scanRadius; offsetY++)
                {
                    for (int offsetZ = -scanRadius; offsetZ <= scanRadius; offsetZ++)
                    {
                        BlockPos scanPos = new(centerPos.X + offsetX, centerPos.Y + offsetY, centerPos.Z + offsetZ);
                        Block scannedBlock = sapi.World.BlockAccessor.GetBlock(scanPos);

                        if (scannedBlock == null)
                        {
                            continue;
                        }

                        string path = scannedBlock.Code.Path;

                        // time to get the ore name
                        if (path.StartsWith("ore-"))
                        {
                            string trimmed = path.Substring("ore-".Length);
                            string[] parts = trimmed.Split('-');

                            string[] grades = new string[] { "poor", "medium", "rich", "bountiful" };

                            // Remove grade if present
                            int index = 0;
                            if (grades.Contains(parts[0]))
                            {
                                index = 1;
                            }

                            // If only 1 element left, it's the ore name
                            if (parts.Length - index == 1)
                            {
                                detectedOres.Add(parts[index]);
                            }
                            else if (parts.Length - index >= 2)
                            {
                                // Take everything except the last part
                                string[] oreParts = parts.Skip(index).Take(parts.Length - index - 1).ToArray();
                                string oreName = string.Join("-", oreParts);
                                detectedOres.Add(oreName);
                            }
                        }

                    }
                }
            }

            if (player is IServerPlayer serverPlayer && detectedOres.Count > 0)
            {
                string oreList = string.Join(", ", detectedOres);
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, $"Detected nearby ores - {oreList}.", EnumChatType.Notification);
            }
        }
    }

}
