using Riftworks.src.BE;
using Riftworks.src.Blocks;
using Riftworks.src.Config;
using Riftworks.src.Items;
using Riftworks.src.Items.Wearable;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Riftworks.src;

public class RiftworksModSystem : ModSystem
{
    public static RiftworksConfig Config { get; private set; } = new();

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
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemOreScanner", typeof(ItemOreScanner));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemGravityBoots", typeof(ItemGravityBoots));

    }

    public static void TryLoadConfig(ICoreAPI api)
    {
        try
        {
            Config = api.LoadModConfig<RiftworksConfig>("riftworks.json") ?? new RiftworksConfig();
            api.StoreModConfig(Config, "riftworks.json");
        }
        catch (Exception exception)
        {
            api.Logger.Error("Riftworks: Failed to load config, using defaults.", exception);
            Config = new RiftworksConfig();
        }
    }

    // After all JSON assets have been loaded, remove any configured recipes
    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server)
        {
            return;
        }

        List<GridRecipe> recipes = api.World.GridRecipes;

        if (Config.DisableVectorStasisUnit)
        {
            recipes.RemoveAll(recipe => recipe?.Output?.Code?.PathStartsWith("vectorstasisunit") == true);
        }

        if (Config.DisableStormCaster)
        {
            recipes.RemoveAll(recipe => recipe?.Output?.Code?.PathStartsWith("stormcaster") == true);
        }

        if (Config.DisableTemporalDisassembler)
        {
            recipes.RemoveAll(recipe => recipe?.Output?.Code?.PathStartsWith("temporaldisassembler") == true);
        }

        if (Config.DisableOreScanner)
        {
            recipes.RemoveAll(recipe => recipe?.Output?.Code?.PathStartsWith("orescanner") == true);
        }

        if (Config.DisableAdaptiveReconstitutionGear)
        {
            recipes.RemoveAll(recipe => recipe?.Output?.Code?.PathStartsWith("adaptivereconstitutiongear") == true);
        }
    }

}
