using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterMovement))]
public class AIInputHandler : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] protected Transform target;
    [SerializeField] protected float stoppingDistance = 2;

    protected NavMeshAgent agent;
    protected CharacterMovement movementController;
}
