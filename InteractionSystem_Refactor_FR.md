## Proposition de refonte légère du système d’interactions

Ce document propose une **refactorisation légère** du système d’interactions actuel pour le rendre :

- **plus simple à comprendre**,
- **proche du modèle** `IInteractable` + `PlayerInteractor`,
- **compatible solo** sans Netcode,
- et facilement extensible au **multijoueur** via un simple composant générique `NetcodeInteractableObject`.

Tout le code ci‑dessous est donné en **snippets C#** que tu peux adapter / intégrer dans le projet.

---

## Objectifs de la nouvelle architecture

- **API claire** : toute chose “interagissable” implémente une interface simple `IInteractable`.
- **Responsabilité du joueur** : le joueur possède un script `PlayerInteractor` qui :
  - fait un raycast,
  - détecte les objets interactifs,
  - déclenche l’interaction.
- **Solo et multi partagent la même logique** :
  - la logique métier (ouvrir une porte, appuyer sur un bouton, etc.) ne dépend **pas** directement de Netcode.
  - en solo, on appelle directement l’interactable localement.
  - en multi, un wrapper `NetcodeInteractableObject` se charge d’envoyer une **ServerRpc** au serveur, qui appelle ensuite la **même** méthode `Interact` que le solo.
- **Ajout simple du multijoueur** :
  - pour rendre un objet multi, on se contente d’ajouter :
    - `NetworkObject`,
    - `NetcodeInteractableObject`,
    - et potentiellement un composant de synchro (`NetworkTransform`, etc.).

---

## Types de base

### Enum `InteractionVerb`

Permet de décrire le type d’action (Use, Open, Close, etc.) :

```csharp
public enum InteractionVerb
{
    Use = 0,
    Open = 1,
    Close = 2,
    // Ajouter d'autres verbes si besoin (Inspect, PickUp, etc.)
}
```

### Struct `InteractionContext`

Contexte d’interaction passé à l’interactable. Il contient :

- qui interagit (`Transform Interactor`),
- quel verbe est utilisé (`InteractionVerb Verb`),
- si on est en multi ou non,
- si le code courant tourne côté serveur,
- le `ClientId` de l’interacteur en multi.

```csharp
using UnityEngine;

public readonly struct InteractionContext
{
    public readonly Transform Interactor;
    public readonly InteractionVerb Verb;
    public readonly bool IsMultiplayer;
    public readonly bool IsServer;
    public readonly ulong InteractorClientId;

    private InteractionContext(
        Transform interactor,
        InteractionVerb verb,
        bool isMultiplayer,
        bool isServer,
        ulong interactorClientId)
    {
        Interactor = interactor;
        Verb = verb;
        IsMultiplayer = isMultiplayer;
        IsServer = isServer;
        InteractorClientId = interactorClientId;
    }

    public static InteractionContext Local(Transform interactor, InteractionVerb verb)
    {
        return new InteractionContext(
            interactor,
            verb,
            isMultiplayer: false,
            isServer: true,
            interactorClientId: 0);
    }

    public static InteractionContext Server(
        Transform interactor,
        InteractionVerb verb,
        ulong interactorClientId)
    {
        return new InteractionContext(
            interactor,
            verb,
            isMultiplayer: true,
            isServer: true,
            interactorClientId: interactorClientId);
    }
}
```

### Interface `IInteractable`

Interface unique pour tous les objets interactifs :

- `CanInteract` permet de vérifier distance, cooldown, droits, etc.
- `Interact` exécute réellement l’action (ouvrir une porte, activer un levier…).

```csharp
public interface IInteractable
{
    bool CanInteract(in InteractionContext context);
    void Interact(in InteractionContext context);
}
```

---

## Interacteur côté joueur : `PlayerInteractor`

Ce composant va sur le **joueur** (ou la caméra joueur). Il :

- lit l’input (touche E par défaut),
- fait un raycast devant la caméra,
- en **mode multi** :
  - cherche un `NetcodeInteractableObject` sur l’objet touché,
  - demande à ce wrapper d’envoyer une ServerRpc au serveur.
- en **mode solo** (ou fallback si pas de Netcode pour cet objet) :
  - cherche un composant qui implémente `IInteractable`,
  - appelle directement `CanInteract` puis `Interact`.

