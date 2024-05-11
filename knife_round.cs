using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Modules.Timers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System;
using CounterStrikeSharp.API.Core.Attributes;

namespace KnifeRound
{
    [MinimumApiVersion(164)]
    public class KnifeRoundConfig : BasePluginConfig
    {
        [JsonPropertyName("GiveArmorOnKnifeRound")] public int GiveArmorOnKnifeRound { get; set; } = 1;
        [JsonPropertyName("FreezeOnVote")] public bool FreezeOnVote { get; set; }
        [JsonPropertyName("BlockTeamChangeOnVoteAndKnife")] public bool BlockTeamChangeOnVoteAndKnife { get; set; } = true;
        [JsonPropertyName("KnifeRoundTimer")] public float KnifeRoundTimer { get; set; } = 2;
        [JsonPropertyName("VoteTimer")] public float VoteTimer { get; set; } = 2;
        [JsonPropertyName("AfterWinningRestartXTimes")] public int AfterWinningRestartXTimes { get; set; } = 1;
        [JsonPropertyName("ChatDisplayName")] public string ChatDisplayName { get; set; } = "AVA";
        [JsonPropertyName("TeamIntroTimeKnifeStart")] public float TeamIntroTimeKnifeStart { get; set; } = 3;
        [JsonPropertyName("TeamIntroTimeAfterKnife")] public float TeamIntroTimeAfterKnife { get; set; } = 3;
        [JsonPropertyName("StartMessage")] public string StartMessage { get; set; } = "Ножи на готове";
    }

    public class KnifeRound : BasePlugin, IPluginConfig<KnifeRoundConfig>
    {
        public override string ModuleName => "Knife Round";
        public override string ModuleVersion => "1.0.2";
        public KnifeRoundConfig Config { get; set; } = new KnifeRoundConfig();

        private Stopwatch stopwatch = new Stopwatch();
        private Dictionary<ulong, bool> OnSpawn = new Dictionary<ulong, bool>();
        private List<ulong> _rtvCountCT = new List<ulong>();
        private List<ulong> _rtvCountT = new List<ulong>();
        private bool knifemode;
        private bool CTWINNER;
        private bool TWINNER;
        private bool BlockTeam;
        private bool onroundstart;
        private bool knifestarted;
        private string targetPlayerName;
        private int currentVotesT;
        private int currentVotesCT;
        private int smena;
        private int ostavit;
        private bool WinMessageSent; // Добавляем обратно

        // Добавляем обратно переменные для CVars
        private float mp_roundtime;
        private float mp_roundtime_defuse;
        private float mp_team_intro_time;

        public void OnConfigParsed(KnifeRoundConfig config)
        {
            Config = config;
            if (Config.GiveArmorOnKnifeRound < 0 || Config.GiveArmorOnKnifeRound > 2)
            {
                Config.GiveArmorOnKnifeRound = 0;
            }
        }

        public override void Load(bool hotReload)
        {
            Server.ExecuteCommand("mp_warmuptime 180");
            AddCommandListener("jointeam", OnCommandJoinTeam, HookMode.Pre);
            RegisterListener<Listeners.OnTick>(OnTick);
            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        }

        private HookResult OnCommandJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (Config.BlockTeamChangeOnVoteAndKnife && BlockTeam)
            {
                return HookResult.Handled;
            }
            return HookResult.Continue;
        }

        public void OnTick()
        {
            if (knifemode && BlockTeam)
            {
                var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
                foreach (var player in playerEntities.Where(p => p != null && p.IsValid && p.PlayerPawn != null && p.PlayerPawn.IsValid))
                {
                    if (OnSpawn.TryGetValue(player.SteamID, out bool valid) && valid)
                    {
                        foreach (var weapon in player.PlayerPawn.Value.WeaponServices!.MyWeapons.Where(w => w.IsValid && w.Value.IsValid && !w.Value.DesignerName.Contains("weapon_knife")))
                        {
                            player.ExecuteClientCommand("slot3");
                            player.DropActiveWeapon();
                            weapon.Value.Remove();
                        }
                    }
                }
            }

            if ((TWINNER || CTWINNER) && !WinMessageSent)
            {
                var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                var winningPlayers = Utilities.GetPlayers().FindAll(p => p != null && p.IsValid && p.TeamNum == (int)winningTeam);
                foreach (var player in winningPlayers)
                {
                    if (player != null && player.IsValid)
                    {
                        player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] {Config.StartMessage}");
                        player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] !switch - смена стороны");
                        player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] !stay - оставить сторону");
                    }
                }
                WinMessageSent = true;
            }
        }

        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (onroundstart)
            {
                if (knifemode)
                {
                    BlockTeam = true;
                    knifestarted = true;
                    Utilities.GetPlayers().ForEach(player =>
                    {
                        if (player != null && player.IsValid)
                        {
                            player.PrintToChat($"{ChatColors.Blue}[{ChatColors.Purple}{Config.ChatDisplayName}\x01]{ChatColors.Blue}{Config.StartMessage}");
                        }
                    });
                }
            }
            else if (!onroundstart)
            {
                mp_roundtime = ConVar.Find("mp_roundtime")!.GetPrimitiveValue<float>();
                mp_roundtime_defuse = ConVar.Find("mp_roundtime_defuse")!.GetPrimitiveValue<float>();
                mp_team_intro_time = ConVar.Find("mp_team_intro_time")!.GetPrimitiveValue<float>();
                knifemode = true;
                onroundstart = true;
            }
            if (knifemode)
            {
                Server.NextFrame(() =>
                {
                    Server.ExecuteCommand($"mp_team_intro_time {Config.TeamIntroTimeKnifeStart}; sv_buy_status_override 3; mp_roundtime {Config.KnifeRoundTimer}; mp_roundtime_defuse {Config.KnifeRoundTimer}; mp_give_player_c4 0");
                });
            }

            return HookResult.Continue;
        }

        // Other event handlers...

        public override void Unload(bool hotReload)
        {
            OnSpawn.Clear();
            _rtvCountT.Clear();
            _rtvCountCT.Clear();
            knifemode = false;
            CTWINNER = false;
            TWINNER = false;
            BlockTeam = false;
            onroundstart = false;
            knifestarted = false;
            targetPlayerName = "";
            currentVotesT = 0;
            currentVotesCT = 0;
            smena = 0;
            ostavit = 0;
        }

        private void OnMapEnd()
        {
            Unload(false);
        }
    }
}
