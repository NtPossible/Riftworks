using Riftworks.src.EntityClasses.Behavior;
using Riftworks.src.Items.Wearable;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.Systems
{
    public class AdaptiveReconstitutionSystem : ModSystemWearableTick<ItemAdaptiveReconstitutionGear>
    {
        ICoreClientAPI? capi;
        ICoreServerAPI? sapi;
        EntityBehaviorPlayerInventory? bh;

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
            api.Event.OnEntityDeath += OnEntityDeath;
        }

        protected override void HandleItem(IPlayer player, ItemAdaptiveReconstitutionGear reconstitutionGear, ItemSlot slot, double hoursPassed, float dt)
        {
            EntityPlayer playerEntity = player.Entity;
            reconstitutionGear.UpdateAdaptation(dt, slot);

            if (playerEntity != null && playerEntity.Api != null && !playerEntity.HasBehavior<EntityBehaviorAdaptiveResistance>())
            {
                playerEntity.AddBehavior(new EntityBehaviorAdaptiveResistance(playerEntity));
            }

            // passive regen
            if (playerEntity != null && playerEntity.Alive)
            {
                EntityBehaviorHealth? entityBehaviorHealth = playerEntity.GetBehavior<EntityBehaviorHealth>();
                if (entityBehaviorHealth != null)
                {
                    if (entityBehaviorHealth.Health < entityBehaviorHealth.MaxHealth)
                    {
                        if (!entityBehaviorHealth.ActiveDoTEffects.Any(effect => effect.DamageType == EnumDamageType.Heal))
                        {
                            entityBehaviorHealth.ApplyDoTEffect(EnumDamageSource.Internal, EnumDamageType.Heal, 10, 50, TimeSpan.FromSeconds(10), 35, 0);
                        }
                    }
                }
            }
            slot.MarkDirty();
        }
            
        protected override void HandleMissing(IPlayer player)
        {
            EntityPlayer playerEntity = player.Entity;
            if (playerEntity != null && playerEntity.Api != null && playerEntity.HasBehavior<EntityBehaviorAdaptiveResistance>())
            {
                playerEntity.RemoveBehavior(playerEntity.GetBehavior<EntityBehaviorAdaptiveResistance>());
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (entity is not EntityPlayer)
            {
                return;
            }

            EntityPlayer? player = entity as EntityPlayer;
            IInventory? inventory = player?.Player?.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

            if (inventory == null)
            {
                return;
            }

            ItemSlot? gearSlot = inventory.FirstOrDefault(s => s?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear);
            if (gearSlot?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear)
            {
                ItemAdaptiveReconstitutionGear.ResetResistances(gearSlot);
                ItemStack stackToDrop = gearSlot.TakeOut(1);

                if (stackToDrop != null)
                {
                    sapi?.World?.SpawnItemEntity(stackToDrop, player?.Pos?.XYZ);
                }
                gearSlot.MarkDirty();

                if (player != null && player.HasBehavior<EntityBehaviorAdaptiveResistance>())
                {
                    EntityBehaviorAdaptiveResistance? behavior = player.GetBehavior<EntityBehaviorAdaptiveResistance>();
                    player.RemoveBehavior(behavior);
                }
            }
        }

        private void Event_LevelFinalize()
        {
            EntityBehaviorPlayerInventory? behavior = capi?.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh = behavior;
        }
    }
}