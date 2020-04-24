using System.Collections.Generic;

namespace StardewHypnos
{
    enum MinimumRelationshipType
    {
        OnlyPartners,
        Friends,
        Everyone
    }

    class ModConfig
    {
        // Minimum "relationship" tier to benefit from entering friend / dating / ... NPC homes at any time
        public MinimumRelationshipType MinimumRelationship { get; set; } = MinimumRelationshipType.OnlyPartners;

        // Minimum hearts with a NPC to benefit from entering their house at all times (for Friends)
        public int MinimumFriendshipHearts { get; set; } = 6;

        // Open doors at all times for locations owned by NPCs with enough relation?
        public bool KeepFriendDoorsOpen { get; set; } = true;

        // Additional warps and their NPC owners
        public Dictionary<string, List<string>> CustomOwnerByWarp { get; set; }

        // Blacklisted warps
        public List<string> BlacklistedWarps { get; set; }
    }
}
