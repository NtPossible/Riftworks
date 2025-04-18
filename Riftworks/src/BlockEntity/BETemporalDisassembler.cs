using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Riftworks.src.GUI;
using Riftworks.src.Inventory;

namespace Riftworks.src.BlockEntity
{
    class BlockEntityTemporalDisassembler : BlockEntityOpenableContainer
    {
        internal InventoryTemporalDisassembler inventory;

        GuiDialogBlockEntityTemporalDisassembler clientDialog;

        private float disassemblyTime = 0;
        private const float maxDisassemblyTime = 60f;
        private bool lastValidDisassemblyState = false;

        // Dictionary of preferred wildcard variants
        private static readonly Dictionary<string, string> PreferredWildcards = new Dictionary<string, string>
        {
            { "metalnailsandstrips-", "copper" },
            { "linen-", "normal-down" },
            { "plank-", "oak" },
            { "log-", "log-placed-oak-ud" }

        };

        #region Config

        public override string InventoryClassName
        {
            get { return "temporal disassembler"; }
        }

        public virtual string DialogTitle
        {
            get { return Lang.Get("Temporal Disassembler"); }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        #endregion

        public BlockEntityTemporalDisassembler()
        {
            inventory = new InventoryTemporalDisassembler(null, null);
            inventory.SlotModified += OnSlotModifid;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("temporaldisassembler-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            if (Api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(UpdateDisassemblyProgress, 100); 
            }
        }

        private void OnSlotModifid(int slotid)
        {
            // Force re-check
            lastValidDisassemblyState = false; 

            if (Api is ICoreClientAPI)
            {
                clientDialog.Update(disassemblyTime, maxDisassemblyTime);
            }

            if (slotid == 0 || slotid == 1)
            {
                if (!CanStartDisassembly())
                {
                    disassemblyTime = 0.0f; 
                }
                MarkDirty();

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                }
            }
        }


        private bool CanStartDisassembly()
        {
            // If state hasn't changed, no need to check again
            if (lastValidDisassemblyState) return true;

            bool isValid = !InputSlot.Empty && !GearSlot.Empty && GearSlot.Itemstack?.Item?.Code.ToString() == "game:gear-temporal";

            lastValidDisassemblyState = isValid; 
            return isValid;
        }

        private void UpdateDisassemblyProgress(float dt)
        {
            if (!CanStartDisassembly()) 
            {
                disassemblyTime = 0; 
                MarkDirty();
                return;
            }

            disassemblyTime += dt;
            if (disassemblyTime >= maxDisassemblyTime)
            {
                PerformDisassembly();
                disassemblyTime = 0;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                MarkDirty();
            }

            if (clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(disassemblyTime, maxDisassemblyTime);
            }

            MarkDirty();

        }

        private void PerformDisassembly()
        {
            List<ItemStack> disassembledItems = Disassemble(InputSlot.Itemstack);

            foreach (ItemStack item in disassembledItems)
            {
                if (item == null) continue;

                // Try to merge into existing stacks first
                foreach (var slot in inventory.Skip(2).Take(9))
                {
                    if (!slot.Empty && slot.Itemstack.Equals(Api.World, item, GlobalConstants.IgnoredStackAttributes))
                    {
                        // if the items match merge the item into the slot only if max stack size isnt reached
                        if (slot.Itemstack.StackSize < item.Collectible.MaxStackSize)
                        {
                            slot.Itemstack.StackSize = slot.Itemstack.StackSize + item.StackSize;
                            item.StackSize = item.StackSize - item.StackSize;
                            slot.MarkDirty();
                        }

                        if (item.StackSize <= 0) break;
                    }
                }

                // If item is still not empty, try placing in an empty slot
                if (item.StackSize > 0)
                {
                    ItemSlot emptySlot = inventory.Skip(2).FirstOrDefault(slot => slot.Empty);
                    if (emptySlot != null)
                    {
                        emptySlot.Itemstack = item.Clone();
                        emptySlot.MarkDirty();
                    }
                    else
                    {
                        // drop the item if no space
                        Api.World.SpawnItemEntity(item, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }
            }

            InputSlot.TakeOut(1); 
            GearSlot.TakeOut(1); 
        
            MarkDirty();
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        private List<ItemStack> Disassemble(ItemStack inputItem)
        {
            GridRecipe matchingRecipe = Api.World.GridRecipes.FirstOrDefault(recipe =>
                recipe.Output?.ResolvedItemstack.Equals(Api.World, inputItem, GlobalConstants.IgnoredStackAttributes) == true);

            if (matchingRecipe == null)
            {
                return new List<ItemStack> { inputItem };
            }

            List<ItemStack> resultItems = new List<ItemStack>();

            foreach (GridRecipeIngredient ingredient in matchingRecipe.resolvedIngredients)
            {
                if (ingredient == null || ingredient.IsTool) continue;

                if (ingredient.ResolvedItemstack != null)
                {
                    resultItems.Add(ingredient.ResolvedItemstack.Clone());
                }
                else if (ingredient.IsWildCard)
                {
                    string resolvedVariant = GetPreferredOrFirstWildcard(ingredient);

                    if (!string.IsNullOrEmpty(resolvedVariant))
                    {
                        CraftingRecipeIngredient resolvedIngredient = ingredient.Clone();
                        resolvedIngredient.Code.Path = ingredient.Code.Path.Replace("*", resolvedVariant);

                        if (resolvedIngredient.Resolve(Api.World, "TemporalDisassembler") && resolvedIngredient.ResolvedItemstack != null)
                        {
                            resultItems.Add(resolvedIngredient.ResolvedItemstack.Clone());
                        }
                    }
                }
            }


            return resultItems;
        }

        private string GetPreferredOrFirstWildcard(CraftingRecipeIngredient ingredient)
        {
            string basePath = ingredient.Code.Path.Replace("*", "");

            // First check the preferred variant dictionary
            if (PreferredWildcards.TryGetValue(basePath, out string preferredVariant))
            {
                return preferredVariant;
            }

            // If there are explicit AllowedVariants, use the first one
            if (ingredient.AllowedVariants?.Length > 0)
            {
                return ingredient.AllowedVariants[0];
            }

            // Find first valid variant from world registry
            if (ingredient.Type == EnumItemClass.Item)
            {
                return Api.World.Items
                    .Where(item => item.Code?.Path.StartsWith(basePath) == true)
                    .Select(item => item.Code.Path.Replace(basePath, ""))
                    .FirstOrDefault();
            }
            else if (ingredient.Type == EnumItemClass.Block)
            {
                return Api.World.Blocks
                    .Where(block => block.Code?.Path.StartsWith(basePath) == true)
                    .Select(block => block.Code.Path.Replace(basePath, ""))
                    .FirstOrDefault();
            }

            // No valid variant found
            return null; 
        }

        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer, () => {
                    clientDialog = new GuiDialogBlockEntityTemporalDisassembler(DialogTitle, Inventory, Pos, Api as ICoreClientAPI);
                    clientDialog.Update(disassemblyTime, maxDisassemblyTime);
                    return clientDialog;
                });
            }

            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

            if (Api != null)
            {
                Inventory.AfterBlocksLoaded(Api.World);
            }

            disassemblyTime = tree.GetFloat("disassemblyTime");

            if (Api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                clientDialog.Update(disassemblyTime, maxDisassemblyTime);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("disassemblyTime", disassemblyTime);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            clientDialog?.TryClose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
        }

        #endregion

        #region Helper getters

        public ItemSlot InputSlot
        {
            get { return inventory[0]; }
        }

        public ItemSlot GearSlot
        {
            get { return inventory[1]; }
        }

        #endregion
    }
}
