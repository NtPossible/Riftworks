using Riftworks.src.Items.Wearable;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Riftworks.src.EntityClasses.Behavior
{
    public class EntityBehaviorAdaptiveResistance : EntityBehavior
    {
        public EntityBehaviorAdaptiveResistance(Entity entity) : base(entity)
        {
            EntityBehaviorHealth? healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
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
                IInventory? inventory = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                ItemSlot? slot = inventory?.FirstOrDefault(slot => slot?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear);
                ItemStack? stack = slot?.Itemstack;
                if (stack?.Collectible is ItemAdaptiveReconstitutionGear && slot != null)
                {
                    ITreeAttribute? resistanceLevels = stack.Attributes.GetTreeAttribute("resistances");

                    if (resistanceLevels != null)
                    {
                        float resistance = resistanceLevels.GetFloat(damageSource.Type.ToString(), 0f);
                        // Floor to nearest 20% increment
                        float tieredResistance = (float)Math.Floor(resistance * 5f) / 5f;

                        float reducedDamage = damage * (1f - tieredResistance);
                        damage = Math.Max(0, reducedDamage);
                    }

                    ItemAdaptiveReconstitutionGear.HandleDamageTaken(damageSource.Type, slot);
                }
            }
            return damage;
        }

        public override string PropertyName() => "adaptiveResistance";
    }
}
