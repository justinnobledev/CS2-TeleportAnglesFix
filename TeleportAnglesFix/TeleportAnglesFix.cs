﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TeleportAnglesFix
{
    public class TeleportAnglesFix : BasePlugin
    {
        public override string ModuleName { get; } = "Teleport Angles Fix";
        public override string ModuleVersion { get; } = "1.1";
        public override string ModuleAuthor { get; } = "Retro";
        public override string ModuleDescription { get; } = "Fixes the angles of the player when they teleport.";

        private Dictionary<int, QAngle> _angleCache = new();
        private string _currentMapName = string.Empty;
        private HashSet<string> _targetMaps = new HashSet<string>();

        private class Config
        {
            public List<string> TargetMaps { get; set; } = new List<string>();
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            UpdateCurrentMapName();
            LoadConfig();
        }

        private void UpdateCurrentMapName()
        {
            _currentMapName = Server.MapName;
        }

        private void LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(ModuleDirectory, "maplist.json");
                if (!File.Exists(configPath))
                {
                    CreateDefaultConfig(configPath);
                }

                var configJson = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Config>(configJson);
                if (config != null)
                {
                    _targetMaps = new HashSet<string>(config.TargetMaps);
                }
                else
                {
                    Logger.LogError("Failed to load configuration: deserialization returned null");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred while loading the configuration");
            }
        }

        private void CreateDefaultConfig(string configPath)
        {
            var defaultConfig = new Config
            {
                TargetMaps = new List<string> { "surf_reprise" }
            };

            var configJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, configJson);
            Logger.LogInformation("Default configuration file created at: " + configPath);
        }

        [EntityOutputHook("trigger_teleport", "OnStartTouch")]
        public HookResult OnStartTouch(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller,
            CVariant value, float delay)
        {
            UpdateCurrentMapName();

            if (activator.DesignerName != "player" || !_targetMaps.Contains(_currentMapName)) return HookResult.Continue;

            var pawn = activator.As<CCSPlayerPawn>();
            if (!pawn.IsValid || !pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
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
            UpdateCurrentMapName();

            if (activator.DesignerName != "player" || !_targetMaps.Contains(_currentMapName)) return HookResult.Continue;

            var pawn = new CCSPlayerPawn(activator.Handle);
            if (!pawn.IsValid || !pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;
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
}