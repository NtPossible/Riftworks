using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Collections.Generic;
using System.Linq;
using Riftworks.src.Items.Wearable;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;

namespace Riftworks.src.Systems
{
    public class ModSystemVectorStasisUnit : ModSystemWearableTick<ItemVectorStasisUnit>
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;
        private HashSet<long> frozenEntities = new();

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
            api.Event.RegisterGameTickListener(OnProjectileTick, 5);
        }

        protected override EnumCharacterDressType Slot => EnumCharacterDressType.Arm;

        protected override void HandleItem(IPlayer plr, ItemVectorStasisUnit stasisUnit, ItemSlot armSlot, double hoursPassed, float dt)
        {
            if (hoursPassed > 0.05)
            {
                stasisUnit.AddFuelHours(armSlot.Itemstack, -hoursPassed);
                armSlot.MarkDirty();
            }
        }

        private void OnProjectileTick(float dt)
        {
            frozenEntities.RemoveWhere(entityId => sapi.World.GetEntityById(entityId) == null);

            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                IInventory inv = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (inv == null)
                {
                    continue;
                }

                ItemSlot armSlot = inv[(int)EnumCharacterDressType.Arm];
                ItemStack stack = armSlot?.Itemstack;
                
                if (stack?.Collectible is ItemVectorStasisUnit itemStasis && itemStasis.GetFuelHours(stack) > 0)
                {
                    Vec3d playerPos = player.Entity.ServerPos.XYZ;
                    IEnumerable<Entity> entities = sapi.World.GetEntitiesAround(playerPos, 29, 29)
                        .Where(e => (e is EntityProjectile proj && !proj.Collided && !frozenEntities.Contains(e.EntityId)) 
                        || (e.ServerPos.Motion.Length() > 0.05 && !frozenEntities.Contains(e.EntityId)));

                    foreach (Entity entity in entities)
                    {
                        Vec3d projectedPos = entity.ServerPos.XYZ + entity.ServerPos.Motion * dt;
                        if (playerPos.DistanceTo(projectedPos) <= 4)
                        {
                            FreezeProjectile(entity);
                        }
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

        private void Event_LevelFinalize()
        {
            bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        }
    }
}
