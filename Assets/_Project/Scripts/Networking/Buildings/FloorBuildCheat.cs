using TowerOfBabel.Cheats;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;

namespace TowerOfBabel.Networking.Buildings
{
    /// <summary>
    /// The three floor build cheats. Each targets the next unfinished floor: the entire floor builds
    /// every phase, structure builds only structure, and detail needs the structure already built
    /// because detail is only released once a floor's structure is complete.
    /// </summary>
    public static class FloorBuildCheat
    {
        public static bool IsFloorBuild(CheatCommand command) =>
            command is CheatCommand.BuildFloor or CheatCommand.BuildFloorStructure or CheatCommand.BuildFloorDetail;

        public static TowerConstructionPhase? PhaseOf(CheatCommand command) => command switch
        {
            CheatCommand.BuildFloorStructure => TowerConstructionPhase.Structure,
            CheatCommand.BuildFloorDetail => TowerConstructionPhase.Detail,
            _ => null
        };

        public static bool TryResolve(ChunkManager chunks, CheatCommand command, out int floor, out string failure)
        {
            floor = -1; failure = null;
            if (chunks == null || !IsFloorBuild(command)) { failure = $"{command}: no tower in this scene."; return false; }
            int? active = chunks.ActiveConstructionFloor;
            if (!active.HasValue) { failure = "Every floor is already built."; return false; }
            floor = active.Value;
            bool structureBuilt = chunks.IsStructureComplete(floor);
            if (command == CheatCommand.BuildFloorStructure && structureBuilt)
            { failure = $"Floor {floor + 1} structure is already built - use Instantly build floor detail."; return false; }
            if (command == CheatCommand.BuildFloorDetail && !structureBuilt)
            { failure = $"Floor {floor + 1} detail needs its structure first - use Instantly build floor structure."; return false; }
            return true;
        }
    }
}
