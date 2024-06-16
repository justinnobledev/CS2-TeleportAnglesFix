using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace TeleportAnglesFix;

public class TeleportAnglesFix : BasePlugin
{
    public override string ModuleName { get; } = "Teleport Angles Fix";
    public override string ModuleVersion { get; } = "1.1";
    public override string ModuleAuthor { get; } = "Retro";
    public override string ModuleDescription { get; } = "Fixes the angles of the player when they teleport.";
    
    private Dictionary<int, QAngle> _angleCache = new();

    [EntityOutputHook("trigger_teleport", "OnStartTouch")]
    public HookResult OnStartTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
        CVariant value, float delay)
    {
        if (activator.DesignerName != "player") return HookResult.Continue;

        var pawn = activator.As<CCSPlayerPawn>();
        if (!pawn.IsValid) return HookResult.Continue;
        if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
        var controller = pawn.Controller.Value.As<CCSPlayerController>();
        if (controller.SteamID <= 0) return HookResult.Continue;

        var teleport = caller.As<CTriggerTeleport>();
        if (teleport.UseLandmarkAngles) return HookResult.Continue;

        _angleCache[controller.Slot] = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);

        return HookResult.Continue;
    }

    [EntityOutputHook("trigger_teleport", "OnEndTouch")]
    public HookResult OnEndTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
        CVariant value, float delay)
    {
        if (activator.DesignerName != "player") return HookResult.Continue;

        var pawn = new CCSPlayerPawn(activator.Handle);
        if (!pawn.IsValid) return HookResult.Continue;
        if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
        var controller = pawn.Controller.Value.As<CCSPlayerController>();
        if (controller.SteamID <= 0) return HookResult.Continue;
        
        var teleport = caller.As<CTriggerTeleport>();
        if (teleport.UseLandmarkAngles) return HookResult.Continue;

        if (!_angleCache.TryGetValue(controller.Slot, out var angle)) return HookResult.Continue;
        
        Server.RunOnTick(Server.TickCount + 1, () =>
        {
            pawn.Teleport(angles: angle);
            _angleCache.Remove(controller.Slot);
        });
        

        return HookResult.Continue;
    }
}