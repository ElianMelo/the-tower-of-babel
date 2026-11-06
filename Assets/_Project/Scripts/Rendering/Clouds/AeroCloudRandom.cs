using UnityEngine;

namespace TowerOfBabel.Rendering
{
    public static class AeroCloudRandom
    {
        public static float Range(System.Random random, Vector2 range)
            => Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());

        /// <summary>Weighted pick; null when nothing has a positive weight.</summary>
        public static T Pick<T>(System.Random random, System.Collections.Generic.IList<T> items, System.Func<T, float> weight) where T : class
        {
            if (items == null) return null;
            float total = 0;
            foreach (var item in items) if (item != null) total += Mathf.Max(0, weight(item));
            if (total <= 0) return null;
            float roll = (float)random.NextDouble() * total;
            T last = null;
            foreach (var item in items)
            {
                if (item == null || weight(item) <= 0) continue;
                last = item;
                roll -= weight(item);
                if (roll < 0) return item;
            }
            return last;
        }
    }
}
