using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using Riftworks.src.Items.Wearable;
using Riftworks.src.EntityClasses.Behavior;
using Riftworks.src.Entities;

namespace Riftworks.src.Systems
{
    public class AdaptiveReconstitutionSystem : ModSystemWearableTick<ItemAdaptiveReconstitutionGear>
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;
        private Dictionary<string, EntityAdaptiveReconstitutionGear> playerGearEntities = new();

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

        protected override void HandleItem(IPlayer plr, ItemAdaptiveReconstitutionGear reconstitutionGear, ItemSlot slot, double hoursPassed, float dt)
        {
            EntityPlayer playerEntity = plr.Entity;
            reconstitutionGear.UpdateAdaptation(dt, slot);
            if (playerEntity != null)
            {
                //if (!playerGearEntities.ContainsKey(player.PlayerUID))
                //{
                        //    EntityProperties entityType = sapi.World.GetEntityType(new AssetLocation("riftworks:entityadaptivereconstitutiongear"));
                        //    EntityAdaptiveReconstitutionGear gearEntity = sapi.World.ClassRegistry.CreateEntity(entityType) as EntityAdaptiveReconstitutionGear;

                //    gearEntity.owner = playerEntity;
                //    playerGearEntities[player.PlayerUID] = gearEntity;
                        //    sapi.World.SpawnEntity(gearEntity);
                //}

                try
                {
                    if (!playerEntity.HasBehavior<EntityBehaviorAdaptiveResistance>())
                    {
                        playerEntity.AddBehavior(new EntityBehaviorAdaptiveResistance(playerEntity));
                    }
                }
                catch (NullReferenceException)
                {
                    sapi.World.Logger.Error("Error creating EntityAdaptiveReconstitutionGear for player: " + plr.PlayerUID);
                }
            }
            slot.MarkDirty();
        }

        protected override void HandleMissing(IPlayer player)
        {
            EntityPlayer playerEntity = player.Entity;
            if (playerEntity != null)
            {
                try
                {
                    if (playerEntity.HasBehavior<EntityBehaviorAdaptiveResistance>())
                    {
                        playerEntity.RemoveBehavior(playerEntity.GetBehavior<EntityBehaviorAdaptiveResistance>());
                    }
                }
                catch (NullReferenceException)
                {
                    sapi.World.Logger.Error("Error removing EntityBehaviorAdaptiveResistance from player: " + player.PlayerUID);
                }

                //if (playerGearEntities.TryGetValue(player.PlayerUID, out EntityAdaptiveReconstitutionGear value))
                //{
                        //    Entity gearEntity = value;
                        //    gearEntity.Die();
                //    playerGearEntities.Remove(player.PlayerUID);
                //}
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (entity is not EntityPlayer)
            {
                return;
            }

            EntityPlayer player = entity as EntityPlayer;
            IInventory inventory = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

            if (inventory == null)
            {
                return;
            }

            ItemSlot headSlot = inventory[(int)EnumCharacterDressType.ArmorHead];
            if (headSlot?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear gear)
            {
                gear.ResetResistances(headSlot);
                ItemStack stackToDrop = headSlot.TakeOut(1);

                if (stackToDrop != null)
                {
                    sapi.World.SpawnItemEntity(stackToDrop, player.Pos.XYZ);
                }
                headSlot.MarkDirty();

                //if (playerGearEntities.TryGetValue(player.PlayerUID, out EntityAdaptiveReconstitutionGear value))
                //{
                //    Entity gearEntity = value;
                //    gearEntity.Die();
                //    playerGearEntities.Remove(player.PlayerUID);
                //}

                if (player.HasBehavior<EntityBehaviorAdaptiveResistance>())
                {
                    EntityBehaviorAdaptiveResistance behavior = player.GetBehavior<EntityBehaviorAdaptiveResistance>();
                    player.RemoveBehavior(behavior);
                }
            }
        }

        private void Event_LevelFinalize()
        {
            EntityBehaviorPlayerInventory behavior = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
            bh = behavior;
        }
    }
}
