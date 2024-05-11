    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using System.Text.Json.Serialization;
    using CounterStrikeSharp.API.Modules.Commands;
    using CounterStrikeSharp.API.Modules.Utils;
    using CounterStrikeSharp.API.Modules.Cvars;
    using CounterStrikeSharp.API.Core.Attributes.Registration;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Extensions.Localization;
    using CounterStrikeSharp.API.Modules.Timers;
    using CounterStrikeSharp.API.Core.Attributes;
    using System.Text.Json;

    namespace Knife_Round;

    [MinimumApiVersion(164)]
    public class KnifeRoundConfig : BasePluginConfig
    {
        [JsonPropertyName("GiveArmorOnKnifeRound")] public int GiveArmorOnKnifeRound { get; set; } = 1;
        [JsonPropertyName("FreezeOnVote")] public bool FreezeOnVote { get; set; } = false;
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

        internal static IStringLocalizer? Stringlocalizer;
        private Stopwatch stopwatch = new Stopwatch();
        private Dictionary<ulong, bool> OnSpawn = new Dictionary<ulong, bool>();
        public float mp_roundtime;
        public string mp_roundtimeFixed = "";
        public float mp_roundtime_defuse;
        public float mp_team_intro_time;
        public int currentVotesT;
        public int currentVotesCT;
        public bool knifemode = false;
        public bool CTWINNER = false;
        public bool TWINNER = false;
        public bool BlockTeam = false;
        public bool onroundstart = false;
        public bool knifestarted = false;
        public float timer;
        public string targetPlayerName = "";
        private List<ulong> _rtvCountCT = new();
        private List<ulong> _rtvCountT = new();
        public int smena = 0;
        public int ostavit = 0;
        private int readyCount = 0;


    public void OnConfigParsed(KnifeRoundConfig config)
        {
            Config = config;
            Stringlocalizer = Localizer;
            if(Config.GiveArmorOnKnifeRound < 0 || Config.GiveArmorOnKnifeRound > 2)
            {
                config.GiveArmorOnKnifeRound = 0;
            }
        }

        public override void Load(bool hotReload)
        {
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
    public bool WinMessageSent = false; // Добавляем флаг для отслеживания отправленного сообщения о победе в чат

    public void OnTick()
    {
        // Если включен режим ножей и блок команд
        if (knifemode && BlockTeam)
        {
            // Поиск всех игроков
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                // Проверка на валидность игрока и его оружия
                if (player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid) continue;
                var playerid = player.SteamID;

                // Удаление оружия, если оно не нож
                if (OnSpawn.ContainsKey(playerid))
                {
                    foreach (var weapon in player.PlayerPawn.Value.WeaponServices!.MyWeapons)
                    {
                        if (weapon is { IsValid: true, Value.IsValid: true } && !weapon.Value.DesignerName.Contains("weapon_knife"))
                        {
                            player.ExecuteClientCommand("slot3");
                            player.DropActiveWeapon();
                            weapon.Value.Remove();
                        }
                    }
                }
            }
        }

        // Если победа уже объявлена и сообщение отправлено, нет необходимости выполнять дополнительные действия
        if (TWINNER && WinMessageSent || CTWINNER && WinMessageSent)
        {
            return;
        }

        // Определение победителя и отправка сообщения в чат
        if (TWINNER || CTWINNER)
        {
            var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

            // Поиск всех игроков на победившей стороне и отправка каждому из них сообщения
            var winningPlayers = Utilities.GetPlayers().FindAll(p => p.TeamNum == (int)winningTeam);
            foreach (var player in winningPlayers)
            {
                if (player == null || !player.IsValid) continue;
                player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] Начало голосование"); // Изменение цвета AVA на фиолетовый
                player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] {ChatColors.Purple}!switch\x01 - смена стороны");
                player.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] {ChatColors.Purple}!stay\x01 - оставить сторону"); // Изменение цвета AVA на фиолетовый
            }

            WinMessageSent = true; // Устанавливаем флаг сообщения о победе в чат
        }
    }


    public void CheckPlayerCountAndSetWarmupTimer()
    {
        var playerCount = Utilities.GetPlayers().Count;
        if (playerCount >= 10)
        {
            Server.ExecuteCommand("mp_warmup_pausetimer 0");
        }
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        string o = "[";
        if (@event == null) return HookResult.Continue;
        if (onroundstart)
        {
            if (knifemode)
            {
                BlockTeam = true;
                knifestarted = true;
                // Вывод сообщения "[AVA] Ножи на готове?" при начале ножевого раунда
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

    [GameEventHandler]
    public HookResult EventRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        if(onroundstart && knifemode)
        {
            BlockTeam = true; 
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid)return HookResult.Continue;
        var playerid = player.SteamID;
        if(knifemode && BlockTeam)
        {
            if (!OnSpawn.ContainsKey(playerid))
            {
                OnSpawn.Add(playerid, true);
            }

            if (OnSpawn.ContainsKey(playerid))
            {
                if(Config.GiveArmorOnKnifeRound == 1)
                {
                    player.GiveNamedItem("item_kevlar");
                }else if(Config.GiveArmorOnKnifeRound == 2)
                {
                    player.GiveNamedItem("item_assaultsuit");
                }
                Server.NextFrame(() =>
                {
                    AddTimer(2.0f, () =>
                    {
                        OnSpawn.Remove(playerid); 
                    }, TimerFlags.STOP_ON_MAPCHANGE);
                });
            }
        }else if(!knifemode)
        {
            if(TWINNER == true || CTWINNER == true)
            {
                Server.NextFrame(() =>
                {
                    
                    if(Config.FreezeOnVote)
                    {
                        
                        if(player.PlayerPawn.Value != null && player.PlayerPawn.Value.IsValid){player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;}
                        
                    }
                    
                });
            }
        }   
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (@event == null || !knifemode) return HookResult.Continue;
        
        var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
        
        stopwatch.Start();
        timer = Config.VoteTimer;
        int countt = 0;
        int countct = 0;

        foreach (var player in playerEntities)
        {
            if (player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid)
                continue;

            if (player.TeamNum == (int)CsTeam.Terrorist && player.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            {
                countt++;
            }
        }

        foreach (var players in playerEntities)
        {
            if (players == null || !players.IsValid || players.PlayerPawn == null || !players.PlayerPawn.IsValid || players.PlayerPawn.Value == null || !players.PlayerPawn.Value.IsValid)
                continue;

            if (players.TeamNum == (int)CsTeam.CounterTerrorist && players.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            {
                countct++;
            }
        }

        if (countt > countct)
        {
            BlockTeam = true;
            TWINNER = true;
            knifemode = false;
        }
        else if (countct > countt)
        {
            BlockTeam = true;
            CTWINNER = true;
            knifemode = false;
        }
        else
        {
            BlockTeam = true;
            CTWINNER = true;
            knifemode = false;
        }
        // Добавляем разминку
        AddTimer(5.0f, () =>
        {
            Server.ExecuteCommand("mp_warmup_pausetimer 1");
            // Запускаем разминку
            Server.ExecuteCommand("mp_warmup_start");
        }, TimerFlags.STOP_ON_MAPCHANGE);
        return HookResult.Continue;
    }

    [ConsoleCommand("switch", "Switch teams after knife round.")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void SwitchTeamCommand(CCSPlayerController? player, CommandInfo cmd)
    {
        // Проверка, является ли игрок победителем
        if ((TWINNER && player.TeamNum == (int)CsTeam.Terrorist) || (CTWINNER && player.TeamNum == (int)CsTeam.CounterTerrorist))
        {
            ChangeTeamCommand(player, cmd, 1); // Вызов метода изменения команды
        }
    }

    [ConsoleCommand("stay", "Stay on current team after knife round.")]
    public void StayTeamCommand(CCSPlayerController? player, CommandInfo cmd)
    {
        // Проверка, является ли игрок победителем
        if ((TWINNER && player.TeamNum == (int)CsTeam.Terrorist) || (CTWINNER && player.TeamNum == (int)CsTeam.CounterTerrorist))
        {
            ChangeTeamCommand(player, cmd, 0); // Вызов метода изменения команды
        }
    }

    public void ChangeTeamCommand(CCSPlayerController? player, CommandInfo cmd, int switchTeam)
    {
        if (player == null || !player.IsValid) return;

        targetPlayerName = player.PlayerName;
        if (!player.UserId.HasValue || string.IsNullOrEmpty(targetPlayerName)) return;

        var rtvCount = switchTeam == 0 ? _rtvCountT : _rtvCountCT;
        var otherRtvCount = switchTeam == 0 ? _rtvCountCT : _rtvCountT;
        var teamToSwitch = switchTeam == 0 ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

        if (TWINNER || CTWINNER)
        {
            if (rtvCount.Contains(player!.SteamID))
            {
                rtvCount.Remove(player.SteamID);
                if (switchTeam == 0)
                    currentVotesT--;
                else
                {
                    currentVotesCT--;
                }


            }
            if (switchTeam == 0)
            {
                ostavit++;
            }
            else
            {
                smena++;
            }

            if (otherRtvCount.Contains(player!.SteamID)) return;
            otherRtvCount.Add(player.SteamID);

            var councT = Utilities.GetPlayers().Count(p => p.TeamNum == (int)CsTeam.Terrorist && !p.IsHLTV);
            var councCT = Utilities.GetPlayers().Count(p => p.TeamNum == (int)CsTeam.CounterTerrorist && !p.IsHLTV);
            var required = (int)Math.Ceiling((switchTeam == 0 ? councT : councCT) * 0.6);
            var currentVotes = switchTeam == 0 ? _rtvCountCT.Count : _rtvCountT.Count;

            if (currentVotes >= required)
            {
                if (switchTeam == 1)
                {
                    foreach (var pl in Utilities.GetPlayers().FindAll(x => x.IsValid))
                    {
                        pl.SwitchTeam(teamToSwitch);
                    }
                }

                Server.NextFrame(() =>
                {
                    _rtvCountT.Clear();
                    _rtvCountCT.Clear();
                    TWINNER = false;
                    CTWINNER = false;
                    BlockTeam = false;
                    int x = Config.AfterWinningRestartXTimes;
                    for (int i = 1; i <= x; i++)
                    {
                        float interval = i * 0.1f;

                        AddTimer(interval, () =>
                        {
                            string test = mp_roundtime.ToString();
                            string test2 = mp_roundtime_defuse.ToString();
                            string test3 = mp_team_intro_time.ToString();
                            if (test.Contains(',') || test2.Contains(',') || test3.Contains(','))
                            {
                                string replacedValue = test.Replace(',', '.');
                                string replacedValue2 = test2.Replace(',', '.');
                                string replacedValue3 = test3.Replace(',', '.');
                                Server.ExecuteCommand($"mp_team_intro_time {Config.TeamIntroTimeAfterKnife}; mp_warmup_start; mp_freezetime 15; sv_buy_status_override -1; mp_roundtime {replacedValue}; mp_roundtime_defuse {replacedValue2}; mp_give_player_c4 1; mp_warmup_end;");
                            }
                            else
                            {
                                Server.ExecuteCommand($"mp_team_intro_time {Config.TeamIntroTimeAfterKnife}; mp_warmup_start; mp_freezetime 15; sv_buy_status_override -1; mp_roundtime {mp_roundtime}; mp_roundtime_defuse {mp_roundtime_defuse}; mp_give_player_c4 1; mp_warmup_end;");
                            }
                        }, TimerFlags.STOP_ON_MAPCHANGE);
                    }
                });
            }

            var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
            // Поиск всех игроков на победившей стороне и отправка каждому из них сообщения
            var winningPlayers = Utilities.GetPlayers().FindAll(p => p.TeamNum == (int)winningTeam);

            foreach (var playerWin in winningPlayers)
            {
                if (playerWin == null || !player.IsValid) continue;
                playerWin.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] {ChatColors.Purple}!switch\x01 - {ChatColors.Red}{smena} голосов");
                playerWin.PrintToChat($"[{ChatColors.Purple}{Config.ChatDisplayName}\x01] {ChatColors.Purple}!stay\x01 - {ChatColors.Red}{ostavit} голосов"); // Изменение цвета AVA на фиолетовый
            }
        }
    }

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
        readyCount = 0;
    }

    private void OnMapEnd()
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
}