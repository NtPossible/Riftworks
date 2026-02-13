using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace Riftworks.src.Items.Wearable
{
    public class ItemAdaptiveReconstitutionGear : ItemWearable
    {
        private const float ADAPTATION_DURATION = 60f;
        private const float BASE_ADAPTATION_RATE = 0.0005f; // 0.05% per second

        private const string damageTypeKey = "adaptType";
        private const string timerKey = "adaptTimer";
        private const string speedKey = "adaptSpeed";
        private const string resistancesKey = "resistances";

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
            string currentDamageTypeKey = attributes.GetString(damageTypeKey, null);

            if (currentDamageTypeKey == null || !Enum.TryParse(currentDamageTypeKey, out EnumDamageType damageType))
            {
                attributes.RemoveAttribute(damageTypeKey);
                return;
            }

            float timer = attributes.GetFloat(timerKey, 0f) + dt;
            if (timer >= ADAPTATION_DURATION)
            {
                attributes.RemoveAttribute(damageTypeKey);
                return;
            }

            float adaptationSpeed = attributes.GetFloat(speedKey, BASE_ADAPTATION_RATE);
            float currentResistance = GetResistance(inSlot, damageType);

            if (currentResistance >= 1f)
            {
                attributes.RemoveAttribute(damageTypeKey);
                return;
            }

            float newResist = Math.Min(currentResistance + adaptationSpeed * dt, 1f);
            SetResistance(inSlot, damageType, newResist);

            int oldPercentage = (int)(currentResistance * 10) * 10;
            int newPercentage = (int)(newResist * 10) * 10;

            attributes.SetFloat(timerKey, timer);

            if (newPercentage > oldPercentage)
            {
                IPlayer? player = (inSlot.Inventory as InventoryCharacter)?.Player;
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    PlayGearSound(api.World, entityPlayer);
                }
            }
        }

        private static float GetResistance(ItemSlot slot, EnumDamageType type)
        {
            return slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).GetFloat(type.ToString(), 0f);
        }

        private static void SetResistance(ItemSlot slot, EnumDamageType type, float value)
        {
            slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).SetFloat(type.ToString(), value);
        }

        private static void PlayGearSound(IWorldAccessor world, EntityPlayer player)
        {
            if (player == null)
            {
                return;
            }
            world.PlaySoundAt(new AssetLocation("riftworks:sounds/gearspin.ogg"), player.Pos.X, player.Pos.Y, player.Pos.Z, null, false, 32f, 1.0f);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetTreeAttribute(resistancesKey);

            if (resistanceLevels != null)
            {
                List<string> adaptedResistances = resistanceLevels
                    .Where(resistanceEntry => resistanceEntry.Value is FloatAttribute floatAttribute && floatAttribute.value > 0)
                    .Select(resistanceEntry => $"{Lang.Get(resistanceEntry.Key)}: {((FloatAttribute)resistanceEntry.Value).value * 100}%")
                    .ToList();

                if (adaptedResistances.Count != 0)
                {
                    dsc.AppendLine("Adaptations:");
                    dsc.AppendLine(string.Join("\n", adaptedResistances));
                }
            }
        }

        public static void ResetResistances(ItemSlot inSlot)
        {
            ItemStack stack = inSlot.Itemstack;
            ITreeAttribute resistanceLevels = stack.Attributes.GetOrAddTreeAttribute(resistancesKey);

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
}
