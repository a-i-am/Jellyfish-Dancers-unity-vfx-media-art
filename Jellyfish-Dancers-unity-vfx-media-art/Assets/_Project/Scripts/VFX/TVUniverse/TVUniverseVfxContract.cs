namespace Project.VFX.TVUniverse
{
    public static class TVUniverseVfxContract
    {
        public const string AttractorPosition = "AttractorPosition_position";
        public const string FlowVector = "FlowVector";
        public const string BurstStrength = "BurstStrength";
        public const string Energy = "Energy";
        public const string SpawnRate = "SpawnRate";
        public const string AttractorStrength = "AttractorStrength";
        public const string CrowdAttractorRadius = "CrowdAttractorRadius";
        public const string CrowdGravityEnabled = "CrowdGravityEnabled";

        public static string CrowdAttractorPosition(int index)
        {
            return "CrowdAttractor" + index + "_position";
        }

        public static string CrowdAttractorStrength(int index)
        {
            return "CrowdAttractor" + index + "Strength";
        }
    }
}
