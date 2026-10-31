namespace TowerOfBabel.Resources
{
    /// <summary>Byte values are network and save identities: append new entries, never renumber.</summary>
    public enum ResourceType : byte
    {
        Stone = 0,
        ProcessedStone = 1,
        Sand = 2,
        Glass = 3,
        RiverEssence = 4,
        RiverBlock = 5,
        WaterEssence = 6,
        WaterBlock = 7,
        SkyEssence = 8,
        SkyBlock = 9
    }

    public static class ResourceTypeNames
    {
        /// <summary>Readable name for text that only has the type, such as build costs.</summary>
        public static string Display(ResourceType type) => type switch
        {
            ResourceType.ProcessedStone => "Processed Stone",
            ResourceType.RiverEssence => "River Essence",
            ResourceType.RiverBlock => "River Block",
            ResourceType.WaterEssence => "Water Essence",
            ResourceType.WaterBlock => "Water Block",
            ResourceType.SkyEssence => "Sky Essence",
            ResourceType.SkyBlock => "Sky Block",
            _ => type.ToString()
        };
    }
}
