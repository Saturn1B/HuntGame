using UnityEngine;

namespace HuntGame.Interactions
{
    public readonly struct InteractionContext
    {
        public readonly Transform    Interactor;
        public readonly InteractionVerb Verb;
        public readonly bool         IsMultiplayer;
        public readonly bool         IsServer;
        public readonly ulong        InteractorClientId;

        private InteractionContext(
            Transform       interactor,
            InteractionVerb verb,
            bool            isMultiplayer,
            bool            isServer,
            ulong           interactorClientId)
        {
            Interactor         = interactor;
            Verb               = verb;
            IsMultiplayer      = isMultiplayer;
            IsServer           = isServer;
            InteractorClientId = interactorClientId;
        }

        /// <summary>Creates a context for a local (non-networked) interaction.</summary>
        public static InteractionContext Local(Transform interactor, InteractionVerb verb)
            => new(interactor, verb, isMultiplayer: false, isServer: false, interactorClientId: 0);

        /// <summary>Creates a context for a server-authoritative networked interaction.</summary>
        public static InteractionContext Server(Transform interactor, InteractionVerb verb, ulong clientId)
            => new(interactor, verb, isMultiplayer: true, isServer: true, interactorClientId: clientId);
    }
}
