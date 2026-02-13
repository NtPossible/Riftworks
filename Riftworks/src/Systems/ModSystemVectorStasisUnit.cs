using Riftworks.src.Items.Wearable;
using System.Collections.Generic;
using System.Linq;
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
            // If this is the first time this player got handled, start listening
            if (activeWearers.Add(player) && projectileTickListenerId < 0 && sapi != null)
            {
                projectileTickListenerId = (int)sapi.Event.RegisterGameTickListener(OnProjectileTick, 5);
            }

            double fuelBefore = FuelWearable.GetFuelHours(slot.Itemstack);

            if (hoursPassed > 0)
            {
                FuelWearable.AddFuelHours(slot.Itemstack, -hoursPassed);

                double fuelAfter = FuelWearable.GetFuelHours(slot.Itemstack);

                if (System.Math.Abs(fuelAfter - fuelBefore) >= 0.02)
                {
                    slot.MarkDirty();
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
                Vec3d playerPos = player.Entity.ServerPos.XYZ;

                IEnumerable<EntityProjectile>? projectiles = sapi?.World.GetEntitiesAround(playerPos, 29, 29).OfType<EntityProjectile>()
                        .Where(projectile => !projectile.Collided && !frozenEntities.Contains(projectile.EntityId));

                foreach (EntityProjectile projectile in projectiles)
                {
                    Vec3d projectedPos = projectile.ServerPos.XYZ + projectile.ServerPos.Motion * dt;
                    if (playerPos.DistanceTo(projectedPos) <= 4)
                    {
                        FreezeProjectile(projectile);
                    }
                }
            }
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