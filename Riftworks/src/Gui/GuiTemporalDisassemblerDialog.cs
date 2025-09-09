using Cairo;
using Riftworks.src.Inventory;
using Riftworks.src.BE;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Riftworks.src.GUI
{
    public class GuiDialogBlockEntityTemporalDisassembler : GuiDialogBlockEntity
    {
        long lastRedrawMs;

        public GuiDialogBlockEntityTemporalDisassembler(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi)
            : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (IsDuplicate) return;

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

            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;

            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            ElementBounds temporalDisassemblerBounds = ElementBounds.Fixed(0, 0, 610, 200);

            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
            ElementBounds gearSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 50, 30, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 203, 30, 9, 1);
            ElementBounds previewInputTextBounds = ElementBounds.Fixed(0, 90, 190, 120);
            ElementBounds previewOutputTextBounds = ElementBounds.Fixed(203, 90, 360, 180);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(temporalDisassemblerBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
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
                    .AddDynamicText(getInputContentsText(), CairoFont.WhiteDetailText(), previewInputTextBounds, "previewInputText")
                    .AddDynamicText(getOutputContentsText(), CairoFont.WhiteDetailText(), previewOutputTextBounds, "previewOutputText")
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
            SingleComposer.GetDynamicText("previewInputText").SetNewText(getInputContentsText());
            SingleComposer.GetDynamicText("previewOutputText").SetNewText(getOutputContentsText());

        }

        string getInputContentsText()
        {
            string contents = Lang.Get("Contents:");

            ItemSlot input = Inventory[0];
            ItemSlot gear = Inventory[1];

            // Nothing in the machine
            if (input.Empty && gear.Empty)
            {
                contents += "\n" + Lang.Get("nobarrelcontents");
                return contents;
            }

            // Input first (nice for quick glance)
            if (!input.Empty)
            {
                contents += "\n" + Lang.Get("barrelcontents-items", input.Itemstack.StackSize, input.Itemstack.GetName());
            }

            // Gear requirement
            if (!gear.Empty)
            {
                contents += "\n" + Lang.Get("barrelcontents-items", gear.Itemstack.StackSize, gear.Itemstack.GetName());
            }
            else
            {
                contents += "\n" + Lang.Get("Requires Temporal Gear");
            }

            return contents;
        }

        string getOutputContentsText()
        {
            // First: if we already have real items in output slots (2..10), show them
            Dictionary<string, int> totals = new();
            for (int i = 2; i < Inventory.Count; i++)
            {
                ItemSlot slot = Inventory[i];
                if (slot.Empty) continue;

                string name = slot.Itemstack.GetName();
                int size = slot.Itemstack.StackSize;
                totals[name] = (totals.TryGetValue(name, out var have) ? have : 0) + size;
            }

            if (totals.Count > 0)
            {
                string contents = Lang.Get("Outputs:");
                foreach (var kvp in totals)
                {
                    contents += "\n" + Lang.Get("barrelcontents-items", kvp.Value, kvp.Key);
                }
                return contents;
            }

            // Otherwise: show a text-only prediction whenever the input slot is filled (no gear required)
            ItemSlot input = Inventory[0];
            if (!input.Empty)
            {
                var be = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityTemporalDisassembler;
                var predicted = be?.GetPredictedOutputs(); // non-destructive preview from the client-side BE

                if (predicted != null && predicted.Count > 0)
                {
                    var pTotals = new Dictionary<string, int>();
                    foreach (var st in predicted)
                    {
                        string name = st.GetName();
                        pTotals[name] = (pTotals.TryGetValue(name, out var have) ? have : 0) + st.StackSize;
                    }

                    string contents = Lang.Get("Will output:");
                    foreach (var kvp in pTotals)
                    {
                        contents += "\n" + Lang.Get("barrelcontents-items", kvp.Value, kvp.Key);
                    }
                    return contents;
                }
            }

            // Fallback
            return Lang.Get("Outputs:");
        }

        float disassemblyTime;
        float maxDisassemblyTime;
        public void Update(float disassemblyTime, float maxDisassemblyTime)
        {
            this.disassemblyTime = disassemblyTime;
            this.maxDisassemblyTime = maxDisassemblyTime;

            if (!IsOpened()) return;

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
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(110), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = disassemblyTime / maxDisassemblyTime;

            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();

            LinearGradient gradient = new(0, 0, GuiElement.scaled(200), 0);
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
