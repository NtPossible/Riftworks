using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Riftworks.src.Blocks;
using Riftworks.src.BlockEntity;
using Riftworks.src.Items;
using Vintagestory.GameContent;
using Riftworks.src.Systems;
using Vintagestory.Client.NoObf;

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
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Mod.Logger.Notification("Hello from template mod server side");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        Mod.Logger.Notification("Hello from template mod client side");
        base.StartClientSide(api);
    }

}
