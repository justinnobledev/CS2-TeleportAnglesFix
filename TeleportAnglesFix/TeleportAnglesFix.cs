using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace TeleportAnglesFix;

public class TeleportAnglesFix : BasePlugin
{
    public override string ModuleName { get; } = "Teleport Angles Fix";
    public override string ModuleVersion { get; } = "1.2.3";
    public override string ModuleAuthor { get; } = "Retro";
    public override string ModuleDescription { get; } = "Fixes the angles of the player when they teleport.";
    
    private Dictionary<int, QAngle> _angleCache = new();

    public FakeConVar<bool> g_bEnableFix = new("css_enable_teleport_ang_fix", "Enable teleport angle fix", true);

    public override void Load(bool hotReload)
    {
        RegisterFakeConVars(typeof(ConVar));
        VirtualFunctions.CBaseTrigger_StartTouchFunc.Hook(Hook_OnStartTouch, HookMode.Pre);
        RegisterListener<Listeners.OnMapStart>((_)=>{_teleportPairs.Clear();});
    }
    
    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseTrigger_StartTouchFunc.Unhook(Hook_OnStartTouch, HookMode.Pre);
    }
    
    private Dictionary<string, CBaseEntity> _teleportPairs = new();
    
    private CBaseEntity? FindTeleportDestination(string targetName)
    {
        if(_teleportPairs.TryGetValue(targetName, out var destination)) 
            return destination;
    
        var dest = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination").FirstOrDefault((
            ent) => targetName.Equals(ent.Entity?.Name));
        
        Logger.LogInformation("Found destination " + dest?.Entity?.Name);
        
        return dest;
    }
    
    private HookResult Hook_OnStartTouch(DynamicHook arg)
    {
        var trigger = arg.GetParam<CBaseTrigger>(0);
        if (!trigger.IsValid) return HookResult.Continue;
        if (!trigger.DesignerName.Equals("trigger_teleport")) return HookResult.Continue;
        var teleporter = trigger.As<CTriggerTeleport>();
        
        var ent = arg.GetParam<CBaseEntity>(1);
        if(!ent.IsValid || !ent.DesignerName.Equals("player"))  return HookResult.Continue;
        
        var pawn = ent.As<CCSPlayerPawn>();
        if(!pawn.Controller.IsValid || pawn.Controller.Value is null)  return HookResult.Continue;
        var controller = pawn.Controller.Value.As<CCSPlayerController>();
        Logger.LogInformation($"controller is {controller.PlayerName}");
        Logger.LogInformation($"{teleporter.UseLandmarkAngles} || string.IsNullOrEmpty: {string.IsNullOrEmpty(teleporter.Landmark)}");
        if(teleporter.UseLandmarkAngles || string.IsNullOrEmpty(teleporter.Landmark)) return HookResult.Continue;
        Logger.LogInformation($"Land is {teleporter.Landmark} and target is {teleporter.Target}");
        
        var teleportDest = FindTeleportDestination(teleporter.Target);
        
        Logger.LogInformation($"teleport destination is {teleportDest?.DesignerName}");
        if(teleportDest is null) return HookResult.Continue;
        if (pawn.AbsOrigin is null || trigger.AbsOrigin is null || teleportDest.AbsOrigin is null) return HookResult.Continue;

        var relativeOffset = trigger.AbsOrigin -  pawn.AbsOrigin;
        var newOrigin = teleportDest.AbsOrigin + relativeOffset;
        
        Server.RunOnTick(Server.TickCount + 1, () =>
        {
            pawn.Teleport(position: newOrigin, angles: new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z), velocity: null);
        });
        
        
        // teleportDest = FindTeleportDestination(teleporter.Landmark);
        //
        // Logger.LogInformation($"teleport destination is {teleportDest?.DesignerName}");
        
        return HookResult.Stop;
    }

    // [EntityOutputHook("trigger_teleport", "OnStartTouch")]
    // public HookResult OnStartTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
    //     CVariant value, float delay)
    // {
    //     if (!g_bEnableFix.Value) return HookResult.Continue;
    //     if (activator.DesignerName != "player") return HookResult.Continue;
    //
    //     var pawn = activator.As<CCSPlayerPawn>();
    //     if (!pawn.IsValid) return HookResult.Continue;
    //     if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
    //     var controller = pawn.Controller.Value.As<CCSPlayerController>();
    //     if (controller.SteamID <= 0) return HookResult.Continue;
    //
    //     var teleport = caller.As<CTriggerTeleport>();
    //     if (teleport.UseLandmarkAngles || string.IsNullOrEmpty(teleport.Landmark)) return HookResult.Continue;
    //
    //     _angleCache[controller.Slot] = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);
    //
    //     return HookResult.Continue;
    // }
    //
    // [EntityOutputHook("trigger_teleport", "OnEndTouch")]
    // public HookResult OnEndTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
    //     CVariant value, float delay)
    // {
    //     if (!g_bEnableFix.Value) return HookResult.Continue;
    //     if (activator.DesignerName != "player") return HookResult.Continue;
    //
    //     var pawn = new CCSPlayerPawn(activator.Handle);
    //     if (!pawn.IsValid) return HookResult.Continue;
    //     if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
    //     var controller = pawn.Controller.Value.As<CCSPlayerController>();
    //     if (controller.SteamID <= 0) return HookResult.Continue;
    //     
    //     var teleport = caller.As<CTriggerTeleport>();
    //     if (teleport.UseLandmarkAngles || string.IsNullOrEmpty(teleport.Landmark)) return HookResult.Continue;
    //
    //     if (!_angleCache.TryGetValue(controller.Slot, out var angle)) return HookResult.Continue;
    //     
    //     Server.RunOnTick(Server.TickCount + 1, () =>
    //     {
    //         if (pawn.IsValid && pawn.Controller.IsValid && pawn.Controller.Value is not null)
    //         {
    //             pawn.Teleport(angles: angle);
    //         }
    //         _angleCache.Remove(controller.Slot);
    //     });
    //     
    //
    //     return HookResult.Continue;
    // }
}