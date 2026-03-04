using UnityEngine;

[CreateAssetMenu(menuName = "NetGame/Interaction/Actions/Elevator Button")]
public class ElevatorButtonAction : InteractionAction
{
    [SerializeField] private NetGameServerEventType eventType = NetGameServerEventType.StartRun;

    public override void Execute(in InteractionContext ctx)
    {
        Debug.Log($"[Server] ElevatorButton '{ctx.Target.name}' -> {eventType} (client {ctx.InteractorClientId}).");

        var evt = new NetGameServerEvent(eventType, ctx.InteractorClientId, ctx.Target, ctx.Interactor);
        NetGameServerEventBus.Raise(in evt);
    }
}