using Riftworks.src.BE;
using Riftworks.src.Blocks;
using Riftworks.src.Config;
using Riftworks.src.Entities;
using Riftworks.src.Items;
using Riftworks.src.Items.Wearable;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Riftworks.src;

public class RiftworksModSystem : ModSystem
{
    const string CONFIG_FILE = "riftworks.json";

    public override void Start(ICoreAPI api)
    {
        TryLoadConfig(api);

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

    void TryLoadConfig(ICoreAPI api)
    {
        try
        {
            RiftworksConfig.Loaded = api.LoadModConfig<RiftworksConfig>(CONFIG_FILE) ?? new RiftworksConfig();
            api.StoreModConfig<RiftworksConfig>(RiftworksConfig.Loaded, CONFIG_FILE);
        }
        catch (Exception e)
        {
            api.Logger.Error("Could not load Riftworks config, using defaults.", e);
            RiftworksConfig.Loaded = new RiftworksConfig();
        }
    }

    // After all JSON assets have been loaded, remove any configured recipes
    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        List<GridRecipe> recipes = api.World.GridRecipes;

        if (RiftworksConfig.Loaded.DisableDivingHelmet)
        {
            recipes.RemoveAll(r => r.Output.Code.PathStartsWith("divinghelmet"));
        }
        if (RiftworksConfig.Loaded.DisableVectorStasisUnit)
        {
            recipes.RemoveAll(r => r.Output.Code.PathStartsWith("vectorstasisunit"));
        }
        if (RiftworksConfig.Loaded.DisableStormCaster) { 
            recipes.RemoveAll(r => r.Output.Code.PathStartsWith("stormcaster"));
        }
        if (RiftworksConfig.Loaded.DisableTemporalDisassembler)
        {
            recipes.RemoveAll(r => r.Output.Code.PathStartsWith("temporaldisassembler"));
        }
        if (RiftworksConfig.Loaded.DisableOreScanner)
        {
            recipes.RemoveAll(r => r.Output.Code.PathStartsWith("orescanner"));
        }
    }

}
