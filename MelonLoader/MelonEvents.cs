namespace MelonLoader
{
    public static class MelonEvents
    {
        public readonly static MelonEvent OnPreInitialization = new(true);

        public readonly static MelonEvent OnApplicationEarlyStart = new(true);

        public readonly static MelonEvent OnPreSupportModule = new(true);

        public readonly static MelonEvent OnApplicationStart = new(true);

        public readonly static MelonEvent OnApplicationLateStart = new(true);

        public readonly static MelonEvent OnApplicationDefiniteQuit = new(true);

        public readonly static MelonEvent OnApplicationQuit = new();

        public readonly static MelonEvent OnUpdate = new();

        public readonly static MelonEvent OnFixedUpdate = new();

        public readonly static MelonEvent OnLateUpdate = new();

        public readonly static MelonEvent OnGUI = new();

        public readonly static MelonEvent<int, string> OnSceneWasLoaded = new();

        public readonly static MelonEvent<int, string> OnSceneWasInitialized = new();

        public readonly static MelonEvent<int, string> OnSceneWasUnloaded = new();

        public readonly static MelonEvent OnPreModsLoaded = new(true);

        internal readonly static MelonEvent MelonHarmonyEarlyInit = new(true);
        internal readonly static MelonEvent MelonHarmonyInit = new(true);
    }
}