```csharp
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public sealed class PlayerInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float rayDistance = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Input System")]
    [SerializeField] private Key useKey = Key.E;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Update()
    {
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // En mode réseau, seul le propriétaire lit l'input
        if (isNetworked && !IsOwner)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb[useKey].wasPressedThisFrame)
        {
            TryInteract(InteractionVerb.Use);
        }
    }

    private void TryInteract(InteractionVerb verb)
    {
        if (viewCamera == null)
        {
            Debug.LogError("[PlayerInteractor] viewCamera is not assigned.");
            return;
        }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            if (debugLogs) Debug.Log("[PlayerInteractor] Raycast hit nothing.");
            return;
        }

        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // 1) Mode multijoueur : on passe par un wrapper réseau générique
        if (isNetworked)
        {
            NetcodeInteractableObject netWrapper = hit.collider.GetComponentInParent<NetcodeInteractableObject>();
            if (netWrapper != null)
            {
                if (debugLogs) Debug.Log($"[PlayerInteractor] Hit networked interactable: {netWrapper.name}");
                netWrapper.RequestInteractFromClient(verb, NetworkObject);
                return;
            }
        }

        // 2) Fallback : interaction locale directe via IInteractable
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null)
        {
            if (debugLogs) Debug.Log("[PlayerInteractor] Hit object without IInteractable.");
            return;
        }

        if (debugLogs) Debug.Log($"[PlayerInteractor] Hit local interactable: {((MonoBehaviour)interactable).name}");

        var ctx = InteractionContext.Local(transform, verb);
        if (interactable.CanInteract(in ctx))
            interactable.Interact(in ctx);
    }
}
```

---

## Wrapper Netcode générique : `NetcodeInteractableObject`

Ce composant est ajouté **sur l’objet interactif** pour le rendre multijoueur. Il :

- exige un `NetworkObject` sur le même GameObject,
- récupère un composant implémentant `IInteractable` sur le même objet,
- expose une méthode publique `RequestInteractFromClient` :
  - utilisée par `PlayerInteractor` côté client,
  - qui envoie ensuite une `ServerRpc`,
  - la ServerRpc s’exécute côté serveur, construit un `InteractionContext.Server`, appelle `CanInteract` puis `Interact`.

Ainsi, la **logique de l’objet (porte, bouton, etc.) ne dépend pas de Netcode**.

```csharp
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetcodeInteractableObject : NetworkBehaviour
{
    private IInteractable _interactable;

    private void Awake()
    {
        _interactable = GetComponent<IInteractable>();
        if (_interactable == null)
        {
            Debug.LogError("[NetcodeInteractableObject] No IInteractable on the same GameObject.", this);
        }
    }

    /// <summary>
    /// Appelé côté client par PlayerInteractor après Raycast.
    /// </summary>
    public void RequestInteractFromClient(InteractionVerb verb, NetworkObject playerNetworkObject)
    {
        if (!IsSpawned || !NetworkManager.Singleton.IsListening)
        {
            // Pas de réseau actif : on exécute localement comme en solo
            if (_interactable != null)
            {
                var localCtx = InteractionContext.Local(playerNetworkObject.transform, verb);
                if (_interactable.CanInteract(in localCtx))
                    _interactable.Interact(in localCtx);
            }
            return;
        }

        // Client -> Serveur
        RequestInteractServerRpc(verb, playerNetworkObject);
    }

    [ServerRpc]
    private void RequestInteractServerRpc(
        InteractionVerb verb,
        NetworkObjectReference playerRef,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        if (_interactable == null)
            return;

        if (!playerRef.TryGet(out NetworkObject playerObj))
            return;

        // Sécurité : s'assurer que le client qui envoie contrôle bien ce NetworkObject
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (playerObj.OwnerClientId != senderId)
            return;

        var ctx = InteractionContext.Server(
            interactor: playerObj.transform,
            verb: verb,
            interactorClientId: senderId);

        if (_interactable.CanInteract(in ctx))
            _interactable.Interact(in ctx);
    }
}
```

---

## Exemple concret : porte qui s’ouvre

### `DoorInteractable` (implémente `IInteractable`)

Cette porte :

- a un pivot (`doorPivot`) qui tourne,
- utilise un angle d’ouverture `openAngle`,
- interpole la rotation sur `openCloseTime`,
- gère un **cooldown** et une **distance maximale** d’utilisation,
- fonctionne telle quelle en **solo**,
- fonctionne en **multi** dès qu’on ajoute `NetworkObject` + `NetcodeInteractableObject` (et éventuellement `NetworkTransform`).

