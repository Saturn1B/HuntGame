using UnityEngine;

namespace HuntGame.Player
{
    public interface IMovable
    {
        void SetMovementInput(Vector2 movementInput);
        void SetSprinting(bool isSprinting);
        void SetCrouching(bool isCrouching);
        void Jump();
        void SetCanMove(bool canMove);
    }

    public interface ILookable
    {
        void SetLookInput(Vector2 lookInput);
    }

    /// <summary>
    /// Implemented by player components whose sensitivity/speed can be reduced by an external
    /// effect (e.g. RagdollController dragging a grabbed creature), without that effect needing
    /// to know the concrete component types.
    /// </summary>
    public interface ISlowable
    {
        void SetSlowingWeight(bool dragging, float weight);
    }
}
