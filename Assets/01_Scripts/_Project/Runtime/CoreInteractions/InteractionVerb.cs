namespace HuntGame.Interactions
{
    public enum InteractionVerb
    {
        Use     = 0,
        Open    = 1,
        Close   = 2,

        /// <summary>Press-and-hold start (e.g. grabbing a ragdoll). Paired with <see cref="Release"/>.</summary>
        Grab    = 3,

        /// <summary>Press-and-hold end, mirrors a prior <see cref="Grab"/>.</summary>
        Release = 4,
    }
}
