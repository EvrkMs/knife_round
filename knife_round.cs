using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes;

using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Linq;

// ��� ��������� ������
using MyCustomConfig;

namespace Knife_Round
{
    [MinimumApiVersion(164)]
    public class KnifeRound : BasePlugin
    {
        public override string ModuleName => "Knife Round";
        public override string ModuleVersion => "2.0.1";

        private Stopwatch stopwatch = new();

        // ��������� �������
        private Dictionary<ulong, bool> OnSpawn = new();

        public float mp_roundtime;
        public float mp_roundtime_defuse;
        public float mp_team_intro_time;

        public bool knifemode = false;
        public bool CTWINNER = false;
        public bool TWINNER = false;
        public bool BlockTeam = false;
        public bool onroundstart = false;
        public bool knifestarted = false;
        public bool WinMessageSent = false;

        public int currentVotesT = 0;
        public int currentVotesCT = 0;
        public int smena = 0;
        public int ostavit = 0;
        public int readyCount = 0;

        private string targetPlayerName = "";

        // ������ ��� �����������
        private List<ulong> _rtvCountCT = new();
        private List<ulong> _rtvCountT = new();

        public override void Load(bool hotReload)
        {
            // ��������� ���� ������
            MyConfigManager.LoadConfig();

            AddCommandListener("jointeam", OnCommandJoinTeam, HookMode.Pre);
            RegisterListener<Listeners.OnTick>(OnTick);
            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);

            Console.WriteLine("[KnifeRound] Loaded. Custom config from C:\\server\\config\\my_knife_config.json");
        }

        // =================== �����������, ��� ��� ��� ==========================
        private bool IsBotPlayer(CCSPlayerController? pl)
        {
            // � ��������� ������ ���� = SteamID = 0
            if (pl == null || !pl.IsValid) return false;
            return (pl.SteamID == 0UL);
        }

        // =====================================================================
        //                      JoinTeam �������
        // =====================================================================
        private HookResult OnCommandJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
        {
            // ��������� jointeam, ���� ������� BlockTeamChangeOnVoteAndKnife
            if (MyConfigManager.Config.BlockTeamChangeOnVoteAndKnife && BlockTeam)
            {
                return HookResult.Handled;
            }
            return HookResult.Continue;
        }

        // =====================================================================
        //                              OnTick
        // =====================================================================
        public void OnTick()
        {
            // ������� ������, ����� ����, ���� ����� �����
            if (knifemode && BlockTeam)
            {
                var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
                foreach (var player in playerEntities)
                {
                    if (player == null || !player.IsValid
                        || player.PlayerPawn == null || !player.PlayerPawn.IsValid
                        || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid)
                        continue;

                    var playerid = player.SteamID;
                    if (OnSpawn.ContainsKey(playerid))
                    {
                        foreach (var weapon in player.PlayerPawn.Value.WeaponServices!.MyWeapons)
                        {
                            if (weapon is { IsValid: true, Value.IsValid: true }
                                && !weapon.Value.DesignerName.Contains("weapon_knife"))
                            {
                                player.ExecuteClientCommand("slot3");
                                player.DropActiveWeapon();
                                weapon.Value.Remove();
                            }
                        }
                    }
                }
            }

            // ���� ��� �������� ���������� � ������ ��������� - ��
            if ((TWINNER && WinMessageSent) || (CTWINNER && WinMessageSent))
            {
                return;
            }

            // ���� ���-�� ������� (TWINNER/CTWINNER), ������� �����������
            if (TWINNER || CTWINNER)
            {
                var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                var winningPlayers = Utilities.GetPlayers().Where(p => p.TeamNum == (int)winningTeam).ToList();

                foreach (var pl in winningPlayers)
                {
                    if (pl == null || !pl.IsValid) continue;
                    pl.PrintToChat($"[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01] {ChatColors.Green}{MyConfigManager.Config.VoitMessgae}");
                    pl.PrintToChat($"[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01] {ChatColors.Purple}!switch{ChatColors.Green} - {MyConfigManager.Config.SwitchMeesage}");
                    pl.PrintToChat($"[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01] {ChatColors.Purple}!stay{ChatColors.Green} - {MyConfigManager.Config.StayMeesage}");
                }

                WinMessageSent = true;
            }
        }

        // =====================================================================
        //                   ������� ������/����� ������
        // =====================================================================
        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (@event == null)
                return HookResult.Continue;

