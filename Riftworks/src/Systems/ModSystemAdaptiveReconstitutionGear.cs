using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using Riftworks.src.Items.Wearable;
using Riftworks.src.EntityClasses.Behavior;
using Riftworks.src.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

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

        protected override EnumCharacterDressType Slot => EnumCharacterDressType.ArmorHead;


        protected override void HandleItem(IPlayer plr, ItemAdaptiveReconstitutionGear reconstitutionGear, ItemSlot headArmorSlot, double hoursPassed, float dt)
        {
            EntityPlayer playerEntity = plr.Entity;
            reconstitutionGear.UpdateAdaptation(dt, headArmorSlot);
            if (playerEntity != null)
            {
                //if (!playerGearEntities.ContainsKey(plr.PlayerUID))
                //{
                        //    EntityProperties entityType = sapi.World.GetEntityType(new AssetLocation("riftworks:entityadaptivereconstitutiongear"));
                        //    EntityAdaptiveReconstitutionGear gearEntity = sapi.World.ClassRegistry.CreateEntity(entityType) as EntityAdaptiveReconstitutionGear;

                //    gearEntity.owner = playerEntity;
                //    playerGearEntities[plr.PlayerUID] = gearEntity;
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
            headArmorSlot.MarkDirty();
        }

        protected override void HandleMissing(IPlayer plr, EnumCharacterDressType slot)
        {
            EntityPlayer playerEntity = plr.Entity;
            if (playerEntity != null)
            {
                try
                {
                    if (playerEntity.HasBehavior<EntityBehaviorAdaptiveResistance>())
                    {
                        EntityBehaviorAdaptiveResistance behavior = playerEntity.GetBehavior<EntityBehaviorAdaptiveResistance>();
                        playerEntity.RemoveBehavior(behavior);
                    }
                }
                catch (NullReferenceException)
                {
                    sapi.World.Logger.Error("Error removing EntityBehaviorAdaptiveResistance from player: " + plr.PlayerUID);
                }

                //if (playerGearEntities.TryGetValue(plr.PlayerUID, out EntityAdaptiveReconstitutionGear value))
                //{
                        //    Entity gearEntity = value;
                        //    gearEntity.Die();
                //    playerGearEntities.Remove(plr.PlayerUID);
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
            IInventory inv = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

            if (inv == null)
            {
                return;
            }

            ItemSlot headSlot = inv[(int)EnumCharacterDressType.ArmorHead];
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
