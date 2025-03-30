using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    class ModSystemVectorStasisUnit : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;
        private HashSet<long> frozenEntities = new HashSet<long>();

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.RegisterGameTickListener(OnTickServer1s, 1000, 200);
            api.Event.RegisterGameTickListener(OnProjectileTick, 5, 5); 

        }

        double lastCheckTotalHours;
        private void OnTickServer1s(float dt)
        {
            double totalHours = sapi.World.Calendar.TotalHours;
            double hoursPassed = totalHours - lastCheckTotalHours;

            if (hoursPassed > 0.05)
            {
                foreach (var plr in sapi.World.AllOnlinePlayers)
                {
                    var inv = plr.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                    if (inv == null) continue;

                    var armSlot = inv[(int)EnumCharacterDressType.Arm];
                    if (armSlot.Itemstack?.Collectible is ItemVectorStasisUnit stasisUnit)
                    {
                        stasisUnit.AddFuelHours(armSlot.Itemstack, -hoursPassed);
                        armSlot.MarkDirty();
                    }
                }

                lastCheckTotalHours = totalHours;
            }
        }

        // theres most likely a better way to do this
        private void OnProjectileTick(float dt)
        {
            frozenEntities.RemoveWhere(entityId => sapi.World.GetEntityById(entityId) == null);

            foreach (var plr in sapi.World.AllOnlinePlayers)
            {
                var inv = plr.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (inv == null) continue;

                var armSlot = inv[(int)EnumCharacterDressType.Arm];
                var stack = armSlot?.Itemstack;

                if (stack?.Collectible is ItemVectorStasisUnit itemStasis && itemStasis.GetFuelHours(stack) > 0)
                {
                    var playerPos = plr.Entity.ServerPos.XYZ;

                    var entities = sapi.World.GetEntitiesAround(playerPos, 29, 29)
                        .Where(e => e is EntityProjectile proj && !proj.Collided && !frozenEntities.Contains(e.EntityId)
                            || e.ServerPos.Motion.Length() > 0.05 && !frozenEntities.Contains(e.EntityId));

                    foreach (var entity in entities)
                    {
                        // Predict next tick position and then freeze if within 4 blocks
                        Vec3d projectedPos = entity.ServerPos.XYZ + entity.ServerPos.Motion * dt; 
                        double distanceToPlayer = playerPos.DistanceTo(projectedPos);

                        if (distanceToPlayer <= 4)  
                        {
                            FreezeProjectile(entity);
                        }
                    }
                }
            }
        }

        private void Event_LevelFinalize()
        {
            bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        }

        private void FreezeProjectile(Entity entity)
        {
            // Hashset to prevent duplicate processing
            if (!frozenEntities.Add(entity.EntityId)) return; 

            entity.ServerPos.Motion.Set(0, 0, 0);
            entity.Pos.SetPos(entity.ServerPos);

            entity.ServerPos.SetPos(entity.Pos);
            entity.Pos.SetPos(entity.ServerPos);
        }

    }

    public class ItemVectorStasisUnit : ItemWearable
    {
        protected float fuelHoursCapacity = 24;

        public double GetFuelHours(ItemStack stack)
        {
            return Math.Max(0, stack.Attributes.GetDecimal("fuelHours"));
        }
        public void SetFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", fuelHours);
        }
        public void AddFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", Math.Max(0, fuelHours + GetFuelHours(stack)));
        }

        public float GetStackFuel(ItemStack stack)
        {
            return stack.ItemAttributes?["nightVisionFuelHours"].AsFloat(0) ?? 0;
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(sourceStack);
                if (fuel == 0) return base.GetMergableQuantity(sinkStack, sourceStack, priority);
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(op.SourceSlot.Itemstack);
                double fuelHoursLeft = GetFuelHours(op.SinkSlot.Itemstack);
                if (fuel > 0 && fuelHoursLeft + fuel / 2 < fuelHoursCapacity)
                {
                    SetFuelHours(op.SinkSlot.Itemstack, fuel + fuelHoursLeft);
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    op.SinkSlot.MarkDirty();
                    return;
                }

                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "vectorstasisunitfull", Lang.Get("ingameerror-vectorstasisunit-full"));
                }
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            double fuelLeft = GetFuelHours(inSlot.Itemstack);
            dsc.AppendLine(Lang.Get("Has fuel for {0:0.#} hours", fuelLeft));
            if (fuelLeft <= 0)
            {
                dsc.AppendLine(Lang.Get("Add temporal gear to refuel"));
            }
        }

    }
}
