using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Riftworks.src.Blocks;
using Riftworks.src.BlockEntity;
using Riftworks.src.Items;
using Riftworks.src.Systems;

namespace Riftworks.src;

public class RiftworksModSystem : ModSystem
{

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass($"{Mod.Info.ModID}:BlockTemporalDisassembler", typeof(BlockTemporalDisassembler));
        api.RegisterBlockEntityClass($"{Mod.Info.ModID}:BETemporalDisassembler", typeof(BlockEntityTemporalDisassembler));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemRiftBlade", typeof(ItemRiftBlade));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemVectorStasisUnit", typeof(ItemVectorStasisUnit));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemAdaptiveReconstitutionGear", typeof(ItemAdaptiveReconstitutionGear));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemDivingHelmet", typeof(ItemDivingHelmet));
        api.RegisterItemClass($"{Mod.Info.ModID}:ItemOreScanner", typeof(ItemOreScanner));
        api.RegisterEntity("EntityAdaptiveReconstitutionGear", typeof(EntityAdaptiveReconstitutionGear));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
    }

}
