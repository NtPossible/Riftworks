using Riftworks.src.Items.Wearable;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Riftworks.src.Systems
{
    public sealed class ModSystemAdaptiveGearRenderer : ModSystem, IRenderer
    {
        private ICoreClientAPI capi = null!;

        // Spin animation
        private float currentAngle = 0f;
        private float targetAngle = 0f;
        private const float SpinDuration = 0.25f;
        private const float SpinDegrees = 60f;

        // Tier change detection
        private int lastSpinCount = -1;

        // Glow animation state
        private float glowOscillationTime = 0f;
        private float glowFlashTimer = 0f;

        // Glow flash parameters
        private const float GlowFlashDuration = 1.2f;
        private const float GlowFlashMin = 50f;
        private const float GlowFlashMax = 220f;

        // Glow oscillation parameters
        private const float GlowOscillationBase = 37.5f;
        private const float GlowOscillationAmplitude = 12.5f;
        private const float GlowOscillationSpeed = 1.8f;

        // Cached mesh
        private MeshRef? meshRef;

        private static readonly double[] IdentityMatrix = {
            1,0,0,0,
            0,1,0,0,
            0,0,1,0,
            0,0,0,1
        };

        public double RenderOrder => 0.4;
        public int RenderRange => 24;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "adaptivegear-spin");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IClientPlayer? player = capi.World.Player;
            EntityPlayer? entity = player?.Entity;
            if (entity == null)
            {
                return;
            }

            IInventory? inventory = player!.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inventory == null)
            {
                return;
            }

            ItemSlot? slot = inventory.FirstOrDefault(slot => slot?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear);
            if (slot == null || slot.Empty)
            {
                lastSpinCount = -1;
                currentAngle = 0f;
                targetAngle = 0f;
                glowOscillationTime = 0f;
                glowFlashTimer = 0f;
                InvalidateMesh();
                return;
            }

            // build the mesh if we don't have one yet
            meshRef ??= ItemAdaptiveReconstitutionGear.GetOrBuildMeshRef(capi, slot.Itemstack);

            if (meshRef == null)
            {
                return;
            }

            // check if a new tier was crossed since last frame
            int currentSpinCount = slot.Itemstack.Attributes.GetInt(ItemAdaptiveReconstitutionGear.spinCountKey, 0);
            if (lastSpinCount == -1)
            {
                lastSpinCount = currentSpinCount;
            }
            else if (currentSpinCount > lastSpinCount)
            {
                lastSpinCount = currentSpinCount;
                targetAngle += SpinDegrees;

                // big flash that fades back to the base glow
                glowFlashTimer = GlowFlashDuration;
            }

            // do the spin
            UpdateSpin(deltaTime);

            int glow = UpdateGlow(deltaTime, slot.Itemstack);

            RenderGearOnPlayer(entity, currentAngle, glow);
        }

        private void UpdateSpin(float deltaTime)
        {
            if (currentAngle < targetAngle)
            {
                float speed = SpinDegrees / SpinDuration;
                currentAngle = Math.Min(currentAngle + speed * deltaTime, targetAngle);
            }

            if (currentAngle >= 360f)
            {
                currentAngle -= 360f;
                targetAngle = Math.Max(targetAngle - 360f, 0f);
            }
        }

        private int UpdateGlow(float deltaTime, ItemStack stack)
        {
            if (glowFlashTimer > 0f)
            {
                glowFlashTimer = Math.Max(glowFlashTimer - deltaTime, 0f);
                float progress = glowFlashTimer / GlowFlashDuration;
                return (int)GameMath.Lerp(GlowFlashMin, GlowFlashMax, progress);
            }

            if (ItemAdaptiveReconstitutionGear.IsAdapting(stack))
            {
                glowOscillationTime += deltaTime * GlowOscillationSpeed;
                return (int)(GlowOscillationBase + GlowOscillationAmplitude * MathF.Sin(glowOscillationTime));
            }

            glowOscillationTime = 0f;
            return 0;
        }

        private void RenderGearOnPlayer(EntityPlayer player, float spinAngleDeg, int glow)
        {
            IRenderAPI rapi = capi.Render;
            EntityPos pos = player.Pos;

            // start with a blank matrix so our position/rotation is in world space - using the camera matrix made the gear fly around
            rapi.GlPushMatrix();
            rapi.GlLoadMatrix(IdentityMatrix);

            // position above the player's head
            rapi.GlTranslate(0, player.LocalEyePos.Y + 0.5, 0);

            // rotate the gear to the facing direction
            rapi.GlRotate((player.BodyYaw * GameMath.RAD2DEG), 0, 1, 0);

            // apply the spin
            rapi.GlRotate(spinAngleDeg, 0, 1, 0);

            // set up the shader with lighting from the player's world position
            BlockPos blockPos = pos.AsBlockPos;
            IStandardShaderProgram shader = rapi.PreparedStandardShader(blockPos.X, blockPos.Y, blockPos.Z);

            // model matrix is our transforms, view is the camera (kept separate so head rotation only affects the view and doesn't move the gear's world position)
            shader.ModelMatrix = rapi.CurrentModelviewMatrix;
            shader.ViewMatrix = rapi.CameraMatrixOriginf;
            shader.ProjectionMatrix = rapi.CurrentProjectionMatrix;
            shader.ExtraGlow = glow;

            // bind the item texture atlas so the gear's texture renders correctly
            shader.Tex2D = capi.ItemTextureAtlas.AtlasTextures[0].TextureId;
            rapi.RenderMesh(meshRef!);

            shader.Stop();
            rapi.GlPopMatrix();
        }

        private void InvalidateMesh()
        {
            meshRef = null;
        }
    }
}