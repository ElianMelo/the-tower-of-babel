using System;
using UnityEngine;

namespace TowerOfBabel.Cheats
{
    public enum CheatStat
    {
        WalkSpeed, SprintSpeed, CrouchSpeed, Acceleration, AirControl, JumpHeight, Gravity,
        ExtraJumps, MouseSensitivity, CameraFov, GatherSpeed, ProcessSpeed, BuildSpeed,
        GatherProduction, ProcessProduction, BuildProduction, GatherCost, ProcessCost, BuildCost,
        BagCapacity, Experience, ResourceRespawnSpeed
    }

    public enum CheatCommand
    {
        AddStone, AddProcessedStone, FillBag, ClearBag, ResetResources, InstantGather, InstantProcess,
        BuildAsset, AdvanceAsset, ResetAsset, BuildFloor, ResetFloor, BuildTower, ResetTower,
        UnlockAllUpgrades, ResetModifiers, AddSand, AddGlass
    }

    [Serializable]
    public sealed class CheatProfile
    {
        private readonly float[] values = new float[Enum.GetValues(typeof(CheatStat)).Length];
        public CheatProfile() { Reset(); }
        public static float Default(CheatStat stat) => stat == CheatStat.BagCapacity ? 50 : stat == CheatStat.CameraFov ? 60 : stat == CheatStat.ExtraJumps ? 0 : 1;
        public static float Sanitize(CheatStat stat, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Default(stat);
            return stat switch {
                CheatStat.BagCapacity => Mathf.Round(Mathf.Clamp(value, 1, 1000000)),
                CheatStat.ExtraJumps => Mathf.Round(Mathf.Clamp(value, 0, 100)),
                CheatStat.CameraFov => Mathf.Clamp(value, 30, 110),
                _ => Mathf.Clamp(value, .01f, 1000)
            };
        }
        public float Get(CheatStat stat) => (uint)stat < values.Length ? values[(int)stat] : 1;
        public bool Set(CheatStat stat, float value)
        {
            if ((uint)stat >= values.Length) return false;
            values[(int)stat] = Sanitize(stat, value); return true;
        }
        public void Reset() { foreach (CheatStat stat in Enum.GetValues(typeof(CheatStat))) values[(int)stat] = Default(stat); }
        public float[] Snapshot() => (float[])values.Clone();
        public void Apply(float[] snapshot)
        {
            Reset(); if (snapshot == null) return;
            for (int i = 0; i < Mathf.Min(values.Length, snapshot.Length); i++) Set((CheatStat)i, snapshot[i]);
        }
    }
}
