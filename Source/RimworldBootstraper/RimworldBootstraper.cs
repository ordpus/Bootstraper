using RimWorld;
using Verse;

namespace RimworldBootstraper;

[StaticConstructorOnStartup]
public static class RimworldBootstraper {
    static RimworldBootstraper() {
        LongEventHandler.ExecuteWhenFinished(() => {
            Log.Message($"Field: {typeof(AbilityComp).GetField("myField")?.Name}");
        });
    }
}