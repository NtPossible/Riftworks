using Riftworks.src.Items.Wearable;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    public class ModSystemVectorStasisUnit : ModSystemWearableTick<ItemVectorStasisUnit>
    {
        ICoreServerAPI? sapi;
        private int projectileTickListenerId = -1;
        private readonly HashSet<IPlayer> activeWearers = new();
        private readonly HashSet<long> frozenEntities = new();

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
        }

        protected override void HandleItem(IPlayer player, ItemVectorStasisUnit stasisUnit, ItemSlot slot, double hoursPassed, float dt)
        {
            double fuelBefore = FuelWearable.GetFuelHours(slot.Itemstack);
            double fuelAfter = fuelBefore;

            if (hoursPassed > 0)
            {
                FuelWearable.AddFuelHours(slot.Itemstack, -hoursPassed);
                fuelAfter = FuelWearable.GetFuelHours(slot.Itemstack);

                if (System.Math.Abs(fuelAfter - fuelBefore) >= 0.02)
                {
                    slot.MarkDirty();
                }
            }

            if (fuelAfter > 0)
            {
                if (activeWearers.Add(player) && projectileTickListenerId < 0 && sapi != null)
                {
                    projectileTickListenerId = (int)sapi.Event.RegisterGameTickListener(OnProjectileTick, 5);
                }
            }
            else
            {
                if (activeWearers.Remove(player) && activeWearers.Count == 0 && projectileTickListenerId >= 0)
                {
                    sapi?.Event.UnregisterGameTickListener(projectileTickListenerId);
                    projectileTickListenerId = -1;
                    frozenEntities.Clear();
                }
            }
        }

        protected override void HandleMissing(IPlayer player)
        {
            if (activeWearers.Remove(player) && activeWearers.Count == 0 && projectileTickListenerId >= 0)
            {
                sapi?.Event.UnregisterGameTickListener(projectileTickListenerId);
                projectileTickListenerId = -1;
                frozenEntities.Clear();
            }
        }

        private void OnProjectileTick(float dt)
        {
            frozenEntities.RemoveWhere(entityId => sapi?.World.GetEntityById(entityId) == null);

            foreach (IPlayer player in activeWearers)
            {
                Entity playerEntity = player.Entity;
                Vec3d playerPos = playerEntity.ServerPos.XYZ;

                Entity[]? entities = sapi?.World.GetEntitiesAround(playerPos, 29, 29);
                if (entities == null)
                {
                    continue;
                }
                foreach (Entity entity in entities)
                {
                    if (frozenEntities.Contains(entity.EntityId))
                    {
                        continue;
                    }
                    if (!IsProjectileLike(entity, playerEntity))
                    {
                        continue;
                    }
                    Vec3d projectedPos = entity.ServerPos.XYZ + entity.ServerPos.Motion * dt;
                    if (playerPos.DistanceTo(projectedPos) <= 4)
                    {
                        FreezeProjectile(entity);
                    }
                }
            }
        }


        private static bool IsProjectileLike(Entity entity, Entity playerEntity)
        {
            if (entity is EntityProjectile projectile)
            {
                if (projectile.Collided)
                {
                    return false;
                }
                // Ignore player's own shots
                if (projectile.FiredBy?.EntityId == playerEntity.EntityId)
                {
                    return false;
                }
                return true;
            }

            if (entity is EntityAgent)
            {
                return false;
            }
            if (entity is EntityItem)
            {
                return false;
            }

            if (entity.ServerPos.Motion.LengthSq() < 0.05)
            {
                return false;
            }

            if (entity.OnGround)
            {
                return false;
            }
            return true;
        }

        private void FreezeProjectile(Entity entity)
        {
            // Hashset to prevent duplicate processing
            if (!frozenEntities.Add(entity.EntityId))
            {
                return;
            }

            entity.ServerPos.Motion.Set(0, 0, 0);
            entity.Pos.SetPos(entity.ServerPos);

            entity.ServerPos.SetPos(entity.Pos);
            entity.Pos.SetPos(entity.ServerPos);
        }
    }
}