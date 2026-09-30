using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using UninterruptedUse.Common.Players;
using UninterruptedUse.Common.Systems;

namespace UninterruptedUseQA;

public sealed class UninterruptedUseQA : Mod { }

public sealed class InputValidation : ModSystem
{
    private readonly List<string> checks = new();
    private Player player;
    private HeldUsePlayer hold;
    private HeldUseInputSystem inputSystem;

    private void Check(bool result, string name)
    {
        if (!result)
            throw new InvalidOperationException("FAILED: " + name);
        checks.Add("PASS " + name);
    }

    public override void PostSetupContent()
    {
        string resultPath = Environment.GetEnvironmentVariable("UNINTERRUPTED_USE_QA_RESULT");
        if (string.IsNullOrEmpty(resultPath))
            throw new InvalidOperationException("Set UNINTERRUPTED_USE_QA_RESULT for this isolated test mod.");

        try
        {
            RunChecks();
            File.WriteAllLines(resultPath, checks);
            Console.WriteLine($"UninterruptedUse QA: {checks.Count} checks passed.");
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            checks.Add(error.ToString());
            File.WriteAllLines(resultPath, checks);
            Console.Error.WriteLine(error);
            Environment.Exit(1);
        }
    }

    private void RunChecks()
    {
        inputSystem = ModContent.GetInstance<HeldUseInputSystem>();
        foreach (string field in new[] { "copyHookInstalled", "updateHookInstalled", "hotbarHookInstalled" })
            Check((bool)typeof(HeldUseInputSystem).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(inputSystem), "actual runtime hook installed: " + field);

        // The host loads as a dedicated server. No window, world, or save is opened.
        // Switch only the input flags to a local-client context for these calls.
        Main.dedServ = false;
        Main.netMode = NetmodeID.SinglePlayer;
        Main.myPlayer = 0;
        Main.hasFocus = true;
        Main.gameMenu = false;
        Main.gamePaused = false;
        Main.playerInventory = false;
        Main.ingameOptionsWindow = false;
        Main.inFancyUI = false;
        Main.mapFullscreen = false;
        Main.drawingPlayerChat = false;
        Main.editSign = false;
        Main.editChest = false;
        Main.blockInput = false;
        Main.blockMouse = false;
        Main.isMouseLeftConsumedByUI = false;
        Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
        Main.screenWidth = 1280;
        Main.screenHeight = 720;
        PlayerInput.WritingText = false;
        PlayerInput.CurrentInputMode = InputMode.Mouse;

        player = new Player { whoAmI = 0, active = true };
        player.inventory[0].SetDefaults(ItemID.Megashark);
        Main.player[0] = player;
        hold = player.GetModPlayer<HeldUsePlayer>();
        PlayerInput.Triggers.Current.MouseLeft = false;
        PlayerInput.Triggers.Current.MouseRight = false;
        hold.OnEnterWorld();

        TestCursorItemInteraction();

        FreshWorldPress();
        Check(player.controlUseItem && hold.ProtectsHeldUse, "fresh world press receives ordinary use input");
        player.releaseUseItem = false;
        bool autoReuse = player.HeldItem.autoReuse;
        int useTime = player.HeldItem.useTime;
        int damage = player.HeldItem.damage;
        for (int frame = 0; frame < 600; frame++)
        {
            player.mouseInterface = true;
            Tick(true);
            if (!player.controlUseItem || !hold.ProtectsHeldUse)
                throw new InvalidOperationException("UI hold interrupted on tick " + frame);
        }
        Check(true, "600 ticks of UI hover preserve mapped use input");
        Check(player.mouseInterface, "real UI hover flag remains true");
        Check(!player.releaseUseItem, "input fix does not synthesize repeated fresh clicks");
        Check(player.HeldItem.autoReuse == autoReuse && player.HeldItem.useTime == useTime
            && player.HeldItem.damage == damage, "item firing rules and stats unchanged");
        Check(!HeldUsePlayer.ShouldBlockItemUse(true, player), "protected hover bypasses release lock predicate");

        PlayerInput.Triggers.Current.MouseRight = true;
        Tick(true);
        Check(!hold.ProtectsHeldUse && !player.controlUseTile, "explicit right-click cancels protection and preserves native UI blocking");
        PlayerInput.Triggers.Current.MouseRight = false;
        PlayerInput.Triggers.Current.MouseLeft = false;
        Check(!hold.ProtectsHeldUse, "release is effective before the next state update");
        Tick(false);
        Check(!player.controlUseItem && !hold.ProtectsHeldUse, "release over UI stops protected use");
        Check(HeldUsePlayer.ShouldBlockItemUse(true, player), "ordinary UI release lock restored");

        player.mouseInterface = true;
        Tick(true);
        Check(!player.controlUseItem && !hold.ProtectsHeldUse, "a press that begins on UI does not attack");
        player.mouseInterface = false;
        Tick(true);
        Check(player.controlUseItem && !hold.ProtectsHeldUse, "dragging off UI keeps native input without arming protection");
        player.mouseInterface = true;
        Tick(true);
        Check(!player.controlUseItem && !hold.ProtectsHeldUse, "UI-started press cannot acquire protection later");

        TestBlocker("inventory", () => Main.playerInventory = true, () => Main.playerInventory = false);
        TestBlocker("full-screen map", () => Main.mapFullscreen = true, () => Main.mapFullscreen = false);
        TestBlocker("pause", () => Main.gamePaused = true, () => Main.gamePaused = false);
        TestBlocker("main menu", () => Main.gameMenu = true, () => Main.gameMenu = false);
        TestBlocker("options", () => Main.ingameOptionsWindow = true, () => Main.ingameOptionsWindow = false);
        TestBlocker("fancy UI", () => Main.inFancyUI = true, () => Main.inFancyUI = false);
        TestBlocker("chat", () => Main.drawingPlayerChat = true, () => Main.drawingPlayerChat = false);
        TestBlocker("sign editing", () => Main.editSign = true, () => Main.editSign = false);
        TestBlocker("chest editing", () => Main.editChest = true, () => Main.editChest = false);
        TestBlocker("input lock", () => Main.blockInput = true, () => Main.blockInput = false);
        TestBlocker("mouse lock", () => Main.blockMouse = true, () => Main.blockMouse = false);
        TestBlocker("consumed UI click", () => Main.isMouseLeftConsumedByUI = true, () => Main.isMouseLeftConsumedByUI = false);
        TestBlocker("text input", () => PlayerInput.WritingText = true, () => PlayerInput.WritingText = false);
        TestBlocker("lost focus", () => Main.hasFocus = false, () => Main.hasFocus = true);
        TestBlocker("NPC dialogue", () => player.SetTalkNPC(0), () => player.SetTalkNPC(-1));
        TestBlocker("death", () => player.dead = true, () => player.dead = false);
        TestBlocker("ghost", () => player.ghost = true, () => player.ghost = false);
        TestBlocker("inactive player", () => player.active = false, () => player.active = true);
        TestBlocker("gamepad", () => PlayerInput.CurrentInputMode = InputMode.XBoxGamepad,
            () => PlayerInput.CurrentInputMode = InputMode.Mouse);

        Tick(false);
        player.mouseInterface = false;
        player.delayUseItem = true;
        Tick(true);
        Check(!hold.ProtectsHeldUse && player.delayUseItem, "pre-existing release requirement is respected");
        player.delayUseItem = false;

        FreshWorldPress();
        player.mouseInterface = true;
        Main.mouseLeft = false;
        Tick(true);
        Check(player.controlUseItem && hold.ProtectsHeldUse, "mapped use action works independently of physical mouseLeft");
        player.whoAmI = 1;
        Check(HeldUsePlayer.ShouldBlockItemUse(true, player), "remote players are unaffected");
        player.whoAmI = 0;

        Tick(false);
        player.inventory[0].TurnToAir();
        player.mouseInterface = false;
        Tick(true);
        Check(!hold.ProtectsHeldUse, "empty hand does not arm protection");
        player.inventory[0].SetDefaults(ItemID.Megashark);

        PlayerInput.Triggers.Current.MouseLeft = true;
        hold.OnEnterWorld();
        Tick(true);
        Check(!hold.ProtectsHeldUse, "entering a world with a held key cannot arm protection");

        TestHotbarWrapper();
        TestNativeHoverDelayBlock();
        TestNativeItemUse();
        TestProtectionToggle();

        inputSystem.Unload();
        FreshWorldPress();
        player.mouseInterface = true;
        Tick(true);
        Check(!player.controlUseItem, "unloading restores native UI input blocking");
        inputSystem.Load();
        Tick(true);
        Check(player.controlUseItem, "reloading installs working input hooks again");
        inputSystem.Unload();
    }

