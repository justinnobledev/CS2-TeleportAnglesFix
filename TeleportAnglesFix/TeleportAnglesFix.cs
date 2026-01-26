using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace TeleportAnglesFix;

public class TeleportAnglesFix : BasePlugin
{
    public override string ModuleName { get; } = "Teleport Angles Fix";
    public override string ModuleVersion { get; } = "1.2.3";
    public override string ModuleAuthor { get; } = "Retro";
    public override string ModuleDescription { get; } = "Fixes the angles of the player when they teleport.";

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
    
    private readonly Dictionary<string, CBaseEntity> _teleportPairs = new();
    
    private CBaseEntity? FindTeleportDestination(string targetName)
    {
        if(_teleportPairs.TryGetValue(targetName, out var destination)) 
            return destination;
    
        var dest = Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination").FirstOrDefault((
            ent) => targetName.Equals(ent.Entity?.Name));
        
        return dest;
    }
    
    private HookResult Hook_OnStartTouch(DynamicHook arg)
    {
        if (g_bEnableFix.Value) return HookResult.Continue;
        var trigger = arg.GetParam<CBaseTrigger>(0);
        if (!trigger.IsValid) return HookResult.Continue;
        if (!trigger.DesignerName.Equals("trigger_teleport")) return HookResult.Continue;
        var teleporter = trigger.As<CTriggerTeleport>();
        
        var ent = arg.GetParam<CBaseEntity>(1);
        if(!ent.IsValid || !ent.DesignerName.Equals("player"))  return HookResult.Continue;
        
        var pawn = ent.As<CCSPlayerPawn>();
        if(!pawn.Controller.IsValid || pawn.Controller.Value is null)  return HookResult.Continue;

        if(teleporter.UseLandmarkAngles || string.IsNullOrEmpty(teleporter.Landmark)) return HookResult.Continue;
        
        var teleportDest = FindTeleportDestination(teleporter.Target);
        
        if(teleportDest is null) return HookResult.Continue;
        if (pawn.AbsOrigin is null || trigger.AbsOrigin is null || teleportDest.AbsOrigin is null) return HookResult.Continue;

        var relativeOffset = trigger.AbsOrigin -  pawn.AbsOrigin;
        var newOrigin = teleportDest.AbsOrigin + relativeOffset;
        
        pawn.Teleport(position: newOrigin, angles: new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z), velocity: null);
        
        return HookResult.Stop;
    }
}