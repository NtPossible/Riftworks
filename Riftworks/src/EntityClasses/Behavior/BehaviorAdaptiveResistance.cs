using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using Riftworks.src.Items.Wearable;

namespace Riftworks.src.EntityClasses.Behavior
{
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
}
