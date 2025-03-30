using Vintagestory.API.Common;
using VEntity = Vintagestory.API.Common.Entities.Entity;

namespace Riftworks.src.Items
{
    internal class ItemRiftBlade : Item
    {
        public override void OnAttackingWith(IWorldAccessor world, VEntity byEntity, VEntity attackedEntity, ItemSlot itemslot)
        {
            DamageSource damage = new DamageSource()
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