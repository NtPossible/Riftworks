using Cairo;
using Riftworks.src.BE;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Riftworks.src.GUI
{
    public class GuiDialogBlockEntityTemporalDisassembler : GuiDialogBlockEntity
    {
        private const string BarrelContentsItemsLangKey = "barrelcontents-items";

        long lastRedrawMs;

        public GuiDialogBlockEntityTemporalDisassembler(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi)
            : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(Inventory);

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            // Direct call can cause InvalidOperationException
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setuptemporaldisassemblerdlg");
        }

        void SetupDialog()
        {

            ItemSlot? hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;

            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            ElementBounds? temporalDisassemblerBounds = ElementBounds.Fixed(0, 0, 610, 200);

            ElementBounds? inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
            ElementBounds? gearSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 50, 30, 1, 1);
            ElementBounds? outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 203, 30, 9, 1);
            ElementBounds? previewInputTextBounds = ElementBounds.Fixed(0, 90, 190, 120);
            ElementBounds? previewOutputTextBounds = ElementBounds.Fixed(203, 90, 360, 180);

            // 2. Around all that is 10 pixel padding
            ElementBounds? bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(temporalDisassemblerBounds);

            // 3. Finally Dialog
            ElementBounds? dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            ClearComposers();
            SingleComposer = capi.Gui
                .CreateCompo("blockentitytemporaldisassembler" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(temporalDisassemblerBounds, OnBgDraw, "symbolDrawer")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, inputSlotBounds, "inputSlot")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 1 }, gearSlotBounds, "gearSlot")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 9, new int[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 }, outputSlotBounds, "outputSlot")
                    .AddDynamicText(GetInputContentsText(), CairoFont.WhiteDetailText(), previewInputTextBounds, "previewInputText")
                    .AddDynamicText(GetOutputContentsText(), CairoFont.WhiteDetailText(), previewOutputTextBounds, "previewOutputText")
                .EndChildElements()
                .Compose();

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }
        }

        public void UpdateContents()
        {
            SingleComposer.GetDynamicText("previewInputText").SetNewText(GetInputContentsText());
            SingleComposer.GetDynamicText("previewOutputText").SetNewText(GetOutputContentsText());

        }

        string GetInputContentsText()
        {
            StringBuilder contents = new(Lang.Get("Contents:"));

            ItemSlot? input = Inventory[0];
            ItemSlot? gear = Inventory[1];

            // Nothing in the machine
            if (input.Empty && gear.Empty)
            {
                contents.AppendLine();
                contents.Append(Lang.Get("nobarrelcontents"));
                return contents.ToString();
            }

            // Input first
            if (!input.Empty)
            {
                contents.AppendLine();
                contents.Append(Lang.Get(BarrelContentsItemsLangKey, input.Itemstack.StackSize, input.Itemstack.GetName()));
            }

            // Gear requirement
            if (!gear.Empty)
            {
                contents.AppendLine();
                contents.Append(Lang.Get(BarrelContentsItemsLangKey, gear.Itemstack.StackSize, gear.Itemstack.GetName()));
            }
            else
            {
                contents.AppendLine();
                contents.Append(Lang.Get("Requires Temporal Gear"));
            }

            return contents.ToString();
        }

        string GetOutputContentsText()
        {
            // Aggregate currently present items in the output slots
            Dictionary<string, int> outputTotals = new();
            for (int i = 2; i < Inventory.Count; i++)
            {
                ItemSlot? outputSlot = Inventory[i];
                if (outputSlot.Empty)
                {
                    continue;
                }

                string itemName = outputSlot.Itemstack.GetName();
                int stackSize = outputSlot.Itemstack.StackSize;

                outputTotals.TryGetValue(itemName, out int existingCount);
                outputTotals[itemName] = existingCount + stackSize;
            }

            // If there are actual outputs, list them
            if (outputTotals.Count > 0)
            {
                StringBuilder contents = new(Lang.Get("Outputs:"));
                foreach (KeyValuePair<string, int> pair in outputTotals)
                {
                    contents.AppendLine();
                    contents.Append(Lang.Get(BarrelContentsItemsLangKey, pair.Value, pair.Key));
                }
                return contents.ToString();
            }

            // No actual outputs: if there's an input, try to show predicted outputs
            ItemSlot? inputSlot = Inventory[0];
            if (!inputSlot.Empty)
            {
                BlockEntityTemporalDisassembler? blockEntity = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityTemporalDisassembler;
                List<ItemStack>? predictedOutputs = blockEntity?.GetPredictedOutputs();

                if (predictedOutputs != null && predictedOutputs.Count > 0)
                {
                    // Aggregate predicted outputs by name
                    Dictionary<string, int> predictedTotals = new();
                    foreach (ItemStack predictedStack in predictedOutputs)
                    {
                        string predictedName = predictedStack.GetName();
                        int predictedSize = predictedStack.StackSize;

                        predictedTotals.TryGetValue(predictedName, out int existingCount);
                        predictedTotals[predictedName] = existingCount + predictedSize;
                    }

                    StringBuilder contents = new(Lang.Get("Will output:"));
                    foreach (KeyValuePair<string, int> pair in predictedTotals)
                    {
                        contents.AppendLine();
                        contents.Append(Lang.Get(BarrelContentsItemsLangKey, pair.Value, pair.Key));
                    }
                    return contents.ToString();
                }
            }

            // Fallback when nothing to show
            return Lang.Get("Outputs:");
        }

        float disassemblyTime;
        float maxDisassemblyTime;
        public void Update(float disassemblyTime, float maxDisassemblyTime)
        {
            this.disassemblyTime = disassemblyTime;
            this.maxDisassemblyTime = maxDisassemblyTime;

            if (!IsOpened())
            {
                return;
            }
            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                SingleComposer?.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = 30;

            ctx.Save();
            Matrix? m = ctx.Matrix;
            m.Translate(GuiElement.scaled(110), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = disassemblyTime / maxDisassemblyTime;

            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();

            LinearGradient? gradient = new(0, 0, GuiElement.scaled(200), 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));  // Dark green at the start.
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));  // Lighter green at the end.
            ctx.SetSource(gradient);

            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void SendInvPacket(object p)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, p);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("gearSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputSlot").OnGuiClosed(capi);

            base.OnGuiClosed();
        }
    }
}
