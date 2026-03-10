using UnityEngine;
using DungeonSteakhouse.Net.Session;

namespace DungeonSteakhouse.Net.Interactions.Actions
{
    /// <summary>
    /// Single button action that:
    /// - In Hub: tries to start the run
    /// - In Run: tries to return to hub
    /// </summary>
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Interaction/Actions/Elevator Session Auto Command")]
    public sealed class ElevatorSessionAutoCommandAction : InteractionAction
    {
        [SerializeField] private NetSessionAutoCommand command = NetSessionAutoCommand.Auto;
        [SerializeField] private bool logWhenDenied = true;

        public override bool CanExecute(in InteractionContext ctx)
        {
            var controller = NetRunSessionController.Instance;
            if (controller == null)
                return false;

            return controller.CanExecuteServerCommand(command, out _);
        }

        public override void Execute(in InteractionContext ctx)
        {
            var controller = NetRunSessionController.Instance;
            if (controller == null)
            {
                if (logWhenDenied)
                    Debug.LogWarning("[Server] ElevatorSessionAutoCommandAction denied: NetRunSessionController.Instance is null.");
                return;
            }

            if (!controller.CanExecuteServerCommand(command, out string reason))
            {
                if (logWhenDenied)
                    Debug.LogWarning($"[Server] ElevatorSessionAutoCommandAction denied: {reason}");
                return;
            }

            controller.ServerExecuteCommand(ctx.InteractorClientId, command);
        }
    }
}
