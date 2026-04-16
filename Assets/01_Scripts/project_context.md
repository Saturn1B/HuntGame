# HuntTheKiwi — project context
_Generated: 2026-04-16_

---

## Stack

| Layer | Technology |
|---|---|
| Engine | Unity 6 |
| Multiplayer | Unity Netcode for GameObjects (NGO) |
| Transport | FacepunchTransport (Steam P2P) |
| Platform | Steamworks.NET via Facepunch.Steamworks |
| Input | Unity Input System (PlayerInput + callbacks) |
| Animation | Animator + NetworkVariables for remote sync |
| Procedural gen | Custom socket-based dungeon generator + SAT overlap test |
| Traps/FX | DOTween |
| Editor tooling | Sirenix OdinInspector (AI + ProceduralGeneration) |
| Voice | Steam Voice API, custom NGO CustomMessaging relay |
| UI | TextMeshPro |

---

## Namespace map

| Namespace | Purpose |
|---|---|
| _(none)_ | Legacy player scripts: CharacterMovement, FirstPersonCamera, PlayerInputHandler, SimplePlayerInteractor |
| `HuntGame.Player` | HeadBobbing, PlayerHeadIK, IMovable, ILookable |
| `HuntGame.Interactions` | IInteractable, InteractionContext, InteractionVerb, PlayerInteractor, NetcodeInteractableObject, all Interactable components |
| `HuntGame` | PlayerCubePusher |
| `HuntingGame` | Health |
| `HuntingGame.AI` | AIInputHandler, BoilBackAI, CeilingAI, PopCockAI, Detector, ConeDetector, WanderingBehaviour, RagdollController, IKFootSolver |
| `HuntingGame.Gameplay` | DamageZone, BladeTrap, SpikeTrap, TriggerDetection |
| `HuntingGame.ProceduralGeneration` | DungeonGenerator, LoopPregenerator, LoopViewer, Room, RoomData, Socket, SocketType, LoopData |
| `DungeonSteakhouse.Net` | NetGameRoot, NetGameState, NetDemoUI |
| `DungeonSteakhouse.Net.Connection` | NetConnectionApproval, NetConnectionPayload, NetConnectionPayloadCodec |
| `DungeonSteakhouse.Net.Core` | INetBootstrapper, INetIdentityProvider, INetLobbyController, NetLocalIdentity |
| `DungeonSteakhouse.Net.Players` | NetPlayer, NetPlayerRegistry, NetFpsPlayerAdapter, NetLocalCameraEnforcer |
| `DungeonSteakhouse.Net.Session` | NetSessionManager, NetSessionState, NetSpawnContext, NetReadyPlatformGate, NetPlayerTeleporter, NetPlayerSpawnResolver, NetSpawnPointGroup, ElevatorButtonInteractable, ElevatorEffectsController, ElevatorSceneLoader, ReturnToLobbyInteractable |
| `DungeonSteakhouse.Net.Steam` | SteamBootstrap, SteamIdentityProvider, SteamLobbyNetcode |
| `DungeonSteakhouse.Net.Voice` | NetSteamProximityVoiceChat, SteamVoiceSpeaker, MouthBlendshapeTalkAnimator, VoiceTalkIndicator, NetSteamVoiceConfig |
| `DungeonSteakhouse.Net.UI` | NetLobbyRosterTMP, NetSessionUI |

---

## Key classes (one line each)

**Player / FPS**
- `CharacterMovement` — CharacterController-based movement with crouch, sprint, jump, drag-weight slowdown; implements IMovable + ILookable.
- `FirstPersonCamera` — Yaw/pitch look with drag-weight sensitivity reduction.
- `PlayerInputHandler` — Unity Input System bridge; routes OnMove/OnCamera/OnSprint/OnCrouch/OnJump/OnInteract to components.
- `HeadBobbing` — Camera head-bob driven by CharacterController velocity.
- `PlayerInteractor` — Raycast-based interaction dispatcher; routes to NetcodeInteractableObject (net) or IInteractable (local).
- `SimplePlayerInteractor` — Legacy raycast interactor for ISimpleInteractable (pre-net system, used by singleplayer ragdoll grab).

**Interaction system (production)**
- `IInteractable` — Interface: CanInteract(InteractionContext), Interact(InteractionContext).
- `InteractionContext` — Readonly struct carrying Interactor, Verb, IsMultiplayer, IsServer, InteractorClientId.
- `InteractionVerb` — Enum: Use, Open, Close.
- `NetcodeInteractableObject` — NetworkBehaviour wrapper for IInteractable; local prediction + ServerRpc validation + ClientRpc broadcast.
- `ChestInteractable` — IInteractable: animated lid open/close with UnityEvent hooks.
- `DoorInteractable` — IInteractable: pivot-based door swing, server context bypasses client-side guards.
- `LeverInteractable` — IInteractable: toggle lever with UnityEvent hooks.
- `PickupInteractable` — IInteractable: physics pick-up/drop using HeldItemSocket.
- `HeldItemSocket` — Tracks held item and lerps it to socket offset each frame (no SetParent, NetworkObject-safe).

