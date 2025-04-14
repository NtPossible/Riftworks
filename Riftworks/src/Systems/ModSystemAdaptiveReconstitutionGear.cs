using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;

namespace Riftworks.src.Systems
{
    internal class ModSystemAdaptiveReconstitutionGear : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;

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
            api.Event.OnEntityDeath += OnEntityDeath;
        }

        private void OnTickServer1s(float dt)
        {
            foreach (IPlayer plr in sapi.World.AllOnlinePlayers)
            {
                IInventory inv = plr.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (inv == null) continue;

                ItemSlot headArmorSlot = inv[(int)EnumCharacterDressType.ArmorHead];
                EntityPlayer playerEntity = plr.Entity;
                if (playerEntity == null || !playerEntity.Alive || playerEntity.WatchedAttributes == null) continue;

                if (headArmorSlot.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear reconstitutionGear)
                {
                    reconstitutionGear.UpdateAdaptation(dt, headArmorSlot);

                    if (playerEntity != null)
                    {
                        if (playerEntity.GetBehavior<EntityBehaviorAdaptiveResistance>() == null)
                        {
                            playerEntity.AddBehavior(new EntityBehaviorAdaptiveResistance(playerEntity));
                        }
                    }

                    headArmorSlot.MarkDirty();
                }
                else
                {
                    if (playerEntity != null)
                    {
                        EntityBehaviorAdaptiveResistance behavior = playerEntity.GetBehavior<EntityBehaviorAdaptiveResistance>();
                        if (behavior != null)
                        {
                            playerEntity.RemoveBehavior(behavior);
                        }
        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer player)) return;

            IInventory inv = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return;

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
                    }

                EntityBehaviorAdaptiveResistance behavior = player.GetBehavior<EntityBehaviorAdaptiveResistance>();
                if (behavior != null)
                {
                    player.RemoveBehavior(behavior);
                }
            }
        }

        private void Event_LevelFinalize()
        {
            bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        }
    }

    public class ItemAdaptiveReconstitutionGear : ItemWearable
    {
        private EnumDamageType? currentAdaptingDamageType = null;
        private float adaptationTimer = 0f;
        private const float ADAPTATION_DURATION = 60f; 
        private const float BASE_ADAPTATION_RATE = 0.002f * 10; // 0.2% per second
        private float adaptationSpeed = BASE_ADAPTATION_RATE;

        public void HandleDamageTaken(EnumDamageType damageType)
        {
            if (damageType == EnumDamageType.Heal) { return; } 

            // If its a different damage type switch adaptation and reset timer and speed
            if (currentAdaptingDamageType != damageType)
            {
                currentAdaptingDamageType = damageType;
                adaptationTimer = 0f;
                adaptationSpeed = BASE_ADAPTATION_RATE;
            }
            else
            {
                // If hit by the same type, reset timer & increase speed
                adaptationTimer = 0f;
                adaptationSpeed *= 1.05f;
            }
        }

        public void UpdateAdaptation(float dt, ItemSlot inSlot)
        {
            if (currentAdaptingDamageType == null) return;

            adaptationTimer += dt;
            if (adaptationTimer >= ADAPTATION_DURATION)
            {
                currentAdaptingDamageType = null;
                return;
            }

            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetOrAddTreeAttribute("resistances");
            float currentResistance = resistanceLevels.GetFloat(currentAdaptingDamageType.ToString(), 0f);

            if (currentResistance >= 1f)
            {
                currentAdaptingDamageType = null;
                return;
            }

            // Get previous and new resistance percentage (rounded to nearest 10%)
            int previousPercent = (int)(currentResistance * 10) * 10;
            float newResistance = Math.Min(currentResistance + adaptationSpeed * dt, 1f);
            int newPercent = (int)(newResistance * 10) * 10;

            resistanceLevels.SetFloat(currentAdaptingDamageType.ToString(), newResistance);

            // For playing the sound and animations (hopefully)
            if (newPercent > previousPercent)
            {
                IPlayer player = (inSlot.Inventory as InventoryCharacter)?.Player;
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    PlayGearSound(api.World, entityPlayer);
                }
            }

        }

        private void PlayGearSound(IWorldAccessor world, EntityPlayer player)
        {
            if (player == null) return;

            world.PlaySoundAt(
                new AssetLocation("riftworks:sounds/gearspin.ogg"),
                player.Pos.X, player.Pos.Y, player.Pos.Z,
                null,
                false, 
                32f, 
                1.0f
            );
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetTreeAttribute("resistances");

            if (resistanceLevels != null)
            {
                List<string> adaptedResistances = resistanceLevels
                    .Where(r => r.Value is FloatAttribute floatAttr && floatAttr.value > 0)
                    .Select(r => $"{Lang.Get(r.Key)}: {((FloatAttribute)r.Value).value * 100}%")
                    .ToList();

                if (adaptedResistances.Any())
                {
                    dsc.AppendLine("Adaptations:");
                    dsc.AppendLine(string.Join("\n", adaptedResistances));
                }
            }
        }

        // Currently unused
        public void ResetResistances(ItemSlot inSlot)
        {
            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetOrAddTreeAttribute("resistances");

            foreach (EnumDamageType damageType in Enum.GetValues(typeof(EnumDamageType)).Cast<EnumDamageType>())
            {
                resistanceLevels.SetFloat(damageType.ToString(), 0f);
            }
        }
    }
    public class EntityBehaviorAdaptiveResistance : EntityBehavior
    {
        public EntityBehaviorAdaptiveResistance(Entity entity) : base(entity)
        {
            EntityBehaviorHealth healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBehavior != null)
            {
                healthBehavior.onDamaged += ReduceDamage;
            }
        }

        private float ReduceDamage(float damage, DamageSource damageSource)
        {
            if (entity is EntityPlayer player)
            {
                IInventory inv = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                ItemStack stack = inv?[(int)EnumCharacterDressType.ArmorHead]?.Itemstack;

                if (stack?.Collectible is ItemAdaptiveReconstitutionGear gear)
                {
                    ITreeAttribute resistanceLevels = stack.Attributes.GetTreeAttribute("resistances");

                    if (resistanceLevels != null)
                    {
                        float resistance = resistanceLevels.GetFloat(damageSource.Type.ToString(), 0f);

                        float reducedDamage = damage * (1f - resistance);
                        damage = Math.Max(0, reducedDamage);

                    }

                    gear.HandleDamageTaken(damageSource.Type);
                }
            }
            return damage;
        }

        public override string PropertyName() => "adaptiveResistance";
    }
}