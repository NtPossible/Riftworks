using Vintagestory.API.Common;
using Riftworks.src.BlockEntity;

namespace Riftworks.src.Blocks
{
    class BlockTemporalDisassembler : Block
    {

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }


            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityTemporalDisassembler beTemporalDisassembler) return false;

            if (!beTemporalDisassembler.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID))
            {
                return beTemporalDisassembler.OnPlayerRightClick(byPlayer, blockSel); 
            }

            return true;
        }


    }
}
