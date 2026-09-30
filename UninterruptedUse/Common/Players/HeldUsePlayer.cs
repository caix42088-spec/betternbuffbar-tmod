using Terraria;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using UninterruptedUse.Common.Systems;

namespace UninterruptedUse.Common.Players;

[Autoload(Side = ModSide.Client)]
public sealed class HeldUsePlayer : ModPlayer
{
    private bool useWasDown;
    private bool holdStartedInWorld;
    public bool ProtectionEnabled { get; private set; } = true;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (HeldUseKeybindSystem.ToggleProtectionKeybind?.JustPressed == true)
        {
            SetProtectionEnabled(!ProtectionEnabled);
            Main.NewText(Language.GetTextValue(ProtectionEnabled
                ? "Mods.UninterruptedUse.Messages.Enabled"
                : "Mods.UninterruptedUse.Messages.Disabled"));
        }
    }

    internal void SetProtectionEnabled(bool enabled)
    {
        ProtectionEnabled = enabled;
        // A toggle is also an escape from the current protected hold. Turning
        // it back on cannot capture a click already being used for an interaction.
        holdStartedInWorld = false;
        useWasDown = PlayerInput.Triggers.Current.MouseLeft;
    }

    public override void SaveData(TagCompound tag)
    {
        tag["ProtectionEnabled"] = ProtectionEnabled;
    }

    public override void LoadData(TagCompound tag)
    {
        SetProtectionEnabled(!tag.ContainsKey("ProtectionEnabled") || tag.GetBool("ProtectionEnabled"));
    }

    public override void OnEnterWorld()
    {
        // Entering a world with a button already held is not a new press.
        useWasDown = PlayerInput.Triggers.Current.MouseLeft;
        holdStartedInWorld = false;
    }

    internal void UpdateInput()
    {
        // MouseLeft is Terraria's mapped Use Item action, including rebound keys.
        bool useDown = PlayerInput.Triggers.Current.MouseLeft;

        if (!useDown || !CanProtectGameplayInput())
        {
            holdStartedInWorld = false;
        }
        else if (!useWasDown)
        {
            holdStartedInWorld = !Player.mouseInterface && !Player.delayUseItem;
        }

        // A press that began over UI cannot become protected by dragging off it.
        useWasDown = useDown;
    }

    internal bool ProtectsHeldUse => holdStartedInWorld
        && PlayerInput.Triggers.Current.MouseLeft
        && CanProtectGameplayInput();

    private bool CanProtectGameplayInput()
    {
        return ProtectionEnabled
            && !Main.dedServ
            && Player.whoAmI == Main.myPlayer
            && Player.active
            && !Player.dead
            && !Player.ghost
            && Main.hasFocus
            && !Main.gameMenu
            && !Main.gamePaused
            && !Main.playerInventory
            && !Main.ingameOptionsWindow
            && !Main.inFancyUI
            && !Main.mapFullscreen
            && !Main.drawingPlayerChat
            && !Main.editSign
            && !Main.editChest
            && !Main.blockInput
            && !Main.blockMouse
            && !Main.isMouseLeftConsumedByUI
            && !PlayerInput.WritingText
            && !PlayerInput.UsingGamepad
            // A cursor-carried item is an inventory/placement/drop operation,
            // even after closing the inventory. Slot 58 is the mouse-item slot.
            && Player.selectedItem != 58
            && (Main.mouseItem == null || Main.mouseItem.IsAir)
            // Give explicit interactions priority over continuation protection.
            && !PlayerInput.Triggers.Current.MouseRight
            && !PlayerInput.Triggers.Current.Throw
            && Player.talkNPC < 0
            && !Player.HeldItem.IsAir
            && Player.HeldItem.useStyle > 0
            && Player.HeldItem.useTime > 0;
    }

    internal static bool ShouldBlockItemUse(bool mouseInterface, Player player)
    {
        // Keep the real mouseInterface flag for tooltips, right clicks, and UI.
        return mouseInterface && !player.GetModPlayer<HeldUsePlayer>().ProtectsHeldUse;
    }
}
