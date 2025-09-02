using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace TeleportAnglesFix;

public class TeleportAnglesFix : BasePlugin
{
    public override string ModuleName { get; } = "Teleport Angles Fix";
    public override string ModuleVersion { get; } = "1.2.1";
    public override string ModuleAuthor { get; } = "Retro";
    public override string ModuleDescription { get; } = "Fixes the angles of the player when they teleport.";
    
    private Dictionary<int, QAngle> _angleCache = new();

    public FakeConVar<bool> g_bEnableFix = new("css_enable_teleport_ang_fix", "Enable teleport angle fix", true);

    public Dictionary<string, CInfoTeleportDestination?> _teleportPairs = [];

    public override void Load(bool hotReload)
    {
        RegisterFakeConVars(typeof(ConVar));
        RegisterListener<Listeners.OnMapEnd>(() => { _teleportPairs.Clear(); });
    }

    [EntityOutputHook("trigger_teleport", "OnStartTouch")]
    public HookResult OnStartTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
        CVariant value, float delay)
    {
        if (!g_bEnableFix.Value) return HookResult.Continue;
        if (activator.DesignerName != "player") return HookResult.Continue;

        var pawn = activator.As<CCSPlayerPawn>();
        if (!pawn.IsValid) return HookResult.Continue;
        if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
        var controller = pawn.Controller.Value.As<CCSPlayerController>();
        if (controller.SteamID <= 0) return HookResult.Continue;

        var teleport = caller.As<CTriggerTeleport>();
        if (!teleport.UseLandmarkAngles || string.IsNullOrEmpty(teleport.Landmark)) return HookResult.Continue;

        _angleCache[controller.Slot] = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);

        var dest = FindTeleportDestination(teleport);
        if (dest is null) return HookResult.Continue;

        var offset = ((System.Numerics.Vector3)controller.PlayerPawn.Value.AbsOrigin) - ((System.Numerics.Vector3)teleport.AbsOrigin);
        Server.RunOnTick(Server.TickCount + 1, () =>
        {
            var newOrigin = ((System.Numerics.Vector3)dest.AbsOrigin) + offset;
            controller.Teleport(position: newOrigin, angles: (System.Numerics.Vector3)_angleCache[controller.Slot]);
        });

        return HookResult.Stop;
    }

    private CInfoTeleportDestination? FindTeleportDestination(CTriggerTeleport teleport)
    {
        if (_teleportPairs.TryGetValue(teleport.UniqueHammerID, out var dest))
            return dest;
        dest = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination")
                    .Where((ent) => ent.Target == teleport.Target).FirstOrDefault();
        _teleportPairs.Add(teleport.UniqueHammerID, dest);
        return dest;
    }

    [EntityOutputHook("trigger_teleport", "OnEndTouch")]
    public HookResult OnEndTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
        CVariant value, float delay)
    {
        if (!g_bEnableFix.Value) return HookResult.Continue;
        if (activator.DesignerName != "player") return HookResult.Continue;

        var pawn = new CCSPlayerPawn(activator.Handle);
        if (!pawn.IsValid) return HookResult.Continue;
        if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
        var controller = pawn.Controller.Value.As<CCSPlayerController>();
        if (controller.SteamID <= 0) return HookResult.Continue;
        
        var teleport = caller.As<CTriggerTeleport>();
        if (!teleport.UseLandmarkAngles || string.IsNullOrEmpty(teleport.Landmark)) return HookResult.Continue;

        if (!_angleCache.TryGetValue(controller.Slot, out var angle)) return HookResult.Continue;

        var dest = FindTeleportDestination(teleport);
        if (dest is not null) return HookResult.Stop;
        
        Server.RunOnTick(Server.TickCount + 1, () =>
        {
            pawn.Teleport(angles: angle);
            _angleCache.Remove(controller.Slot);
        });
        

        return HookResult.Continue;
    }
}