**Networking core**
- `NetGameRoot` — Singleton MonoBehaviour; owns NetworkManager ref, config, PlayerRegistry, IdentityProvider; exposes Host()/Shutdown().
- `NetGameConfig` — ScriptableObject; max players, build version, scene names, late-join flag.
- `NetConnectionApproval` — Server-side NGO approval: build version gate + max-player gate + late-join gate.
- `NetConnectionPayload / NetConnectionPayloadCodec` — JSON struct sent as connection data (buildVersion, platformUserId, displayName).
- `INetBootstrapper` — Interface for platform init (IsReady, Initialize, Shutdown).
- `INetIdentityProvider` — Interface: TryGetLocalIdentity(out NetLocalIdentity).
- `INetLobbyController` — Interface: Host(), LeaveLobby(), TryGetSessionId().

**Player networking**
- `NetPlayer` — NetworkBehaviour; NetworkVariables for PlatformUserId, DisplayName, IsReady; owner submits identity via ServerRpc.
- `NetPlayerRegistry` — MonoBehaviour list of NetPlayers; fires PlayerAdded/Removed/Updated events; exposes AreAllReady.
- `NetFpsPlayerAdapter` — NetworkBehaviour; enables/disables FPS components (input, camera, movement, audio listener) based on IsOwner.
- `NetLocalCameraEnforcer` — NetworkBehaviour; ensures only owner camera is active across additive scene loads; re-enforces for N seconds after SceneLoaded.
- `PlayerAnimationController` — NetworkBehaviour; owner writes velocity/grounded/bodyYaw/headYaw/headPitch to NetworkVariables; non-owners read and drive Animator + PlayerHeadIK.
- `PlayerHeadIK` — Manual IK: owner reads from camera transform; non-owners set via SetRemoteTarget.
- `PlayerCubePusher` — Owner-only CharacterController collision → ServerRpc → AddForce on NetworkObject Rigidbody.

**Session flow**
- `NetSessionManager` — NetworkBehaviour singleton; state machine (Lobby→StartingRun→InRun→ReturningToLobby); drives NGO additive scene load/unload; orchestrates teleports.
- `NetSessionState` — Enum byte: Lobby, StartingRun, InRun, ReturningToLobby.
- `NetReadyPlatformGate` — Server-side trigger collider; tracks per-client overlap count, fires AllReadyConfirmedServer after countdown.
- `NetPlayerTeleporter` — Teleports all or specific clients to spawn points; deferred retry for late-joining clients.
- `NetPlayerSpawnResolver` — Finds NetSpawnPointGroup by context (Lobby/Run), returns spawn position for a clientId.
- `NetSpawnPointGroup` — Transform[] spawn points for a given NetSpawnContext; deterministic index via clientId modulo.
- `ElevatorButtonInteractable` — IInteractable; calls RequestStartRunServerRpc when state is Lobby.
- `ReturnToLobbyInteractable` — IInteractable; calls RequestReturnToLobbyServerRpc when state is InRun.
- `ElevatorEffectsController` — Listens to NetSessionManager.StateChanged; triggers door/shake animations.
- `ElevatorSceneLoader` — Loads Elevator scene additively on Start if not already loaded.

**Steam / transport**
- `SteamBootstrap` — Initialises SteamClient (optional), waits for SteamClient.IsValid, exposes static Ready + ReadyChanged.
- `SteamIdentityProvider` — Implements INetIdentityProvider from Steamworks SteamClient.SteamId + SteamClient.Name.
- `SteamLobbyNetcode` — Implements INetLobbyController; creates/joins Steam lobby, sets transport.targetSteamId, calls NetworkManager.StartHost/Client.

**Voice**
- `NetSteamProximityVoiceChat` — Captures Steam voice, sends to server via CustomMessaging, server relays to all clients; clients decode and push to SteamVoiceSpeaker.
- `NetSteamVoiceConfig` — ScriptableObject; all voice tuning params (buffer, jitter, spatialization, sample rate).
- `SteamVoiceSpeaker` — Streaming AudioClip ring buffer per player; receives PCM samples from NetSteamProximityVoiceChat.
- `MouthBlendshapeTalkAnimator` — Drives SkinnedMeshRenderer blendshapes from PingTalking() / SetTalkLevel01().
- `VoiceTalkIndicator` — Sprite-swap mouth open/closed based on PingTalking() timeout.

