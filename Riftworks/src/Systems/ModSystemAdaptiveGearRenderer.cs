using Riftworks.src.Items.Wearable;
using System;
using System.Collections.Generic;
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

        private sealed class GearState
        {
            public MeshRef? MeshRef;
            public float CurrentAngle;
            public float TargetAngle;
            public int LastSpinCount = -1;
            public float GlowOscillationTime;
            public float GlowFlashTimer;
        }

        private readonly Dictionary<long, GearState> gearStates = new();
        private readonly HashSet<long> activePlayerIds = new();

        // Spin animation
        private const float SpinSpeed = 240f;
        private const float SpinDegrees = 60f;

        // Glow flash parameters
        private const float GlowFlashDuration = 1.2f;
        private const float GlowFlashMin = 50f;
        private const float GlowFlashMax = 220f;

        // Glow oscillation parameters
        private const float GlowOscillationBase = 37.5f;
        private const float GlowOscillationAmplitude = 12.5f;
        private const float GlowOscillationSpeed = 1.8f;

        public double RenderOrder => 0.4;
        public int RenderRange => 24;

        private readonly Matrixf matrixf = new();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "adaptivegear-opaque");
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            activePlayerIds.Clear();

            foreach (EntityPlayer? entity in capi.World.AllOnlinePlayers.Select(player => player.Entity))
            {
                if (entity == null)
                {
                    continue;
                }
                ItemSlot? slot = FindGearSlot(entity);
                long id = entity.EntityId;

                if (slot == null || slot.Empty)
                {
                    gearStates.Remove(id);
                    continue;
                }

                activePlayerIds.Add(id);

                if (!gearStates.TryGetValue(id, out GearState? state))
                {
                    state = new GearState();
                    gearStates[id] = state;
                }

                state.MeshRef ??= ItemAdaptiveReconstitutionGear.GetOrBuildMeshRef(capi, slot.Itemstack);
                if (state.MeshRef == null)
                {
                    continue;
                }

                int currentSpinCount = slot.Itemstack.Attributes.GetInt(ItemAdaptiveReconstitutionGear.spinCountKey, 0);
                if (state.LastSpinCount == -1)
                {
                    state.LastSpinCount = currentSpinCount;
                }
                else if (currentSpinCount > state.LastSpinCount)
                {
                    state.LastSpinCount = currentSpinCount;
                    state.TargetAngle += SpinDegrees;
                    state.GlowFlashTimer = GlowFlashDuration;
                }

                UpdateSpin(state, deltaTime);
                int glow = UpdateGlow(state, deltaTime, slot.Itemstack);
                RenderGearOnPlayer(entity, state.CurrentAngle, glow, state.MeshRef);
            }

            foreach (long id in gearStates.Keys.Where(id => !activePlayerIds.Contains(id)).ToList())
            {
                gearStates.Remove(id);
            }
        }

        private static ItemSlot? FindGearSlot(EntityPlayer entity)
        {
            ItemSlot? slot = null;
            entity.WalkInventory(itemSlot =>
            {
                if (itemSlot?.Itemstack?.Collectible is ItemAdaptiveReconstitutionGear && itemSlot.Inventory?.ClassName == GlobalConstants.characterInvClassName)
                {
                    slot = itemSlot;
                    return false;
                }
                return true;
            });
            return slot;
        }

        private static void UpdateSpin(GearState state, float deltaTime)
        {
            if (state.CurrentAngle < state.TargetAngle)
            {
                state.CurrentAngle = Math.Min(state.CurrentAngle + SpinSpeed * deltaTime, state.TargetAngle);
            }

            if (state.CurrentAngle >= 360f)
            {
                state.CurrentAngle -= 360f;
                state.TargetAngle = Math.Max(state.TargetAngle - 360f, 0f);
            }
        }

        private static int UpdateGlow(GearState state, float deltaTime, ItemStack stack)
        {
            if (state.GlowFlashTimer > 0f)
            {
                state.GlowFlashTimer = Math.Max(state.GlowFlashTimer - deltaTime, 0f);
                float progress = state.GlowFlashTimer / GlowFlashDuration;
                return (int)GameMath.Lerp(GlowFlashMin, GlowFlashMax, progress);
            }

            if (ItemAdaptiveReconstitutionGear.IsAdapting(stack))
            {
                state.GlowOscillationTime += deltaTime * GlowOscillationSpeed;
                return (int)(GlowOscillationBase + GlowOscillationAmplitude * MathF.Sin(state.GlowOscillationTime));
            }

            state.GlowOscillationTime = 0f;
            return 0;
        }

        private void RenderGearOnPlayer(EntityPlayer player, float spinAngleDeg, int glow, MeshRef meshRef)
        {
            IRenderAPI renderAPI = capi.Render;

            // Offset this player's position relative to the local camera origin
            EntityPos localPos = capi.World.Player.Entity.Pos;
            float dx = (float)(player.Pos.X - localPos.X);
            float dy = (float)(player.Pos.Y - localPos.Y);
            float dz = (float)(player.Pos.Z - localPos.Z);

            // Start with a blank matrix so our position/rotation is in world space - using the camera matrix made the gear fly around
            matrixf.Identity();
            // Position above the player's head
            matrixf.Translate(dx, dy + (float)player.LocalEyePos.Y + 0.5f, dz);
            // Rotate the gear to the facing direction
            matrixf.RotateY(player.BodyYaw);
            // Apply the spin
            matrixf.RotateY(spinAngleDeg * GameMath.DEG2RAD);

            // Set up the shader with lighting from the player's world position
            BlockPos blockPos = player.Pos.AsBlockPos;
            IStandardShaderProgram shader = renderAPI.PreparedStandardShader(blockPos.X, blockPos.Y, blockPos.Z);
            shader.ModelMatrix = matrixf.Values;
            shader.ViewMatrix = renderAPI.CameraMatrixOriginf;
            shader.ProjectionMatrix = renderAPI.CurrentProjectionMatrix;
            shader.ExtraGlow = glow;

            // Bind the item texture atlas so the gear's texture renders correctly
            shader.Tex2D = capi.ItemTextureAtlas.AtlasTextures[0].TextureId;
            renderAPI.RenderMesh(meshRef);
            shader.Stop();
        }
    }
}