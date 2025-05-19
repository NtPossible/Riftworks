using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Riftworks.src.Entities
{
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