**AI**
- `AIInputHandler` — Base class: NavMeshAgent path → CharacterMovement input bridge; handles death→ragdoll.
- `BoilBackAI` — Wander/Flee/Hide state machine; flees from player detected by Detector.
- `CeilingAI` — Extends AIInputHandler; flips gravity when far from target and ceiling is present.
- `PopCockAI` — Wander/Chase/Idle + explosion suicide at close range.
- `Detector` (abstract) — Timed detection loop, fires _onPlayerSpotted/_onPlayerLost.
- `ConeDetector` — OverlapSphere + angle + LOS raycast; extends Detector.
- `WanderingBehaviour` — Async NavMesh random target generator; exposes isMoving and currentTarget via property-events.
- `RagdollController` — Per-bone Rigidbody enable/disable + ConfigurableJoint grab (ISimpleInteractable — legacy system).
- `IKFootSolver` — Per-foot procedural IK using raycasting; supports gait synchronization with opposedLegs.

**Gameplay**
- `Health` — Simple HP + _onChangeHealthValue + _onDeath events. **Bug: else branch modifies `healthPoint` not `currentHealthPoint`.**
- `DamageZone` — OnTriggerEnter → Health.ChangeHealth.
- `BladeTrap / SpikeTrap` — DOTween animations triggered by TriggerDetection events.
- `TriggerDetection` — Tag-filtered trigger collider that fires _onTriggerEnter/_onTriggerExit.

**Procedural generation**
- `DungeonGenerator` — Socket-based dungeon builder: ghost pool, weighted room placement, SAT overlap test, loop and bridge detection.
- `RoomData` — ScriptableObject: prefab, weight, accepted socket types.
- `Room` — MonoBehaviour: sockets array, 2D footprint for SAT.
- `Socket` — Connection point on a room: SocketType (SMALL/MEDIUM/LARGE), open/closed state, barricade activation.
- `LoopData` — ScriptableObject: ordered list of rooms + relative poses for a pre-baked loop.
- `LoopPregenerator` — Editor-only async DFS that discovers loops and saves them as LoopData assets.
- `LoopViewer` — Editor preview of a LoopData asset in the scene.

---

## Dependency graph (critical paths only)

```
SteamBootstrap ──────────────────► SteamIdentityProvider
                                   SteamLobbyNetcode
                                        │
                               NetworkManager.StartHost/Client
                                        │
NetGameRoot ◄────────────────────────── │
    │ (Config, PlayerRegistry,          │
    │  IdentityProvider, LobbyCtrl)     │
    ▼                                   │
NetPlayer → NetPlayerRegistry → NetLobbyRosterTMP
                │
                ▼
        NetConnectionApproval → NetSessionManager
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                  ▼
          NetReadyPlatformGate  NetPlayerTeleporter  Scene Load/Unload
                                      │
                              NetPlayerSpawnResolver
                                      │
                              NetSpawnPointGroup

PlayerInputHandler → CharacterMovement (IMovable)
                   → FirstPersonCamera (SetLookInput)
                   → SimplePlayerInteractor → ISimpleInteractable

NetFpsPlayerAdapter → [enables/disables above on IsOwner]

PlayerInteractor → NetcodeInteractableObject → IInteractable (DoorInteractable, etc.)
                 → IInteractable (local path)

AIInputHandler ──► CharacterMovement (SetMovementInput)
BoilBackAI/PopCockAI ──► WanderingBehaviour + ConeDetector

NetSteamProximityVoiceChat ──► SteamVoiceSpeaker ──► MouthBlendshapeTalkAnimator
                                                   └► VoiceTalkIndicator
```

---

## Active code smells (high severity only)

**1. Duplicated dungeon geometry code**
`AlignRooms`, `IsOverlapping`, `PolygonsOverlap`, `Project`, and `GetWorldFootprint` are copy-pasted verbatim into both `DungeonGenerator` and `LoopPregenerator` (~120 lines each).
_Fix_: Extract `DungeonGeometryUtils` static class.

**2. Dual interaction systems**
`ISimpleInteractable` + `SimplePlayerInteractor` (legacy, still used by `RagdollController`) coexist with the production `IInteractable` + `InteractionContext` + `PlayerInteractor` system. Having both causes confusion about which path to extend.
_Fix_: Migrate `RagdollController` to `IInteractable`, delete `ISimpleInteractable`.

**3. RagdollController reaches into player subsystems directly**
`StartInteract` calls `CharacterMovement.SetSlowingWeight` and `FirstPersonCamera.SetSlowingWeight` on the grabber via GetComponent — coupling AI death to player internals.
_Fix_: Introduce `ISlowable` interface.

---

## SRP violations (medium+ severity only)

