using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace Riftworks.src.Items.Wearable
{
    public class ItemAdaptiveReconstitutionGear : ItemWearable, IAttachableToEntity
    {
        private const float adaptationDuration = 60f;
        private const float baseAdaptationRate = 0.0005f; // 0.05% per second
        private const float adaptationSpeedScaling = 0.0002f; // adds 0.02% per second per hit
        private const int resistanceTierCount = 5; // each tier is an additional 20% so it goes like 0,20,40,60,80,100

        private const string damageTypeKey = "adaptType";
        private const string timerKey = "adaptTimer";
        private const string speedKey = "adaptSpeed";
        private const string resistancesKey = "resistances";
        private const string isAdaptingKey = "isAdapting";

        private static readonly AssetLocation gearSoundLocation = new("riftworks:sounds/gearspin.ogg");
        internal const string spinCountKey = "spinCount";
        private const string renderCacheKey = "adaptivegear-rendercache";

        public static MeshRef? GetOrBuildMeshRef(ICoreClientAPI capi, ItemStack stack)
        {
            // return the cached mesh if we already built it
            MeshRef? existingMeshRef = ObjectCacheUtil.TryGet<MeshRef>(capi, renderCacheKey);
            if (existingMeshRef != null)
            {
                return existingMeshRef;
            }

            // tessellate the item into raw mesh data
            capi.Tesselator.TesselateItem(stack.Item, out MeshData meshData);
            if (meshData == null)
            {
                return null;
            }

            // upload to gpu and cache so it's not rebuilt every frame
            MeshRef meshRef = capi.Render.UploadMesh(meshData);
            ObjectCacheUtil.GetOrCreate(capi, renderCacheKey, () => meshRef);
            return meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            if (api is ICoreClientAPI capi)
            {
                MeshRef? meshRef = ObjectCacheUtil.TryGet<MeshRef>(capi, renderCacheKey);
                if (meshRef != null)
                {
                    meshRef.Dispose();
                    ObjectCacheUtil.Delete(capi, renderCacheKey);
                }
            }
        }

        public int RequiresBehindSlots { get; set; } = 0;
        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;
        public CompositeShape? GetAttachedShape(ItemStack stack, string slotCode) => new() { Base = new AssetLocation("game", "block/basic/invisible") };
        public string GetCategoryCode(ItemStack stack) => Attributes["clothescategory"].AsString("head");
        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict) => IAttachableToEntity.CollectTexturesFromCollectible(stack, texturePrefixCode, shape, intoDict);
        public string[]? GetDisableElements(ItemStack stack) => null;
        public string[]? GetKeepElements(ItemStack stack) => null;
        public string GetTexturePrefixCode(ItemStack stack) => "adaptivegear";

        public static void HandleDamageTaken(EnumDamageType damageType, ItemSlot inSlot)
        {
            if (damageType == EnumDamageType.Heal || GetResistance(inSlot, damageType) >= 1f)
            {
                return;
            }

            ITreeAttribute attributes = inSlot.Itemstack.Attributes;
            string current = attributes.GetString(damageTypeKey, null);

            if (current != damageType.ToString())
            {
                // Switched damage type - reset speed and timer
                attributes.SetString(damageTypeKey, damageType.ToString());
                attributes.SetFloat(timerKey, 0f);
                attributes.SetFloat(speedKey, baseAdaptationRate);
            }
            else
            {
                // Same damage type — reset timer and ramp up speed
                attributes.SetFloat(timerKey, 0f);
                attributes.SetFloat(speedKey, attributes.GetFloat(speedKey, baseAdaptationRate) + adaptationSpeedScaling);
            }
        }

        public bool UpdateAdaptation(float dt, ItemSlot inSlot)
        {
            ITreeAttribute attributes = inSlot.Itemstack.Attributes;
            bool wasAdapting = attributes.GetBool(isAdaptingKey, false);

            if (!GetActiveAdaptation(attributes, inSlot, out EnumDamageType damageType, out float timer, out float currentResistance))
            {
                ClearAdaptationState(attributes);
                return wasAdapting;
            }

            float adaptationSpeed = attributes.GetFloat(speedKey, baseAdaptationRate);
            int currentTier = (int)(currentResistance * resistanceTierCount);

            // each higher tier is harder to reach — slows both from tier height and proximity to cap
            float tierDifficultyModifier = 1f / (1f + currentTier * 1.5f);
            float scaledSpeed = adaptationSpeed * (1f - currentResistance) * tierDifficultyModifier;

            float newResistance = Math.Min(currentResistance + scaledSpeed * dt, 1f);
            SetResistance(inSlot, damageType, newResistance);
            attributes.SetFloat(timerKey, timer + dt);
            attributes.SetBool(isAdaptingKey, true);

            int newTier = (int)(newResistance * resistanceTierCount);
            if (newTier > currentTier)
            {
                // reset speed on new tier
                attributes.SetFloat(speedKey, baseAdaptationRate);
                attributes.SetInt(spinCountKey, attributes.GetInt(spinCountKey, 0) + 1);

                IPlayer? player = (inSlot.Inventory as InventoryCharacter)?.Player;

                // instantly heal to full when a tier is passed
                EntityBehaviorHealth? health = player?.Entity?.GetBehavior<EntityBehaviorHealth>();
                if (health != null)
                {
                    health.Health = health.MaxHealth;
                }

                // Play sound
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    PlayGearSound(api.World, entityPlayer);
                }

                return true;
            }

            return !wasAdapting;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetTreeAttribute(resistancesKey);
            if (resistanceLevels == null)
            {
                return;
            }

            List<string> adaptedResistances = resistanceLevels
                .Where(entry => entry.Value is FloatAttribute attribute && attribute.value > 0)
                .Select(entry =>
                {
                    float raw = ((FloatAttribute)entry.Value).value;
                    float tiered = (float)Math.Floor(raw * resistanceTierCount) / resistanceTierCount;
                    return $"{Lang.Get(entry.Key)}: {tiered * 100:0}%";
                })
                .ToList();

            if (adaptedResistances.Count != 0)
            {
                dsc.AppendLine(Lang.Get("riftworks:adaptations-label"));
                dsc.AppendLine(string.Join("\n", adaptedResistances));
            }
        }

        private static bool GetActiveAdaptation(ITreeAttribute attributes, ItemSlot inSlot, out EnumDamageType damageType, out float timer, out float currentResistance)
        {
            damageType = default;
            currentResistance = 0f;
            timer = attributes.GetFloat(timerKey, 0f);

            string? damageTypeString = attributes.GetString(damageTypeKey, null);
            if (damageTypeString == null || !Enum.TryParse(damageTypeString, out damageType))
            {
                return false;
            }

            if (timer >= adaptationDuration)
            {
                return false;
            }

            currentResistance = GetResistance(inSlot, damageType);
            return currentResistance < 1f;
        }

        private static float GetResistance(ItemSlot slot, EnumDamageType type) => slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).GetFloat(type.ToString(), 0f);

        private static void SetResistance(ItemSlot slot, EnumDamageType type, float value) => slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).SetFloat(type.ToString(), value);

        public static bool IsAdapting(ItemStack stack) => stack.Attributes.GetBool(isAdaptingKey, false);

        private static void ClearAdaptationState(ITreeAttribute attributes)
        {
            attributes.RemoveAttribute(damageTypeKey);
            attributes.RemoveAttribute(timerKey);
            attributes.RemoveAttribute(speedKey);
            attributes.SetBool(isAdaptingKey, false);
        }

        public static void ResetResistances(ItemSlot inSlot)
        {
            ClearAdaptationState(inSlot.Itemstack.Attributes);
            inSlot.Itemstack.Attributes.RemoveAttribute(resistancesKey);
        }

        private static void PlayGearSound(IWorldAccessor world, EntityPlayer player)
        {
            world.PlaySoundAt(gearSoundLocation, player.Pos.X, player.Pos.Y, player.Pos.Z, null, false, 32f, 1.0f);
        }
    }
}