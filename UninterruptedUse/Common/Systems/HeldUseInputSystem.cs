using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using UninterruptedUse.Common.Players;

namespace UninterruptedUse.Common.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class HeldUseInputSystem : ModSystem
{
    private bool copyHookInstalled;
    private bool updateHookInstalled;
    private bool hotbarHookInstalled;

    public override void Load()
    {
        IL_TriggersSet.CopyInto += PreserveUseItemInput;
        copyHookInstalled = true;
        IL_Player.Update += PreventHoverReleaseLock;
        updateHookInstalled = true;
        On_Main.GUIHotbarDrawInner += PreventHeldHotbarSelection;
        hotbarHookInstalled = true;
    }

    public override void Unload()
    {
        if (hotbarHookInstalled)
            On_Main.GUIHotbarDrawInner -= PreventHeldHotbarSelection;
        if (updateHookInstalled)
            IL_Player.Update -= PreventHoverReleaseLock;
        if (copyHookInstalled)
            IL_TriggersSet.CopyInto -= PreserveUseItemInput;

        copyHookInstalled = updateHookInstalled = hotbarHookInstalled = false;
    }

    public override void PostUpdateInput()
    {
        // This runs during menus/pauses too, so they cancel an existing hold.
        Main.LocalPlayer.GetModPlayer<HeldUsePlayer>().UpdateInput();
    }

    private static void PreserveUseItemInput(ILContext il)
    {
        var cursor = new ILCursor(il);

        // Only bypass the UI test for Use Item. The right-click path stays vanilla.
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchLdarg(1),
            i => i.MatchLdfld<Player>(nameof(Player.mouseInterface)),
            i => i.OpCode == OpCodes.Brtrue || i.OpCode == OpCodes.Brtrue_S,
            i => i.MatchLdarg(1),
            i => i.MatchLdcI4(1),
            i => i.MatchStfld<Player>(nameof(Player.controlUseItem))))
        {
            throw new InvalidOperationException(
                "UninterruptedUse: could not locate the Use Item UI check in TriggersSet.CopyInto. "
                + "This tModLoader version or another input mod changed the method.");
        }

        cursor.Index += 2;
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitDelegate<Func<bool, Player, bool>>(HeldUsePlayer.ShouldBlockItemUse);
    }

    private static void PreventHoverReleaseLock(ILContext il)
    {
        var cursor = new ILCursor(il);

        // Vanilla also sets delayUseItem on UI hover. Bypassing just CopyInto
        // would still interrupt use on the next tick until the button is released.
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchLdarg(0),
            i => i.MatchLdfld<Player>(nameof(Player.mouseInterface)),
            i => i.OpCode == OpCodes.Brfalse || i.OpCode == OpCodes.Brfalse_S,
            i => i.MatchLdarg(0),
            i => i.MatchLdcI4(1),
            i => i.MatchStfld<Player>(nameof(Player.delayUseItem))))
        {
            throw new InvalidOperationException(
                "UninterruptedUse: could not locate the UI hover release lock in Player.Update. "
                + "This tModLoader version or another input mod changed the method.");
        }

        cursor.Index += 2;
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<bool, Player, bool>>(HeldUsePlayer.ShouldBlockItemUse);
    }

    private static void PreventHeldHotbarSelection(On_Main.orig_GUIHotbarDrawInner orig, Main self)
    {
        if (!Main.LocalPlayer.GetModPlayer<HeldUsePlayer>().ProtectsHeldUse)
        {
            orig(self);
            return;
        }

        // Vanilla selects hotbar slots on a held button, not just a fresh click.
        // Suppress it only inside hotbar drawing; the mapped use input is untouched.
        bool mouseLeft = Main.mouseLeft;
        try
        {
            Main.mouseLeft = false;
            orig(self);
        }
        finally
        {
            Main.mouseLeft = mouseLeft;
        }
    }
}