```csharp
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField] private Transform doorPivot;    
    [SerializeField] private float openAngle = 90f;  
    [SerializeField] private float openCloseTime = 0.5f;
    [SerializeField] private float maxUseDistance = 3f;
    [SerializeField] private float useCooldownSeconds = 0.1f;

    private bool _isOpen;
    private bool _isAnimating;
    private float _lastUseTime;
    private Quaternion _closedRotation;
    private Quaternion _openRotation;

    private void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        _closedRotation = doorPivot.localRotation;
        _openRotation = _closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public bool CanInteract(in InteractionContext context)
    {
        if (context.Interactor == null)
            return false;

        // Cooldown
        if (Time.time - _lastUseTime < useCooldownSeconds)
            return false;

        // Distance maximale
        float dist = Vector3.Distance(context.Interactor.position, doorPivot.position);
        if (dist > maxUseDistance)
            return false;

        return true;
    }

    public void Interact(in InteractionContext context)
    {
        _lastUseTime = Time.time;

        if (_isAnimating)
            return;

        _isOpen = !_isOpen;
        StopAllCoroutines();
        StartCoroutine(AnimateDoor(_isOpen));
    }

    private IEnumerator AnimateDoor(bool open)
    {
        _isAnimating = true;

        Quaternion startRot = doorPivot.localRotation;
        Quaternion targetRot = open ? _openRotation : _closedRotation;

        float t = 0f;
        while (t < openCloseTime)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / openCloseTime);
            doorPivot.localRotation = Quaternion.Slerp(startRot, targetRot, lerp);
            yield return null;
        }

        doorPivot.localRotation = targetRot;
        _isAnimating = false;
    }
}
```

---

## Comment utiliser tout ça en pratique

### En solo (sans Netcode)

1. **Sur le joueur** :
   - Ajouter `PlayerInteractor`.
   - Assigner la caméra (champ `viewCamera`).
2. **Sur la porte** :
   - Ajouter `DoorInteractable`.
   - Assigner le pivot dans `doorPivot` (ou laisser `transform` si le pivot est le GameObject lui‑même).
3. **Lancer le jeu en mode solo** :
   - En regardant la porte et en appuyant sur `E`, `PlayerInteractor` fait un raycast,
   - trouve `DoorInteractable` (via `IInteractable`),
   - appelle `CanInteract` puis `Interact` en local.

### En multijoueur (avec Netcode)

1. **Sur le joueur** :
   - Garder `PlayerInteractor` (aucun changement).
2. **Sur la porte (objet racine)** :
   - Ajouter `NetworkObject`.
   - Ajouter `NetcodeInteractableObject`.
   - Garder `DoorInteractable` sur le même GameObject.
   - (Optionnel mais recommandé) Ajouter `NetworkTransform` ou équivalent pour synchroniser la rotation.
3. **En jeu multijoueur** :
   - Le client propriétaire du joueur presse `E`.
   - `PlayerInteractor` fait un raycast, trouve `NetcodeInteractableObject`.
   - `NetcodeInteractableObject.RequestInteractFromClient` envoie une `ServerRpc`.
   - Côté serveur, on construit un `InteractionContext.Server` et on appelle `DoorInteractable.Interact`.
   - La rotation est exécutée côté serveur, et répliquée aux clients via `NetworkTransform`.

---

## Migration à partir du système existant

L’idée n’est pas de tout jeter, mais de :

- **Simplifier les points d’entrée** :
  - Remplacer/adapter l’ancien `PlayerInteractor` par ce `PlayerInteractor` plus simple.
  - Remplacer les composants spécifiques Netcode (`NetInteractableAdapter`, etc.) par un wrapper **générique** `NetcodeInteractableObject`.
- **Recentrer la logique d’interaction** dans des composants `IInteractable` simples :
  - Pour chaque type d’objet (porte, bouton, coffre, etc.), créer une classe `XXXInteractable` qui implémente `IInteractable`.
  - Migrer progressivement les actions ScriptableObject existantes soit dans le code de ces interactables, soit dans un système secondaire si vous voulez garder l’approche data‑driven.

De cette façon :

- l’idée de base **`IInteractable` + `PlayerInteractor`** devient l’API principale,
- le multijoueur se limite à **un wrapper générique**,
- et la logique reste **unique et partagée** entre solo et multi.

