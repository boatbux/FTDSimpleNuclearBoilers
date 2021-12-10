
using BrilliantSkies.Blocks.SteamEngines.Ui;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Ftd.Constructs.Modules.All.StandardExplosion;
using BrilliantSkies.Ftd.Constructs.Modules.Main.Power;
using BrilliantSkies.Ftd.DamageLogging;
using BrilliantSkies.Ftd.DamageModels;
using BrilliantSkies.Localisation;
using BrilliantSkies.Localisation.Runtime.FileManagers.Files;
using BrilliantSkies.Ui.Tips;
using System;
using UnityEngine;

namespace CultOfClang.NuclearReactor
{
    public class NuclearAllInOne : SteamTank
    {
        public new static ILocFile _locFile = Loc.GetFile("NuclearAllInOne");
        private bool _detonated;
        const float MultiplyerPowerDensity = 10; // down to 10 from 100 so it should only count as 10 of the 3x3x3 RTG's if anything, for 4,050 electricity per second
        const float HeatPerVolume = 60; // bumped this up from 40 to 60
        private static float ExplosionDamage { get; } = 250000f; // a nuclear reactor isn't designed to explode, but it should still be explodey for balance purposes, these stats are still a really good deal for the user
        private static float ExplosionRadius { get; } = PayloadDerivedValues.GetExplosionRadius(ExplosionDamage);
        public float RtgVolume => MultiplyerPowerDensity * (float)this.item.SizeInfo.ArrayPositionsUsed;
        public float SteamPerSecond => 1600 * (float)this.item.SizeInfo.ArrayPositionsUsed; // 1600 is just for my interpretation/fork. I figure my fork of the boiler should be 5x5x5, so that'd be 125 cubes, and 125x1600 is 200,000, and that's a nice number for 1 nuclear boiler for what I want to do with it. that's 40 steam-jets operating at max capacity

        protected override void AppendToolTip(ProTip tip)
        {
            base.AppendToolTip(tip);
            tip.SetSpecial_Name(RTG._locFile.Get("SpecialName", "Simple Nuclear Boiler", true), RTG._locFile.Get("SpecialDescription", "Nuclear Boiler, generates endless steam using the power of the atom!", true)); //typo corrections and a name change
            tip.InfoOnly = true;
            tip.Add(Position.Middle, RTG._locFile.Format("Return_CreatesEnergyPer", "Creates <<{0} steam per second>>", SteamPerSecond));
            if (this.StorageModule != null)
                SteamSharedUiHelper.AppendPressureAndCapacityBar(tip, this.StorageModule);
        }

        public override void StateChanged(IBlockStateChange change)
        {
            base.StateChanged(change);
            if (change.IsAvailableToConstruct)
            {
                this.MainConstruct.PowerUsageCreationAndFuelRestricted.RtgVolume += RtgVolume;
                this.MainConstruct.HotObjectsRestricted.AddASimpleSourceOfBodyHeat(HeatChange);
                this.MainConstruct.SchedulerRestricted.RegisterForFixedUpdate(Update);

            }
            else
            {
                if (!change.IsLostToConstructOrConstructLost)
                    return;
                this.MainConstruct.PowerUsageCreationAndFuelRestricted.RtgVolume -= RtgVolume;
                this.MainConstruct.HotObjectsRestricted.RemoveASimpleSourceofBodyHeat(HeatChange);
                this.MainConstruct.SchedulerRestricted.UnregisterForFixedUpdate(Update);

            }
        }

        private float HeatChange => (float)(this.item.SizeInfo.ArrayPositionsUsed * HeatPerVolume);

        public void Detonate()
        {
            if (this._detonated)
                return;
            this._detonated = true;
            ExplosionCreator.Explosion((IAllConstructBlock)this.MainConstruct, new ExplosionDamageDescription(this.MainConstruct.GunnerReward, ExplosionDamage, ExplosionRadius, this.GameWorldPosition)
            {
                SolidCoordLink = new SolidCoord(this.GetConstructableOrSubConstructable(), this.LocalPosition)
            });
            UnityEngine.Object.Instantiate(Resources.Load("Detonator-MushroomCloud"), this.GameWorldPosition, Quaternion.identity);
        }

        public override BlockTechInfo GetTechInfo()
        {
            return new BlockTechInfo()
                .AddSpec(RTG._locFile.Get("TechInfo_EnergyGeneratedPer", "Energy generated per second", true), RtgVolume)
                .AddSpec("Steam", this.StorageModule?.Amount)
                .AddStatement(RTG._locFile.Format("TechInfo_IrDetectionRange", "Adds {0}{1} to body temperature of the vehicle. This will have a small impact on IR detection range from all angles.",
    this.HeatChange,
     "°C/m\u00B3"
));
        }

        private void Update(ISectorTimeStep obj)
        {
            this.GetConstructableOrSubConstructable().MainThreadRotation = Quaternion.identity;
            var steam = obj.DeltaTime * SteamPerSecond;
            this.StorageModule.AddSteam(steam);
            this.Stats.BoilerSteamCreated.Add(steam);
            if (StorageModule.Pressure =< 2) //what if it explodes when there's a major leak?? That'd give players a reason to fortify their steam pipe network!
                Detonate();
        }
    }
}