            if (onroundstart)
            {
                if (knifemode)
                {
                    BlockTeam = true;
                    knifestarted = true;
                    // "���� �� ������"
                    Utilities.GetPlayers().ForEach(player =>
                    {
                        if (player != null && player.IsValid)
                        {
                            player.PrintToChat($"{ChatColors.Blue}[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01]{ChatColors.Blue}{MyConfigManager.Config.StartMessage}");
                        }
                    });
                }
            }
            else
            {
                // ���������� cvars
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
                    Server.ExecuteCommand(
                        $"mp_team_intro_time {MyConfigManager.Config.TeamIntroTimeKnifeStart}; " +
                        $"sv_buy_status_override 3; " +
                        $"mp_roundtime {MyConfigManager.Config.KnifeRoundTimer}; " +
                        $"mp_roundtime_defuse {MyConfigManager.Config.KnifeRoundTimer}; " +
                        $"mp_give_player_c4 0"
                    );
                });
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult EventRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
        {
            if (onroundstart && knifemode)
            {
                BlockTeam = true;
            }
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            if (@event == null)
                return HookResult.Continue;

            var player = @event.Userid;
            if (player == null || !player.IsValid
                || player.PlayerPawn == null || !player.PlayerPawn.IsValid
                || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid)
            {
                return HookResult.Continue;
            }

            var playerid = player.SteamID;

            if (knifemode && BlockTeam)
            {
                if (!OnSpawn.ContainsKey(playerid))
                {
                    OnSpawn.Add(playerid, true);
                }

                if (OnSpawn.ContainsKey(playerid))
                {
                    // ����� �����
                    if (MyConfigManager.Config.GiveArmorOnKnifeRound == 1)
                    {
                        player.GiveNamedItem("item_kevlar");
                    }
                    else if (MyConfigManager.Config.GiveArmorOnKnifeRound == 2)
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
            }
            else if (!knifemode)
            {
                // ���� ��� ����������, �� ��� �����������
                if (TWINNER || CTWINNER)
                {
                    Server.NextFrame(() =>
                    {
                        if (MyConfigManager.Config.FreezeOnVote)
                        {
                            if (player.PlayerPawn.Value.IsValid)
                            {
                                player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
                            }
                        }
                    });
                }
            }

            return HookResult.Continue;
        }

        [GameEventHandler(HookMode.Post)]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (@event == null || !knifemode)
                return HookResult.Continue;

            stopwatch.Start();
            int countT = 0;
            int countCT = 0;

            var players = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var p in players)
            {
                if (p == null || !p.IsValid
                    || p.PlayerPawn == null || !p.PlayerPawn.IsValid
                    || p.PlayerPawn.Value == null || !p.PlayerPawn.Value.IsValid)
                    continue;

                // ��������
                if (p.TeamNum == (int)CsTeam.Terrorist && p.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                    countT++;

                if (p.TeamNum == (int)CsTeam.CounterTerrorist && p.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                    countCT++;
            }

            if (countT > countCT)
            {
                TWINNER = true;
            }
            else if (countCT > countT)
            {
                CTWINNER = true;
            }
            else
            {
                // ��� ��������� CT
                CTWINNER = true;
            }

            BlockTeam = true;
            knifemode = false;

            // ��������� �������� �� X ������
            // � ����� X ������, ���� ����� �� ������, ������ stay
            AddTimer(5.0f, () =>
            {
                Server.ExecuteCommand($"mp_warmuptime {MyConfigManager.Config.WarmupTimeAfterKnif}");
                Server.ExecuteCommand("mp_warmup_start");

                // ����� WarmupTimeAfterKnif ������ �������� OnWarmupTimeExpired
                AddTimer(MyConfigManager.Config.WarmupTimeAfterKnif, OnWarmupTimeExpired, TimerFlags.STOP_ON_MAPCHANGE);

            }, TimerFlags.STOP_ON_MAPCHANGE);

            return HookResult.Continue;
        }

        /// <summary>
        /// ���������� �� �������, ����� WarmupTimeAfterKnif ��������.
        /// ���� ������� ��� � �� ������� (BlockTeam �� ��� true), �� ������ stay.
        /// </summary>
        private void OnWarmupTimeExpired()
        {
            // ���� BlockTeam ��� ������� (������, sides �������) - ������ �� ������
            if (!BlockTeam)
                return;

            // ������� �� ������� => ������ "stay"
            // �� ����, ��� �� �� ������, ��� "if (switchTeam==0)" ��������� ������ ���-�� �������
            Console.WriteLine("[KnifeRound] Warmup ended with no side chosen. Forcing !stay.");

            ForceStay();
        }

        /// <summary>
        /// ������������� ��������� ������ "�������� �� ������� �������"
        /// (��� ���� �� ��� ������������� !stay � �������� ������� ��������).
        /// </summary>
        private void ForceStay()
        {
            // ������ ������� �����, ������ �������, ��� � ����� "ChangeTeamCommand"
            _rtvCountT.Clear();
            _rtvCountCT.Clear();
            TWINNER = false;
            CTWINNER = false;
            BlockTeam = false;

            int x = MyConfigManager.Config.AfterWinningRestartXTimes;
            for (int i = 1; i <= x; i++)
            {
                float interval = i * 0.1f;
                AddTimer(interval, () =>
                {
                    // ������ mp_team_intro_time => �����
                    Server.ExecuteCommand($"mp_team_intro_time {MyConfigManager.Config.TeamIntroTimeAfterKnife}");
                    // �������
                    Server.ExecuteCommand("mp_restartgame 1");

                    // ����� ������� ���������� cvars
                    AddTimer(1.0f, () =>
                    {
                        string val1 = mp_roundtime.ToString().Replace(',', '.');
                        string val2 = mp_roundtime_defuse.ToString().Replace(',', '.');
                        string val3 = mp_team_intro_time.ToString().Replace(',', '.');

                        Server.ExecuteCommand(
                            $"mp_warmup_end; " +
                            $"mp_team_intro_time {val3}; " + // ���������� ������
                            $"mp_roundtime {val1}; " +
                            $"mp_roundtime_defuse {val2}; " +
                            $"mp_give_player_c4 1;"
                        );
                    });
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }

        // =====================================================================
        //                         ����������� !switch / !stay
        // =====================================================================
        [ConsoleCommand("switch", "Switch teams after knife round.")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SwitchTeamCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if ((TWINNER && player?.TeamNum == (int)CsTeam.Terrorist)
                || (CTWINNER && player?.TeamNum == (int)CsTeam.CounterTerrorist))
            {
                ChangeTeamCommand(player, cmd, 1);
            }
        }

        [ConsoleCommand("stay", "Stay on current team after knife round.")]
        public void StayTeamCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if ((TWINNER && player?.TeamNum == (int)CsTeam.Terrorist)
                || (CTWINNER && player?.TeamNum == (int)CsTeam.CounterTerrorist))
            {
                ChangeTeamCommand(player, cmd, 0);
            }
        }

        private void ChangeTeamCommand(CCSPlayerController? player, CommandInfo cmd, int switchTeam)
        {
            if (player == null || !player.IsValid) return;

            // �� ��������� ����� ����
            if (IsBotPlayer(player)) return;

            targetPlayerName = player.PlayerName;
            if (!player.UserId.HasValue || string.IsNullOrEmpty(targetPlayerName))
                return;

            var rtvCount = (switchTeam == 0) ? _rtvCountT : _rtvCountCT;
            var otherRtvCount = (switchTeam == 0) ? _rtvCountCT : _rtvCountT;
            var teamToSwitch = (switchTeam == 0) ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

            if (TWINNER || CTWINNER)
            {
                if (rtvCount.Contains(player.SteamID))
                {
                    rtvCount.Remove(player.SteamID);
                    if (switchTeam == 0) currentVotesT--;
                    else currentVotesCT--;
                }

                if (switchTeam == 0) ostavit++;
                else smena++;

                if (otherRtvCount.Contains(player.SteamID))
                    return;

                otherRtvCount.Add(player.SteamID);

                // ������� �������� ������ (�� ����, �� HLTV)
                var councT = Utilities.GetPlayers().Count(
                    p => p.TeamNum == (int)CsTeam.Terrorist && !p.IsHLTV && !IsBotPlayer(p)
                );
                var councCT = Utilities.GetPlayers().Count(
                    p => p.TeamNum == (int)CsTeam.CounterTerrorist && !p.IsHLTV && !IsBotPlayer(p)
                );

                var required = (int)Math.Ceiling((switchTeam == 0 ? councT : councCT) * 0.6);
                var currentVotes = (switchTeam == 0) ? _rtvCountCT.Count : _rtvCountT.Count;

                if (currentVotes >= required)
                {
                    // ���� switchTeam=1 (����� �������) - ��������� ����
                    if (switchTeam == 1)
                    {
                        foreach (var pl in Utilities.GetPlayers().Where(x => x.IsValid))
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

                        int x = MyConfigManager.Config.AfterWinningRestartXTimes;
                        for (int i = 1; i <= x; i++)
                        {
                            float interval = i * 0.1f;
                            AddTimer(interval, () =>
                            {
                                // ������ �����
                                Server.ExecuteCommand($"mp_team_intro_time {MyConfigManager.Config.TeamIntroTimeAfterKnife}");
                                Server.ExecuteCommand("mp_restartgame 1");

                                // ����� ������� ���������� cvars
                                AddTimer(1.0f, () =>
                                {
                                    string val1 = mp_roundtime.ToString().Replace(',', '.');
                                    string val2 = mp_roundtime_defuse.ToString().Replace(',', '.');
                                    string val3 = mp_team_intro_time.ToString().Replace(',', '.');

                                    Server.ExecuteCommand(
                                        $"mp_warmup_end; " +
                                        $"mp_team_intro_time {val3}; " +
                                        $"mp_roundtime {val1}; " +
                                        $"mp_roundtime_defuse {val2}; " +
                                        $"mp_give_player_c4 1;"
                                    );
                                });
                            }, TimerFlags.STOP_ON_MAPCHANGE);
                        }
                    });
                }

                // ����� �������
                var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                var winningPlayers = Utilities.GetPlayers().Where(p => p.TeamNum == (int)winningTeam);
                foreach (var pw in winningPlayers)
                {
                    if (pw == null || !pw.IsValid) continue;

                    pw.PrintToChat($"[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01] {ChatColors.Purple}!switch\x01 - {ChatColors.Red}{smena} �������");
                    pw.PrintToChat($"[{ChatColors.Purple}{MyConfigManager.Config.ChatDisplayName}\x01] {ChatColors.Purple}!stay\x01 - {ChatColors.Red}{ostavit} �������");
                }
            }
        }

        // =====================================================================
        //                  ����������/������������ �������
        // =====================================================================
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
}