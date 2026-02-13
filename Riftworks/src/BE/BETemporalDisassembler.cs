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

        GuiDialogBlockEntityTemporalDisassembler? clientDialog;

        private float disassemblyTime = 0;
        private const float maxDisassemblyTime = 60f;

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
                clientDialog?.UpdateContents();
                clientDialog?.Update(disassemblyTime, maxDisassemblyTime);
            }
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

            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        private GridRecipe? GetGridRecipe(ItemStack inputStack)
        {
            // Filter all grid‐recipes whose output type matches and exact variant and whose batch size cleanly divides into our input stack size
            IEnumerable<GridRecipe> filteredRecipes = Api.World.GridRecipes
                .Where(recipe =>
                {
                    ItemStack? outputStack = recipe.Output?.ResolvedItemstack;
                    return outputStack != null
                        && MatchesExactVariant(outputStack, inputStack)
                        && inputStack.StackSize >= outputStack.StackSize
                        && inputStack.StackSize % outputStack.StackSize == 0;
                });

            // If multiple recipes exist, prefer the one with the largest output stack
            GridRecipe? matchingRecipe = filteredRecipes?.OrderByDescending(recipe => recipe.Output?.ResolvedItemstack.StackSize).FirstOrDefault();

            return matchingRecipe;
        }

        private int CalculateItemsToConsume(ItemStack inputStack)
        {
            // Find a recipe whose output type matches and whose output stack size and cleanly divides into the input stack size.
            GridRecipe? matchingRecipe = GetGridRecipe(inputStack);

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

                ItemSlot? emptySlot = outputSlots?.FirstOrDefault(slot => slot.Empty);
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

            GridRecipe? matchingRecipe = GetGridRecipe(inputItem);

            if (matchingRecipe == null)
            {
                return new List<ItemStack> { inputItem };
            }

            List<ItemStack> resultItems = new();
            int batchSize = matchingRecipe.Output.ResolvedItemstack.StackSize;
            int batchCount = inputItem.StackSize / batchSize;

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
                    string? resolvedVariant = GetPreferredOrFirstWildcard(ingredient);

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

        private string? GetPreferredOrFirstWildcard(CraftingRecipeIngredient ingredient)
        {
            string basePath = ingredient.Code.Path.Replace("*", "");

            // First check the preferred variant dictionary
            if (PreferredWildcards.TryGetValue(basePath, out string? preferredVariant))
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

        private static bool IsRepairableAndDamaged(ItemStack stack)
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

        private static ItemStack RepairItem(ItemStack stack)
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

        public List<ItemStack> GetPredictedOutputs()
        {
            if (InputSlot.Empty)
            {
                return new List<ItemStack>();
            }

            int toConsume = CalculateItemsToConsume(InputSlot.Itemstack);
            if (toConsume <= 0)
            {
                return new List<ItemStack>();
            }

            ItemStack temp = InputSlot.Itemstack.Clone();
            temp.StackSize = toConsume;

            return Disassemble(temp);
        }

        // Checks whether an input stack matches the recipe’s output by requiring the same collectible and the same variant-defining attributes(ignores any extra attributes on the input).
        private static bool MatchesExactVariant(ItemStack recipeOutputStack, ItemStack inputStack)
        {
            if (recipeOutputStack?.Collectible != inputStack?.Collectible)
            {
                return false;
            }

            // If the recipe output has no attributes it matches
            if (recipeOutputStack?.Attributes is not TreeAttribute recipeAttributes || recipeAttributes.Count == 0)
            {
                return true;
            }

            // If the input has no attributes but the recipe does, no match
            if (inputStack?.Attributes is not TreeAttribute inputAttributes)
            {
                return false;
            }
            // If there are attributes then compare the attribute keys that exist on the recipe output
            foreach (string attributeKey in recipeAttributes.Keys)
            {
                IAttribute recipeValue = recipeAttributes[attributeKey];
                IAttribute inputValue = inputAttributes[attributeKey];

                if (inputValue == null)
                {
                    return false;
                }

                // Compare via JSON token to handle numbers/strings/arrays/trees uniformly
                if (!recipeValue.ToJsonToken().Equals(inputValue.ToJsonToken())) { 
                    return false;
                }
            }

            return true;
        }

        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api is ICoreClientAPI capi)
            {
                toggleInventoryDialogClient(byPlayer, () =>
                {
                    clientDialog = new GuiDialogBlockEntityTemporalDisassembler(DialogTitle, Inventory, Pos, capi);
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

        public override void OnBlockBroken(IPlayer? byPlayer = null)
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
