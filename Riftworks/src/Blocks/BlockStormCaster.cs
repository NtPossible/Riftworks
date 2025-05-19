using Riftworks.src.BE;
using Vintagestory.API.Common;

namespace Riftworks.src.Blocks
{
    public class BlockStormCaster : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityStormCaster beStormCaster)
            {
                return false;
            }

            if (beStormCaster != null && beStormCaster.OnInteract(blockSel, byPlayer))
            {
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
