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
        private readonly Dictionary<long, Entity> trackedProjectiles = new();

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
            api.Event.OnEntitySpawn += OnEntitySpawn;
            api.Event.OnEntityDespawn += OnEntityDespawn;
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
                    trackedProjectiles.Clear();
                }
            }
        }

        protected override void HandleMissing(IPlayer player)
        {
            if (activeWearers.Remove(player) && activeWearers.Count == 0 && projectileTickListenerId >= 0)
            {
                sapi?.Event.UnregisterGameTickListener(projectileTickListenerId);
                projectileTickListenerId = -1;
                trackedProjectiles.Clear();
            }
        }

        private void OnEntitySpawn(Entity entity)
        {
            if (IsTrackableProjectile(entity))
            {
                trackedProjectiles[entity.EntityId] = entity;
            }
        }

        private void OnEntityDespawn(Entity entity, EntityDespawnData data)
        {
            trackedProjectiles.Remove(entity.EntityId);
        }

        private void OnProjectileTick(float dt)
        {
            List<long> toRemove = new();
            foreach (KeyValuePair<long, Entity> kvp in trackedProjectiles)
            {
                if (kvp.Value == null || !kvp.Value.Alive)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (long id in toRemove)
            {
                trackedProjectiles.Remove(id);
            }

            foreach (IPlayer player in activeWearers)
            {
                Entity playerEntity = player.Entity;
                Vec3d playerPos = playerEntity.ServerPos.XYZ;

                List<long> frozenThisTick = new();

                foreach (KeyValuePair<long, Entity> kvp in trackedProjectiles)
                {
                    Entity entity = kvp.Value;

                    if (!IsProjectile(entity, playerEntity))
                    {
                        continue;
                    }

                    // Predict next position
                    EntityPos pos = entity.ServerPos;
                    double px = pos.X + pos.Motion.X * dt;
                    double py = pos.Y + pos.Motion.Y * dt;
                    double pz = pos.Z + pos.Motion.Z * dt;

                    double dx = playerPos.X - px;
                    double dy = playerPos.Y - py;
                    double dz = playerPos.Z - pz;

                    if (dx * dx + dy * dy + dz * dz <= 16)
                    {
                        FreezeProjectile(entity);
                        frozenThisTick.Add(entity.EntityId);
                    }
                }

                // Remove frozen projectiles from tracking
                foreach (long id in frozenThisTick)
                {
                    trackedProjectiles.Remove(id);
                }
            }
        }

        // to check if the spawned entity is an actual projectile
        private static bool IsTrackableProjectile(Entity entity)
        {
            if (entity is EntityProjectile)
            {
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
            if (entity.ServerPos.Motion.LengthSq() > 0.05 && !entity.OnGround)
            {
                return true;
            }
            return false;
        }

        // to check if the entity is a projectile that should be frozen
        private static bool IsProjectile(Entity entity, Entity playerEntity)
        {
            if (entity is EntityProjectile projectile)
            {
                if (projectile.Collided)
                {
                    return false;
                }
                // Ignore player's own projectiles
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

        private static void FreezeProjectile(Entity entity)
        {
            entity.ServerPos.Motion.Set(0, 0, 0);
            entity.Pos.SetPos(entity.ServerPos);

            entity.ServerPos.SetPos(entity.Pos);
            entity.Pos.SetPos(entity.ServerPos);
        }
    }
}