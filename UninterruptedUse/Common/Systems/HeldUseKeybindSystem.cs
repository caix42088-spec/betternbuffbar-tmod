using Terraria.ModLoader;

namespace UninterruptedUse.Common.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class HeldUseKeybindSystem : ModSystem
{
    public static ModKeybind ToggleProtectionKeybind { get; private set; }

    public override void Load()
    {
        ToggleProtectionKeybind = KeybindLoader.RegisterKeybind(Mod, "ToggleProtection", "F8");
    }

    public override void Unload()
    {
        ToggleProtectionKeybind = null;
    }
}
