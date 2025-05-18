using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Riftworks.src.BE
{
    public class BlockEntityStormCaster : BlockEntity
    {
        protected ICoreServerAPI sapi;
        protected ILoadedSound ambientSound;
        protected double fuelDays = 0;
        protected double lastUpdateTotalDays = 0;

        protected bool HasFuel => fuelDays > 0;

        public bool On { get; set; }

        BlockEntityAnimationUtil AnimUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                RegisterGameTickListener(OnServerTick, 5000);
            }

            lastUpdateTotalDays = api.World.Calendar.TotalDays;

            if (sapi == null) AnimUtil?.InitializeAnimator("stormcaster");

            if (sapi == null && On)
            {
                Activate();
            }
        }

        public void ToggleAmbientSound(bool on)
        {
            if (Api?.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/block/stormcaster.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 0f,
                        Range = 6,
                        SoundType = EnumSoundType.Ambient
                    });

                    if (ambientSound != null)
                    {
                        ambientSound.Start();
                        ambientSound.FadeTo(0.5f, 1f, (s) => { });
                        ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                    }
                }
                else
                {
                    if (ambientSound.IsPlaying) ambientSound.FadeTo(0.5f, 1f, (s) => { });
                }
            }
            else
            {
                ambientSound?.FadeOut(0.5f, (s) => { s.Dispose(); ambientSound = null; });
            }
        }

        private void OnServerTick(float dt)
        {
            if (On)
            {
                double dayspassed = Api.World.Calendar.TotalDays - lastUpdateTotalDays;
                fuelDays -= dayspassed;
                MarkDirty(false);
            }
            if (!HasFuel)
            {
                Deactivate();
            }

            lastUpdateTotalDays = Api.World.Calendar.TotalDays;
        }

        public void Activate()
        {
            if (!HasFuel || Api == null) return;

            On = true;
            lastUpdateTotalDays = Api.World.Calendar.TotalDays;

            AnimUtil?.StartAnimation(new AnimationMetaData() { Animation = "stormspin", Code = "stormspin", EaseInSpeed = 1, EaseOutSpeed = 2, AnimationSpeed = 1f });
            MarkDirty(true);
            ToggleAmbientSound(true);

            if (sapi != null)
            {
                ApplyWeather(on: true);
            }
        }

        public void Deactivate()
        {
            AnimUtil?.StopAnimation("stormspin");
            On = false;
            ToggleAmbientSound(false);
            MarkDirty(true);

            if (sapi != null)
            {
                ApplyWeather(on: false);
            }
        }

        public bool OnInteract(BlockSelection blockSel, IPlayer byPlayer)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/toggleswitch"), Pos, 0, byPlayer, false, 16);

                if (On) Deactivate();
                else Activate();

                return true;
            }

            if (slot.Itemstack.ItemAttributes?.IsTrue("riftwardFuel") == true && fuelDays < 0.5)
            {
                fuelDays += slot.Itemstack.ItemAttributes["rifwardfuelDays"].AsDouble(14);
                slot.TakeOut(1);
                (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Activate();
            }

            return true;
        }

        private void ApplyWeather(bool on)
        {
            WeatherSystemServer weatherSystem = sapi.ModLoader.GetModSystem<WeatherSystemServer>(true);

            if (on)
            {
                // First override precipitation
                weatherSystem.OverridePrecipitation = 1f;
                weatherSystem.serverChannel.BroadcastPacket<WeatherConfigPacket>(new WeatherConfigPacket()
                {
                    OverridePrecipitation = weatherSystem.OverridePrecipitation,
                    RainCloudDaysOffset = weatherSystem.RainCloudDaysOffset
                }, Array.Empty<IServerPlayer>());

                // Then get a random event
                double chance = sapi.World.Rand.NextDouble();
                string chosenEvent = chance switch
                {
                    < 0.20 => "noevent",       // 20%
                    < 0.55 => "lightthunder",  // 35%
                    < 0.90 => "heavythunder",  // 35%
                    < 0.95 => "smallhail",     //  5%
                    _ => "largehail"           //  5%
                };

                // Apply in this block’s region
                BlockPos pos = Pos;
                int regionX = pos.X / sapi.World.BlockAccessor.RegionSize;
                int regionZ = pos.Z / sapi.World.BlockAccessor.RegionSize;
                long idx2d = weatherSystem.MapRegionIndex2D(regionX, regionZ);

                if (weatherSystem.weatherSimByMapRegion.TryGetValue(idx2d, out WeatherSimulationRegion weatherSim) && weatherSim != null)
                {
                    weatherSim.SetWeatherEvent(chosenEvent, true);
                    weatherSim.CurWeatherEvent.AllowStop = true;
                    weatherSim.CurWeatherEvent.OnBeginUse();
                    weatherSim.TickEvery25ms(0.025f);
                    weatherSim.sendWeatherUpdatePacket();
                }
            }
            else
            {
                // Reset precipitation to auto
                weatherSystem.OverridePrecipitation = null;
                weatherSystem.serverChannel.BroadcastPacket<WeatherConfigPacket>(new WeatherConfigPacket()
                {
                    OverridePrecipitation = weatherSystem.OverridePrecipitation,
                    RainCloudDaysOffset = weatherSystem.RainCloudDaysOffset
                }, Array.Empty<IServerPlayer>());

                // Stop any event
                BlockPos pos = Pos;
                int regionX = pos.X / sapi.World.BlockAccessor.RegionSize;
                int regionZ = pos.Z / sapi.World.BlockAccessor.RegionSize;
                long idx2d = weatherSystem.MapRegionIndex2D(regionX, regionZ);

                if (weatherSystem.weatherSimByMapRegion.TryGetValue(idx2d, out WeatherSimulationRegion weatherSim) && weatherSim != null)
                {
                    weatherSim.SetWeatherEvent("noevent", true);
                    weatherSim.CurWeatherEvent.AllowStop = true;
                    weatherSim.CurWeatherEvent.OnBeginUse();
                    weatherSim.TickEvery25ms(0.025f);
                    weatherSim.sendWeatherUpdatePacket();
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            ambientSound?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            ambientSound?.Dispose();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            fuelDays = tree.GetDouble("fuelUntilTotalDays", 0);
            lastUpdateTotalDays = tree.GetDouble("lastUpdateTotalDays");
            On = tree.GetBool("on");

            if (On) Activate();
            else Deactivate();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastUpdateTotalDays", lastUpdateTotalDays);
            tree.SetDouble("fuelUntilTotalDays", fuelDays);
            tree.SetBool("on", On);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (fuelDays <= 0)
            {
                dsc.AppendLine(Lang.Get("Out of power. Recharge with temporal gears."));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Charge for {0:0.#} days", fuelDays));
            }
        }
    }
}