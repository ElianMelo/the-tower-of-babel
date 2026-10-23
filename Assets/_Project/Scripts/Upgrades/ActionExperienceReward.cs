using UnityEngine;

namespace TowerOfBabel.Upgrades
{
    /// <summary>A receipt for a completed, server-authorized action. Visuals never grant XP.</summary>
    public readonly struct ActionExperienceReward
    {
        public readonly UpgradeJob Job;
        public readonly Vector3 WorldPosition;
        public readonly string Action;
        public readonly int PreviousLevel, PreviousExperience, Level, Experience;

        public ActionExperienceReward(UpgradeJob job, Vector3 worldPosition, string action,
            int previousLevel, int previousExperience, int level, int experience)
        {
            Job = job;
            WorldPosition = worldPosition;
            Action = action;
            PreviousLevel = previousLevel;
            PreviousExperience = previousExperience;
            Level = level;
            Experience = experience;
        }

        public int PreviousTotal => Total(PreviousLevel, PreviousExperience);
        public int TotalExperience => Total(Level, Experience);
        public int Amount => Mathf.Max(0, TotalExperience - PreviousTotal);
        public static int Total(int level, int experience) => level * (level + 1) * 5 + experience;
    }
}