    private void Tick(bool down)
    {
        PlayerInput.Triggers.Current.MouseLeft = down;
        inputSystem.PostUpdateInput();
        player.controlUseItem = false;
        player.controlUseTile = false;
        PlayerInput.Triggers.Current.CopyInto(player);
    }

    private void TestCursorItemInteraction()
    {
        Item originalCursorItem = Main.mouseItem;
        Item originalCursorSlot = player.inventory[58];
        int originalSlot = player.selectedItem;
        try
        {
            Main.mouseItem = new Item(ItemID.WoodenSword);
            player.inventory[58] = Main.mouseItem;
            player.selectedItem = 58;
            FreshWorldPress();
            Check(!hold.ProtectsHeldUse, "cursor-carried usable item cannot activate attack protection");
            player.mouseInterface = true;
            Tick(true);
            Check(!player.controlUseItem, "cursor item UI placement retains native input blocking");

            player.selectedItem = originalSlot;
            FreshWorldPress();
            Check(!hold.ProtectsHeldUse, "cursor-carried item blocks protection even with a hotbar slot selected");

            MethodInfo hotbar = typeof(HeldUseInputSystem).GetMethod("PreventHeldHotbarSelection",
                BindingFlags.Static | BindingFlags.NonPublic);
            bool observedClick = false;
            On_Main.orig_GUIHotbarDrawInner draw = _ => observedClick = Main.mouseLeft;
            Main.mouseLeft = true;
            hotbar.Invoke(null, new object[] { draw, null });
            Check(observedClick, "cursor-carried item does not suppress hotbar interaction clicks");

            Main.playerInventory = true;
            Main.mouseLeftRelease = true;
            player.mouseInterface = true;
            var slots = new[] { new Item() };
            Main.dedServ = true;
            try { Terraria.UI.ItemSlot.LeftClick(slots, 0, 0); }
            finally { Main.dedServ = false; }
            Check(slots[0].type == ItemID.WoodenSword && Main.mouseItem.IsAir,
                "native inventory click places a cursor-carried usable item into a slot");

            Main.mouseLeftRelease = true;
            Main.dedServ = true;
            try { Terraria.UI.ItemSlot.LeftClick(slots, 0, 0); }
            finally { Main.dedServ = false; }
            Check(slots[0].IsAir && Main.mouseItem.type == ItemID.WoodenSword,
                "native inventory click picks the usable item back up");

            Main.playerInventory = false;
            player.selectedItem = 58;
            player.inventory[58] = Main.mouseItem;
            player.itemAnimation = player.itemTime = player.reuseDelay = player.noThrow = 0;
            player.releaseThrow = true;
            player.mouseInterface = false;
            PlayerInput.Triggers.Current.Throw = true;
            Tick(true);
            Check(player.controlThrow && !hold.ProtectsHeldUse,
                "mapped drop command reaches native controls while holding a cursor item");
            Main.dedServ = true;
            try { player.dropItemCheck(); }
            finally { Main.dedServ = false; }
            Check(Main.mouseItem.IsAir && player.inventory[58].IsAir,
                "native dropItemCheck successfully drops the cursor-carried usable item");
        }
        finally
        {
            Main.playerInventory = false;
            PlayerInput.Triggers.Current.Throw = false;
            Main.mouseItem = originalCursorItem;
            player.inventory[58] = originalCursorSlot;
            player.selectedItem = originalSlot;
            Tick(false);
        }
    }

