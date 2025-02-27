namespace SimpleLib.Files
{
    internal static class AutoFileRegisterer
    {
        public static void RegisterDefault(FileRegistry registry)
        {
            registry.SetId("Engine/Shaders/Missing.hlsl", EngineShadersMissingHlsl);
            registry.SetId("Engine/Materials/Missing.material", EngineMaterialsMissingMaterial);
            registry.SetId("Engine/Textures/Loading.png", EngineTexturesLoadingPng);
            registry.SetId("Engine/Shaders/sIMGUI.hlsl", EngineShadersSIMGUIHlsl);
            registry.SetId("Engine/Materials/sIMGUI.material", EngineMaterialsSIMGUIMaterial);
        }

        public const ulong EngineShadersMissingHlsl = 0UL;
        public const ulong EngineMaterialsMissingMaterial = 1UL;
        public const ulong EngineTexturesLoadingPng = 2UL;
        public const ulong EngineShadersSIMGUIHlsl = 3UL;
        public const ulong EngineMaterialsSIMGUIMaterial = 4UL;
    }
}
