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
}