| Class | Severity | Violation summary |
|---|---|---|
| `DungeonGenerator` | **high** | Generation algorithm + ghost pooling + loop detection + SAT math all in one 600-line class |
| `LoopPregenerator` | **high** | Editor tooling + async DFS + room pooling + SAT math; duplicates DungeonGenerator |
| `NetSessionManager` | medium | Session state machine + NGO scene loading + scene event routing + player teleport orchestration |
| `NetSteamProximityVoiceChat` | medium | Steam capture + server relay + client decompression + distance culling |
| `AIInputHandler` | medium | NavMesh movement + ragdoll death sequence + input bridging |
| `RagdollController` | medium | Ragdoll physics + player grab interaction + drag-weight side-effects on grabber |

---

## Refactoring backlog (priority order)

1. **[CRITICAL / trivial]** Fix `Health.ChangeHealth` bug — `healthPoint += value` → `currentHealthPoint += value`. HP does not decrement correctly.

2. **[HIGH / low effort]** Extract `DungeonGeometryUtils` — removes ~120 lines of duplication between DungeonGenerator and LoopPregenerator.

3. **[HIGH / medium]** Migrate `ISimpleInteractable` → `IInteractable` — unify both interaction pipelines, delete legacy code.

4. **[MEDIUM / low]** Introduce `ISlowable` — decouple RagdollController grab from CharacterMovement and FirstPersonCamera.

5. **[MEDIUM / high]** Split `DungeonGenerator` — DungeonGhostPool, DungeonRoomPlacer, DungeonLoopPlacer, DungeonOverlapTester.

6. **[LOW / low]** Add namespaces to root-level player scripts (CharacterMovement, FirstPersonCamera, PlayerInputHandler, SimplePlayerInteractor) → `HuntGame.Player`.

7. **[LOW / trivial]** Wrap `NativeArray.Dispose` in try/finally in `NetcodeInteractableObject.RequestInteractServerRpc`.

8. **[LOW / low]** Fix `ElevatorEffectsController.CameraShakeRoutine` — inject camera reference instead of using `Camera.main` (unreliable in additive multiplayer scenes).

---

## Patterns in use

- **Owner-authoritative + server-broadcast interaction**: `NetcodeInteractableObject` does local prediction on the interacting client, ServerRpc validation, then ClientRpc broadcast to all others.
- **Adapter pattern for multiplayer FPS**: `NetFpsPlayerAdapter` wraps unchanged singleplayer components without modifying them.
- **Ghost pool for procedural generation**: `DungeonGenerator` maintains inactive ghost GameObjects for collision testing before instantiating real rooms.
- **Strategy via interface injection**: `NetGameRoot` holds `INetBootstrapper`, `INetIdentityProvider`, `INetLobbyController` as serialised MonoBehaviour fields — easy to swap implementation.
- **Command pattern for AI locomotion**: `AIInputHandler` drives `CharacterMovement` through `SetMovementInput` — AI and player share the same locomotion system.
- **Event-based decoupling**: `WanderingBehaviour._onMovingChanged`, `NetPlayerRegistry.PlayerAdded/Removed/Updated`, `NetSessionManager.StateChanged`, `Detector._onPlayerSpotted` — events rather than direct polling.
- **NetworkVariable for animation sync**: `PlayerAnimationController` uses 6 NetworkVariables (velocity, grounded, bodyYaw, headYaw, headPitch) instead of an AnimatorNetworkSync component.
- **Additive scene architecture**: Elevator scene is persistent; Tavern and Dungeon are additively loaded/unloaded by `NetSessionManager`. `NetLocalCameraEnforcer` re-enforces camera ownership after each load.
- **SAT (Separating Axis Theorem)** for 2D polygon overlap in dungeon generation.
- **Weighted random room selection** with roomWeight on RoomData.
- **Async DFS** in LoopPregenerator for pre-baking loop configurations.

---

## What NOT to suggest

- Do not suggest rewriting the FPS locomotion with Rigidbody — the project explicitly uses CharacterController and the AI reuses it.
- Do not suggest NGO AnimatorNetworkSync component — the project deliberately uses custom NetworkVariables for animation to have fine-grained control.
- Do not suggest replacing Steam P2P transport with Unity Relay — the project is Steam-exclusive (FacepunchTransport, SteamLobbyNetcode).
- Do not suggest re-parenting held items via SetParent — HeldItemSocket explicitly avoids this for NetworkObject compatibility (comment in code).
- Do not suggest moving NetSessionManager out of DontDestroyOnLoad — it must survive scene transitions by design.
- Do not suggest removing the ghost pool in DungeonGenerator — it was added to avoid Instantiate/Destroy spam during generation; it is intentional.
- Do not suggest replacing DOTween for trap animations — it is already integrated and in use.
- Do not suggest adding Odin Inspector to net/session code — Odin is scoped to AI and ProceduralGeneration intentionally.
- Do not suggest synchronising the dungeon procedural generation via NGO — the current design generates server-side and players are teleported in; network-sync of generation is out of scope.
