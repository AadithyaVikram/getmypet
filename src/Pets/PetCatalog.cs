using System.Collections.Generic;

namespace DragonBreak;

// Add future pet definitions here. Selection and persistence can be introduced when a second pet ships.
internal static class PetCatalog
{
    public static IPetDefinition Default { get; } = new DragonPet();
    public static IReadOnlyList<IPetDefinition> All { get; } = new[] { Default };
}