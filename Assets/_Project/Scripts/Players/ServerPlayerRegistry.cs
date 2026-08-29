using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Players
{
    /// <summary>Authoritative connected-player data owned by the server main thread.</summary>
    public sealed class ServerPlayerRegistry
    {
        private readonly Dictionary<uint, PlayerInstance> players = new();

        public int Count => players.Count;
        public IEnumerable<PlayerInstance> Players => players.Values;

        public PlayerInstance Register(uint playerId, Vector3 position, Quaternion rotation)
        {
            if (players.TryGetValue(playerId, out PlayerInstance existing))
                return existing;

            PlayerInstance instance = new(playerId, position, rotation);
            players.Add(playerId, instance);
            return instance;
        }

        public bool ApplySnapshot(uint authenticatedPlayerId, PlayerStateSnapshot claimedSnapshot,
            out PlayerInstance instance)
        {
            if (!players.TryGetValue(authenticatedPlayerId, out instance))
                instance = Register(authenticatedPlayerId, claimedSnapshot.Position, claimedSnapshot.Rotation);

            claimedSnapshot.PlayerId = authenticatedPlayerId;
            return instance.ApplySnapshot(claimedSnapshot);
        }

        public bool TryGet(uint playerId, out PlayerInstance instance) =>
            players.TryGetValue(playerId, out instance);

        public bool Unregister(uint playerId)
        {
            if (!players.Remove(playerId, out PlayerInstance instance))
                return false;
            instance.MarkDisconnected();
            return true;
        }

        public void CopySnapshots(List<PlayerStateSnapshot> results)
        {
            results.Clear();
            foreach (PlayerInstance player in players.Values)
                results.Add(player.CurrentSnapshot);
        }

        public void Clear()
        {
            foreach (PlayerInstance instance in players.Values)
                instance.MarkDisconnected();
            players.Clear();
        }
    }
}
