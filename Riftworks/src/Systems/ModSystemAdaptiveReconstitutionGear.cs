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
using Vintagestory.API.Util;

namespace Riftworks.src.Systems
{
    internal class ModSystemAdaptiveReconstitutionGear : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;
        private Dictionary<string, EntityAdaptiveReconstitutionGear> playerGearEntities = new Dictionary<string, EntityAdaptiveReconstitutionGear>();

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
                        //if (!playerGearEntities.ContainsKey(plr.PlayerUID))
                        //{
                        //    EntityProperties entityType = sapi.World.GetEntityType(new AssetLocation("riftworks:entityadaptivereconstitutiongear"));
                        //    EntityAdaptiveReconstitutionGear gearEntity = sapi.World.ClassRegistry.CreateEntity(entityType) as EntityAdaptiveReconstitutionGear;

                        //    gearEntity.owner = playerEntity;
                        //    playerGearEntities[plr.PlayerUID] = gearEntity;
                        //    sapi.World.SpawnEntity(gearEntity);
                        //}
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

                        //if (playerGearEntities.TryGetValue(plr.PlayerUID, out EntityAdaptiveReconstitutionGear value))
                        //{
                        //    Entity gearEntity = value;
                        //    gearEntity.Die();
                        //    playerGearEntities.Remove(plr.PlayerUID);
                        //}
                    }
                }
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (entity is not EntityPlayer player) return;

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

                //if (playerGearEntities.TryGetValue(player.PlayerUID, out EntityAdaptiveReconstitutionGear value))
                //{
                //    Entity gearEntity = value;
                //    gearEntity.Die();
                //    playerGearEntities.Remove(player.PlayerUID);
                //}

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
        private const float ADAPTATION_DURATION = 60f; 
        private const float BASE_ADAPTATION_RATE = 0.0005f; // 0.05% per second

        private const string damageTypeKey = "adaptType";
        private const string timerKey = "adaptTimer";
        private const string speedKey = "adaptSpeed";

        public void HandleDamageTaken(EnumDamageType damageType, ItemSlot inSlot)
        {
            if (damageType == EnumDamageType.Heal || GetResistance(inSlot, damageType) >= 1f)
            {
                return;
            }

            ITreeAttribute attributes = inSlot.Itemstack.Attributes;

            string current = attributes.GetString(damageTypeKey, null);

            if (current != damageType.ToString())
            {
                attributes.SetString(damageTypeKey, damageType.ToString());
                attributes.SetFloat(timerKey, 0f);
                attributes.SetFloat(speedKey, BASE_ADAPTATION_RATE);
            }
            else
            {
                attributes.SetFloat(timerKey, 0f);
                attributes.SetFloat(speedKey, attributes.GetFloat(speedKey, BASE_ADAPTATION_RATE) * 1.05f);
            }
        }

        public void UpdateAdaptation(float dt, ItemSlot inSlot)
        {
            ITreeAttribute attributes = inSlot.Itemstack.Attributes;
            string damageTypeKey = attributes.GetString("adaptType", null);

            if (damageTypeKey == null || !Enum.TryParse(damageTypeKey, out EnumDamageType damageType))
            {
                attributes.RemoveAttribute("adaptType");
                return;
            }

            float timer = attributes.GetFloat("adaptTimer", 0f) + dt;
            if (timer >= ADAPTATION_DURATION)
            {
                attributes.RemoveAttribute("adaptType");
                return;
            }

            float speed = attributes.GetFloat("adaptSpeed", BASE_ADAPTATION_RATE);
            float currentResistance = GetResistance(inSlot, damageType);

            if (currentResistance >= 1f)
            {
                attributes.RemoveAttribute("adaptType");
                return;
            }

            float newResist = Math.Min(currentResistance + speed * dt, 1f);
            SetResistance(inSlot, damageType, newResist);

            int oldPercentage = (int)(currentResistance * 10) * 10;
            int newPercentage = (int)(newResist * 10) * 10;

            attributes.SetFloat("adaptTimer", timer);

            if (newPercentage > oldPercentage)
            {
                IPlayer player = (inSlot.Inventory as InventoryCharacter)?.Player;
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    PlayGearSound(api.World, entityPlayer);
                }
            }
        }

        private float GetResistance(ItemSlot slot, EnumDamageType type)
        {
            return slot.Itemstack.Attributes.GetOrAddTreeAttribute("resistances").GetFloat(type.ToString(), 0f);
        }

        private void SetResistance(ItemSlot slot, EnumDamageType type, float value)
        {
            slot.Itemstack.Attributes.GetOrAddTreeAttribute("resistances").SetFloat(type.ToString(), value);
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

        public void ResetResistances(ItemSlot inSlot)
        {
            ItemStack stack = inSlot.Itemstack;
            ITreeAttribute resistanceLevels = stack.Attributes.GetOrAddTreeAttribute("resistances");

            foreach (EnumDamageType damageType in Enum.GetValues(typeof(EnumDamageType)))
            {
                resistanceLevels.SetFloat(damageType.ToString(), 0f);
            }

            inSlot.Itemstack.Attributes.RemoveAttribute(damageTypeKey);
            inSlot.Itemstack.Attributes.RemoveAttribute(timerKey);
            inSlot.Itemstack.Attributes.RemoveAttribute(speedKey);
        }

        //public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        //{
        //    MultiTextureMeshRef meshRef = ObjectCacheUtil.GetOrCreate(capi, "adaptiveGearMesh", () =>
        //    {
        //        Shape shape = capi.Assets.TryGet(new AssetLocation("riftworks:shapes/item/devices/adaptivereconstitutiongear.json")).ToObject<Shape>();
        //        capi.Tesselator.TesselateShape(this, shape, out MeshData meshdata);
        //        return capi.Render.UploadMultiTextureMesh(meshdata);
        //    });

        //    base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        //    renderinfo.ModelRef = meshRef;
        //}

        //public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;

        //public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        //{
        //}

        //public string GetCategoryCode(ItemStack stack) => "adaptivegear";

        //CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode)
        //{
        //    return null; 
        //}

        //public string[] GetDisableElements(ItemStack stack) => null;

        //public string[] GetKeepElements(ItemStack stack) => null;

        //public string GetTexturePrefixCode(ItemStack stack) => "adaptivegear";
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
            if (damageSource.Type == EnumDamageType.Heal)
            {
                return damage;
            }

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

                    gear.HandleDamageTaken(damageSource.Type, inv[(int)EnumCharacterDressType.ArmorHead]);
                }
            }
            return damage;
        }

        public override string PropertyName() => "adaptiveResistance";
    }

    public class EntityAdaptiveReconstitutionGear : Entity
    {
        public EntityPlayer owner;

        public override bool IsInteractable => false;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long chunkindex3d)
        {
            this.Properties = properties;
            base.Initialize(properties, api, chunkindex3d);

            if (api.Side == EnumAppSide.Client)
            {
                AnimManager?.StartAnimation("slowspin");
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Api.Side == EnumAppSide.Server && owner != null)
            {
                // Make entity fly above player’s head
                Pos.SetFrom(owner.Pos);
                Pos.Y += 2;
                ServerPos.SetFrom(Pos);

                // stop motion
                ServerPos.Yaw = 0f;
                ServerPos.Roll = 0f;
                ServerPos.Pitch = 0f;
            }
        }
    }
}