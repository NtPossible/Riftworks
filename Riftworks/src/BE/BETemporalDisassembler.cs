using Riftworks.src.GUI;
using Riftworks.src.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Riftworks.src.BE
{
    public class BlockEntityTemporalDisassembler : BlockEntityOpenableContainer
    {
        internal InventoryTemporalDisassembler inventory;

        GuiDialogBlockEntityTemporalDisassembler clientDialog;

        private float disassemblyTime = 0;
        private const float maxDisassemblyTime = 60f;

        private bool isPreviewActive = false;

        // Dictionary of preferred wildcard variants
        private static readonly Dictionary<string, string> PreferredWildcards = new()
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
            if (slotid == 0 || slotid == 1)
            {
                disassemblyTime = 0.0f;

                if (isPreviewActive)
                {
                    CancelPreview();
                }

                UpdatePreviewState();

                MarkDirty();
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                    clientDialog.Update(disassemblyTime, maxDisassemblyTime);
                }
            }
            else if (Api is ICoreClientAPI)
            {
                clientDialog?.Update(disassemblyTime, maxDisassemblyTime);
            }
        }

        private void UpdatePreviewState()
        {
            // Prevent previewing if any output slot has items
            bool outputsEmpty = inventory.Skip(2).All(slot => slot.Empty);
            bool shouldPreview = !InputSlot.Empty && GearSlot.Itemstack?.Item?.Code.ToString() == "game:gear-temporal" && outputsEmpty;

            if (shouldPreview && !isPreviewActive)
            {
                StartPreview();
            }
            else if (!shouldPreview && isPreviewActive)
            {
                CancelPreview();
            }
        }

        private void StartPreview()
        {
            LockAllOutputSlots();

            List<ItemStack> previewItems = Disassemble(InputSlot.Itemstack);

            foreach (ItemStack previewItem in previewItems)
            {
                InsertItemIntoOutputSlots(previewItem.Clone());
            }

            isPreviewActive = true;
            MarkDirty();
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        private void CancelPreview()
        {
            for (int i = 2; i < inventory.Count; i++)
            {
                if (inventory[i] is ItemSlotPreviewable previewableSlot)
                {
                    if (!previewableSlot.canTake)
                    {
                        previewableSlot.Itemstack = null;
                        previewableSlot.MarkDirty();
                    }

                    previewableSlot.canTake = false;
                }
            }

            isPreviewActive = false;

            MarkDirty();
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        private bool CanStartDisassembly()
        {
            return !InputSlot.Empty && !GearSlot.Empty && GearSlot.Itemstack?.Item?.Code.ToString() == "game:gear-temporal";
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
            ItemSlot inputSlot = InputSlot;
            ItemStack inputStack = inputSlot.Itemstack;

            if (inputStack == null)
            {
                return;
            }

            int itemsToConsume = CalculateItemsToConsume(inputStack);
            if (itemsToConsume <= 0)
            {
                return;
            }

            // Remove the inputs first then disassemble the removed stack
            ItemStack stackToProcess = inputSlot.TakeOut(itemsToConsume);
            GearSlot.TakeOut(1);
            List<ItemStack> outputs = Disassemble(stackToProcess);

            foreach (ItemStack itemStack in outputs)
            {
                InsertItemIntoOutputSlots(itemStack);
            }

            UnlockAllOutputSlots();
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        private GridRecipe GetGridRecipe(ItemStack inputStack)
        {
            // Strip attributes so we match purely on stack type
            ItemStack strippedInput = StripAttributes(inputStack);

            // Filter all grid‐recipes whose output type matches and whose batch size cleanly divides into our input stack size
            IEnumerable<GridRecipe> filteredRecipes = Api.World.GridRecipes
                .Where(recipe =>
                {
                    ItemStack outputStack = recipe.Output?.ResolvedItemstack;
                    return outputStack != null
                        && outputStack.Collectible == strippedInput.Collectible
                        && inputStack.StackSize >= outputStack.StackSize
                        && inputStack.StackSize % outputStack.StackSize == 0;
                });

            // Pick the recipe with the largest output stack
            GridRecipe matchingRecipe = filteredRecipes.OrderByDescending(recipe => recipe.Output?.ResolvedItemstack.StackSize).FirstOrDefault();

            return matchingRecipe;
        }

        private int CalculateItemsToConsume(ItemStack inputStack)
        {
            // Find a recipe whose output type matches and whose output stack size and cleanly divides into the input stack size.
            GridRecipe matchingRecipe = GetGridRecipe(inputStack);

            if (matchingRecipe != null)
            {
                int batchSize = matchingRecipe.Output.ResolvedItemstack.StackSize;
                int batchCount = inputStack.StackSize / batchSize;
                return batchCount * batchSize;
            }

            // If there is no recipe consume the whole stack
            return inputStack.StackSize;
        }

        private void InsertItemIntoOutputSlots(ItemStack itemStack)
        {
            if (itemStack == null)
            {
                return;
            }

            int remaining = itemStack.StackSize;
            CollectibleObject collectible = itemStack.Collectible;
            // Clone any attributes so each split stack keeps its data
            TreeAttribute treeAttributes = (TreeAttribute)itemStack.Attributes.Clone();

            List<ItemSlot> outputSlots = inventory.Skip(2).Take(9).ToList();
            foreach (ItemSlot slot in outputSlots)
            {
                if (slot.Empty || !slot.Itemstack.Equals(Api.World, itemStack, GlobalConstants.IgnoredStackAttributes))
                {
                    continue;
                }

                int canAdd = collectible.MaxStackSize - slot.Itemstack.StackSize;
                if (canAdd <= 0)
                {
                    continue;
                }

                int add = Math.Min(remaining, canAdd);
                slot.Itemstack.StackSize += add;
                remaining -= add;
                slot.MarkDirty();

                if (remaining <= 0)
                {
                    break;
                }
            }

            // Place any leftover into empty slots or drop them
            while (remaining > 0)
            {
                int amountToPlace = Math.Min(remaining, collectible.MaxStackSize);
                remaining -= amountToPlace;

                ItemStack splitStack = new(collectible.Id, collectible.ItemClass, amountToPlace, treeAttributes, Api.World);

                ItemSlot emptySlot = outputSlots.FirstOrDefault(slot => slot.Empty);
                if (emptySlot != null)
                {
                    emptySlot.Itemstack = splitStack;
                    emptySlot.MarkDirty();
                }
                else
                {
                    Api.World.SpawnItemEntity(splitStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }


        // Not really a disassemble now that it repairs. It does make sense lorewise though.
        private List<ItemStack> Disassemble(ItemStack inputItem)
        {
            // If it's repairable and damaged, repair instead of disassemble
            if (IsRepairableAndDamaged(inputItem))
            {
                return new List<ItemStack> { RepairItem(inputItem) };
            }

            ItemStack strippedInput = StripAttributes(inputItem);
            GridRecipe matchingRecipe = GetGridRecipe(strippedInput);

            if (matchingRecipe == null)
            {
                return new List<ItemStack> { inputItem };
            }

            List<ItemStack> resultItems = new();
            int batchSize = matchingRecipe.Output.ResolvedItemstack.StackSize;
            int batchCount = strippedInput.StackSize / batchSize;

            foreach (GridRecipeIngredient ingredient in matchingRecipe.resolvedIngredients)
            {
                if (ingredient == null || ingredient.IsTool)
                {
                    continue;
                }

                if (ingredient.ResolvedItemstack != null)
                {
                    if (ingredient.ResolvedItemstack.Collectible.Code.Path.Contains("schematic"))
                    {
                        continue;
                    }

                    ItemStack clone = ingredient.ResolvedItemstack.Clone();
                    clone.StackSize *= batchCount;
                    resultItems.Add(clone);
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
                            ItemStack clone = resolvedIngredient.ResolvedItemstack.Clone();
                            clone.StackSize *= batchCount;
                            resultItems.Add(clone);
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

        private bool IsRepairableAndDamaged(ItemStack stack)
        {
            int maxDurability = stack.Collectible.GetMaxDurability(stack);
            int currentDurability = stack.Collectible.GetRemainingDurability(stack);

            if (maxDurability > 0 && currentDurability < maxDurability)
            {
                return true;
            }

            float condition = stack.Attributes.GetFloat("condition", -1);
            if (condition >= 0 && condition < 1f)
            {
                return true;
            }

            return false;
        }

        private ItemStack RepairItem(ItemStack stack)
        {
            ItemStack repaired = stack.Clone();

            if (repaired.Collectible.GetMaxDurability(repaired) > 0)
            {
                repaired.Collectible.SetDurability(repaired, repaired.Collectible.GetMaxDurability(repaired));
            }

            if (repaired.Attributes.HasAttribute("condition"))
            {
                repaired.Attributes.SetFloat("condition", 1f);
            }

            return repaired;
        }

        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer, () =>
                {
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
            if (isPreviewActive)
            {
                CancelPreview();
            }
            base.OnBlockRemoved();
            clientDialog?.TryClose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (isPreviewActive)
            {
                CancelPreview();
            }
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

        private void UnlockAllOutputSlots()
        {
            for (int i = 2; i < inventory.Count; i++)
            {
                if (inventory[i] is ItemSlotPreviewable previewableSlot)
                {
                    previewableSlot.canTake = true;
                }
            }
        }

        private void LockAllOutputSlots()
        {
            for (int i = 2; i < inventory.Count; i++)
            {
                if (inventory[i] is ItemSlotPreviewable previewableSlot)
                {
                    previewableSlot.canTake = false;
                }
            }
        }

        private ItemStack StripAttributes(ItemStack stack)
        {
            ItemStack itemClone = stack.Clone();
            itemClone.Attributes = new TreeAttribute();
            return itemClone;
        }
        #endregion
    }
}
