using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>
    /// Decides when trying to move cancels a timed action (gathering, processing, building). Turning the camera never
    /// does. Movement keys already held when the action began are ignored until released, so walking up to a node and
    /// pressing interact does not cancel the action straight away.
    /// </summary>
    public struct InteractionMovementCancel
    {
        public const float Deadzone = 0.2f;

        private bool ignoreHeldMovement;

        public static bool IsMoving(Vector2 move) => move.sqrMagnitude > Deadzone * Deadzone;

        /// <summary>Starts watching; <paramref name="move"/> is the movement input at that moment.</summary>
        public void Begin(Vector2 move) => ignoreHeldMovement = IsMoving(move);

        /// <summary>True when this frame's input should cancel the action.</summary>
        public bool ShouldCancel(Vector2 move, bool jumpPressed)
        {
            bool moving = IsMoving(move);
            if (!moving)
                ignoreHeldMovement = false;
            return jumpPressed || (moving && !ignoreHeldMovement);
        }
    }
}