    private void TestProtectionToggle()
    {
        ModKeybind keybind = HeldUseKeybindSystem.ToggleProtectionKeybind;
        Check(keybind != null, "protection toggle keybind registered");
        Check((string)typeof(ModKeybind).GetProperty("DefaultBinding", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(keybind) == "F8", "protection toggle defaults to F8");
        FreshWorldPress();
        hold.SetProtectionEnabled(false);
        player.mouseInterface = true;
        Tick(true);
        Check(!hold.ProtectsHeldUse && !player.controlUseItem,
            "disabling protection immediately restores native UI input handling");

        hold.SetProtectionEnabled(true);
        player.mouseInterface = false;
        Tick(true);
        Check(!hold.ProtectsHeldUse, "enabling protection does not capture an existing held click");
        FreshWorldPress();
        Check(hold.ProtectsHeldUse, "new world press is protected after enabling");

        var tag = new TagCompound();
        hold.SetProtectionEnabled(false);
        hold.SaveData(tag);
        var restored = new Player().GetModPlayer<HeldUsePlayer>();
        restored.LoadData(tag);
        Check(!restored.ProtectionEnabled, "disabled preference survives saved-state round trip");
        restored.LoadData(new TagCompound());
        Check(restored.ProtectionEnabled, "characters without a saved preference default to enabled");

        hold.SetProtectionEnabled(true);
        string bindingId = Mod.Name + "/ToggleProtection";
        PlayerInput.Triggers.JustPressed.KeyStatus[bindingId] = true;
        Tick(false);
        Check(!hold.ProtectionEnabled, "registered keybind reaches ProcessTriggers and switches protection off");
        PlayerInput.Triggers.JustPressed.KeyStatus[bindingId] = false;
        Tick(false);
        Check(!hold.ProtectionEnabled, "a held toggle does not flip the state again");
        PlayerInput.Triggers.JustPressed.KeyStatus[bindingId] = true;
        Tick(false);
        Check(hold.ProtectionEnabled, "next toggle press switches protection on");
        PlayerInput.Triggers.JustPressed.KeyStatus[bindingId] = false;

        TestBlocker("explicit item drop", () => PlayerInput.Triggers.Current.Throw = true,
            () => PlayerInput.Triggers.Current.Throw = false);
        TestBlocker("explicit right click", () => PlayerInput.Triggers.Current.MouseRight = true,
            () => PlayerInput.Triggers.Current.MouseRight = false);
    }

    private void FreshWorldPress()
    {
        Tick(false);
        player.mouseInterface = false;
        player.delayUseItem = false;
        Tick(true);
    }

    private void TestBlocker(string name, Action block, Action unblock)
    {
        FreshWorldPress();
        block();
        Check(!hold.ProtectsHeldUse, name + " immediately suspends protection");
        Tick(true);
        unblock();
        Tick(true);
        Check(!hold.ProtectsHeldUse, name + " cannot re-arm the same held press");
        FreshWorldPress();
        Check(hold.ProtectsHeldUse, name + " permits a new world press after release");
    }

    private void TestHotbarWrapper()
    {
        MethodInfo wrapper = typeof(HeldUseInputSystem).GetMethod("PreventHeldHotbarSelection",
            BindingFlags.Static | BindingFlags.NonPublic);
        FreshWorldPress();
        player.mouseInterface = true;
        Main.mouseLeft = true;
        bool observedLeft = true;
        On_Main.orig_GUIHotbarDrawInner draw = _ => observedLeft = Main.mouseLeft;
        wrapper.Invoke(null, new object[] { draw, null });
        Check(!observedLeft && Main.mouseLeft, "hotbar receives no held click and global mouse state is restored");

        On_Main.orig_GUIHotbarDrawInner throwingDraw = _ => throw new InvalidOperationException("expected draw failure");
        try { wrapper.Invoke(null, new object[] { throwingDraw, null }); }
        catch (TargetInvocationException) { }
        Check(Main.mouseLeft, "hotbar restores mouse state even if drawing throws");

        Tick(false);
        Main.mouseLeft = true;
        wrapper.Invoke(null, new object[] { draw, null });
        Check(observedLeft, "unprotected hotbar clicks reach native drawing unchanged");
    }

    private void TestNativeHoverDelayBlock()
    {
        DynamicMethodDefinition probeMethod = null;
        Action<Player> probe = null;
        ILContext.Manipulator capture = il =>
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0), i => i.MatchLdcI4(1),
                i => i.MatchStfld<Player>(nameof(Player.delayUseItem))))
                throw new InvalidOperationException("Native hover delay setter not found.");

            int end = cursor.Index + 2;
            int start = cursor.Index - 1;
            while (start >= 0 && !il.Instrs[start].MatchLdfld<Player>(nameof(Player.mouseInterface)))
                start--;
            if (start < 1) throw new InvalidOperationException("Native hover predicate not found.");
            start--;

            // Execute the actual patched native IL block without running physics
            // or opening a world. Do not handwrite a copy of the delay logic.
            probeMethod = new DynamicMethodDefinition("NativeHoverDelayProbe", typeof(void), new[] { typeof(Player) });
            var processor = probeMethod.Definition.Body.GetILProcessor();
            var clones = new Dictionary<Instruction, Instruction>();
            for (int index = start; index <= end; index++)
            {
                Instruction original = il.Instrs[index];
                Instruction clone = Instruction.Create(OpCodes.Nop);
                clone.OpCode = original.OpCode;
                clone.Operand = original.Operand;
                clones.Add(original, clone);
                processor.Append(clone);
            }
            Instruction finish = Instruction.Create(OpCodes.Ret);
            processor.Append(finish);
            foreach (Instruction clone in clones.Values)
            {
                if (clone.Operand is ILLabel label) clone.Operand = label.Target;
                if (clone.Operand is Instruction target)
                    clone.Operand = clones.TryGetValue(target, out Instruction localTarget) ? localTarget : finish;
            }
            probe = (Action<Player>)probeMethod.Generate().CreateDelegate(typeof(Action<Player>));
        };

        try
        {
            IL_Player.Update += capture;
            Check(probe != null, "captured actual patched Player.Update hover block");
            FreshWorldPress();
            player.mouseInterface = true;
            probe(player);
            Check(!player.delayUseItem, "native UI hover block does not latch a protected hold");
            player.delayUseItem = true;
            probe(player);
            Check(player.delayUseItem, "native UI fix never clears an existing delay");
            Tick(false);
            player.delayUseItem = false;
            probe(player);
            Check(player.delayUseItem, "unprotected native UI hover still sets the release requirement");
            player.delayUseItem = false;
            player.mouseInterface = false;
            probe(player);
            Check(!player.delayUseItem, "native non-UI path remains unchanged");
        }
        finally
        {
            IL_Player.Update -= capture;
            probeMethod?.Dispose();
        }
    }

    private void TestNativeItemUse()
    {
        Item originalItem = player.inventory[0];
        var probeItem = new Item(ItemID.WoodenSword)
        {
            damage = 0,
            noMelee = true,
            UseSound = null,
            useTime = 6,
            useAnimation = 6,
            autoReuse = false
        };
        player.inventory[0] = probeItem;
        player.lastVisualizedSelectedItem = probeItem;
        player.statLife = player.statLifeMax2 = 100;
        player.itemAnimation = player.itemTime = 0;
        player.releaseUseItem = true;
        Main.SettingsEnabled_AutoReuseAllItems = false;

        FreshWorldPress();
        int singleStarts = CountNativeUses(120);
        Check(singleStarts == 1, "native non-auto-reuse weapon attacks only once during a held press");
        Tick(false);
        NativeItemCheck();
        player.mouseInterface = false;
        Tick(true);
        Check(CountNativeUses(30) == 1, "native non-auto-reuse weapon attacks again after release and press");

        probeItem.autoReuse = true;
        player.itemAnimation = player.itemTime = 0;
        player.releaseUseItem = true;
        FreshWorldPress();
        int repeatedStarts = CountNativeUses(120);
        Check(repeatedStarts > 1, "native auto-reuse weapon repeats across sustained UI hover");

        probeItem.autoReuse = false;
        probeItem.channel = true;
        player.itemAnimation = player.itemTime = 0;
        player.releaseUseItem = true;
        FreshWorldPress();
        NativeItemCheck();
        Check(player.channel, "native channeling starts from a world press");
        player.mouseInterface = true;
        for (int tick = 0; tick < 120; tick++)
        {
            Tick(true);
            NativeItemCheck();
            if (!player.channel) throw new InvalidOperationException("Native channeling interrupted at " + tick);
        }
        Check(true, "native channeling survives 120 ticks of UI hover");
        Tick(false);
        NativeItemCheck();
        Check(!player.channel, "native channeling stops on release over UI");
        player.inventory[0] = originalItem;
        player.itemAnimation = player.itemTime = 0;
    }

    private int CountNativeUses(int ticks)
    {
        int starts = 0;
        void RecordStart(On_Player.orig_ItemCheck_StartActualUse orig, Player owner, Item item)
        {
            if (owner == player) starts++;
            orig(owner, item);
        }
        On_Player.ItemCheck_StartActualUse += RecordStart;
        try
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                // Alternate between game area and HUD while retaining the same press.
                player.mouseInterface = tick % 20 >= 2;
                Tick(true);
                NativeItemCheck();
            }
        }
        finally
        {
            On_Player.ItemCheck_StartActualUse -= RecordStart;
        }
        return starts;
    }

    private void NativeItemCheck()
    {
        // ItemCheck's gameplay logic is native. Only skip drawing frame lookups,
        // because the isolated host intentionally has no graphics device/assets.
        Main.dedServ = true;
        try { player.ItemCheck(); }
        finally { Main.dedServ = false; }
    }
}
