using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Riftworks.src.Items
{
    public class ItemRiftBlade : Item
    {
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            DamageSource damage = new()
            {
                Type = EnumDamageType.Heal,
                SourceEntity = byEntity,
                KnockbackStrength = 0
            };
            if (attackedEntity.Alive)
            {
                byEntity.ReceiveDamage(damage, 3f);
            }
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);
        }

    }
}