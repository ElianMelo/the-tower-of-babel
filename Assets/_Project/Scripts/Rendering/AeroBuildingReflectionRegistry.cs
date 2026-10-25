using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    public interface IAeroReflectionDeckSource
    {
        bool FindReflectionDeck(Camera camera, Plane[] planes, float range, out int floor, out float height, out float floorHeight);
    }

    public interface IAeroReflectionDrawSource
    {
        int SubmitReflection(Camera camera, Camera owner, Plane[] planes, int floor, float range);
    }

    /// <summary>Gameplay supplies deck identities and existing instance pages; Rendering owns no gameplay state.</summary>
    public static class AeroBuildingReflectionRegistry
    {
        private static readonly HashSet<IAeroReflectionDeckSource> Decks = new();
        private static readonly HashSet<IAeroReflectionDrawSource> Draws = new();
        public static void Register(IAeroReflectionDeckSource source) => Decks.Add(source);
        public static void Unregister(IAeroReflectionDeckSource source) => Decks.Remove(source);
        public static void Register(IAeroReflectionDrawSource source) => Draws.Add(source);
        public static void Unregister(IAeroReflectionDrawSource source) => Draws.Remove(source);
        private static bool Alive(object value) => value != null && (!(value is Object obj) || obj != null);

        public static bool FindDeck(Camera camera, Plane[] planes, float range, out int floor, out float height, out float pitch)
        {
            floor = -1; height = float.NegativeInfinity; pitch = 6;
            foreach (var source in Decks)
                if (Alive(source) && source.FindReflectionDeck(camera, planes, range, out int f, out float h, out float p) && h > height)
                { floor = f; height = h; pitch = p; }
            return floor >= 0;
        }

        public static int Submit(Camera camera, Camera owner, Plane[] planes, int floor, float range)
        {
            int count = 0;
            foreach (var source in Draws) if (Alive(source)) count += source.SubmitReflection(camera, owner, planes, floor, range);
            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Decks.Clear(); Draws.Clear(); }
    }
}
