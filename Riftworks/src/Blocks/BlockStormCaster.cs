using Vintagestory.API.Common;
using Riftworks.src.BE;

namespace Riftworks.src.Blocks
{
    internal class BlockStormCaster : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = GetBlockEntity<BlockEntityStormCaster>(blockSel);
            if (be != null && be.OnInteract(blockSel, byPlayer))
            {
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
