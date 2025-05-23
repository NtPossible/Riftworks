﻿using Riftworks.src.BE;
using Riftworks.src.Blocks;
using Riftworks.src.Entities;
using Riftworks.src.Items;
using Riftworks.src.Items.Wearable;
using Vintagestory.API.Common;

namespace Riftworks.src;

public class RiftworksModSystem : ModSystem
{

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass($"{Mod.Info.ModID}:BlockTemporalDisassembler", typeof(BlockTemporalDisassembler));
        api.RegisterBlockEntityClass($"{Mod.Info.ModID}:BETemporalDisassembler", typeof(BlockEntityTemporalDisassembler));

        api.RegisterBlockClass($"{Mod.Info.ModID}:BlockStormCaster", typeof(BlockStormCaster));
        api.RegisterBlockEntityClass($"{Mod.Info.ModID}:BEStormCaster", typeof(BlockEntityStormCaster));

        api.RegisterItemClass($"{Mod.Info.ModID}:ItemRiftBlade", typeof(ItemRiftBlade));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemVectorStasisUnit", typeof(ItemVectorStasisUnit));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemAdaptiveReconstitutionGear", typeof(ItemAdaptiveReconstitutionGear));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemDivingHelmet", typeof(ItemDivingHelmet));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemOreScanner", typeof(ItemOreScanner));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemGravityBoots", typeof(ItemGravityBoots));

        api.RegisterEntity("EntityAdaptiveReconstitutionGear", typeof(EntityAdaptiveReconstitutionGear));
    }
}
