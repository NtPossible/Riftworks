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
        private const float adaptationDuration = 30f;
        private const float baseAdaptationRate = 0.0005f; // 0.05% per second
        private const float adaptationSpeedScaling = 1.05f;
        private const int resistanceTierCount = 5; // each tier is  an additional 20% so it goes like 0,20,40,60,80,100

        private const string damageTypeKey = "adaptType";
        private const string timerKey = "adaptTimer";
        private const string speedKey = "adaptSpeed";
        private const string resistancesKey = "resistances";

        private static readonly AssetLocation gearSoundLocation = new("riftworks:sounds/gearspin.ogg");

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
                attributes.SetFloat(speedKey, attributes.GetFloat(speedKey, baseAdaptationRate) * adaptationSpeedScaling);
            }
        }

        public void UpdateAdaptation(float dt, ItemSlot inSlot)
        {
            ITreeAttribute attributes = inSlot.Itemstack.Attributes;
            string damageTypeString = attributes.GetString(damageTypeKey, null);

            if (damageTypeString == null || !Enum.TryParse(damageTypeString, out EnumDamageType damageType))
            {
                ClearAdaptationState(attributes);
                return;
            }

            float timer = attributes.GetFloat(timerKey, 0f) + dt;
            if (timer >= adaptationDuration)
            {
                ClearAdaptationState(attributes);
                return;
            }

            float currentResistance = GetResistance(inSlot, damageType);
            if (currentResistance >= 1f)
            {
                ClearAdaptationState(attributes);
                return;
            }

            float adaptationSpeed = attributes.GetFloat(speedKey, baseAdaptationRate);
            float newResistance = Math.Min(currentResistance + adaptationSpeed * dt, 1f);
            SetResistance(inSlot, damageType, newResistance);

            attributes.SetFloat(timerKey, timer);

            // Play sound when a new 20% threshold is crossed
            int oldTier = (int)(currentResistance * resistanceTierCount);
            int newTier = (int)(newResistance * resistanceTierCount);
            if (newTier > oldTier)
            {
                IPlayer? player = (inSlot.Inventory as InventoryCharacter)?.Player;
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    PlayGearSound(api.World, entityPlayer);
                }
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ITreeAttribute resistanceLevels = inSlot.Itemstack.Attributes.GetTreeAttribute(resistancesKey);

            if (resistanceLevels != null)
            {
                List<string> adaptedResistances = resistanceLevels
                    .Where(resistanceEntry => resistanceEntry.Value is FloatAttribute attribute && attribute.value > 0)
                    .Select(entry => {
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
        }

        private static float GetResistance(ItemSlot slot, EnumDamageType type)
        {
            return slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).GetFloat(type.ToString(), 0f);
        }

        private static void SetResistance(ItemSlot slot, EnumDamageType type, float value)
        {
            slot.Itemstack.Attributes.GetOrAddTreeAttribute(resistancesKey).SetFloat(type.ToString(), value);
        }

        // Clears adaptation tracking.
        private static void ClearAdaptationState(ITreeAttribute attributes)
        {
            attributes.RemoveAttribute(damageTypeKey);
            attributes.RemoveAttribute(timerKey);
            attributes.RemoveAttribute(speedKey);
        }

        // Wipe all attributes
        public static void ResetResistances(ItemSlot inSlot)
        {
            ClearAdaptationState(inSlot.Itemstack.Attributes);
            inSlot.Itemstack.Attributes.RemoveAttribute(resistancesKey);
        }

        private static void PlayGearSound(IWorldAccessor world, EntityPlayer player)
        {
            if (player == null)
            {
                return;
            }
            world.PlaySoundAt(gearSoundLocation, player.Pos.X, player.Pos.Y, player.Pos.Z, null, false, 32f, 1.0f);
        }
    }
}