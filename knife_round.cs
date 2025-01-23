using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

// Допустим, ваш конфиг
using MyCustomConfig;
using CounterStrikeSharp.API.Modules.Entities;
using System.Numerics;

namespace knife_round;

[MinimumApiVersion(164)]
public class KnifeRoundPlugin : BasePlugin
{
    public override string ModuleName => "Knife Round";
    public override string ModuleVersion => "3.3.0";

    // --------------------------------------
    //   Глобальные поля
    // --------------------------------------
    public bool knifemode = false;   // идёт ли нож
    public bool TWINNER = false;     // кто победил нож
    public bool CTWINNER = false;
    public bool BlockTeam = false;   // блокируем jointeam
    public bool infiniteWarmupActive = true; // «вечная» разминка до !start
    public bool WinMessageSent = false;      // отправляли ли сообщение о !switch / !stay

    // Голоса switch/stay
    public int smena = 0;   // !switch
    public int ostavit = 0; // !stay
    public List<ulong> _rtvCountCT = new();
    public List<ulong> _rtvCountT = new();

    // Голоса за !start
    public Dictionary<ulong, bool> playersStartVoted = new();

    // Сохраняем «обычные» cvars
    public float mp_roundtime;
    public float mp_roundtime_defuse;
    public float mp_team_intro_time;

    // Для удаления оружия кроме ножа
    private Dictionary<ulong, bool> OnSpawn = new();

    // --------------------------------------
    //   LOAD / UNLOAD
    // --------------------------------------
    public override void Load(bool hotReload)
    {
        // Загружаем конфиг
        MyConfigManager.LoadConfig();

        // Ставим вечную разминку
        Server.ExecuteCommand("mp_warmup_pausetimer 1");

        AddCommandListener("jointeam", OnCommandJoinTeam, HookMode.Pre);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

        Console.WriteLine("[KnifeRoundPlugin] Loaded. V3.3.0");
    }

    public override void Unload(bool hotReload)
    {
        knifemode = false;
        TWINNER = false;
        CTWINNER = false;
        BlockTeam = false;
        infiniteWarmupActive = false;
        WinMessageSent = false;

        smena = 0;
        ostavit = 0;
        _rtvCountCT.Clear();
        _rtvCountT.Clear();
        playersStartVoted.Clear();
        OnSpawn.Clear();

        Console.WriteLine("[KnifeRoundPlugin] Unloaded.");
    }

    // --------------------------------------
    //   События подключения/уход
    // --------------------------------------
    private void OnClientPutInServer(int slot)
    {
        var pl = Utilities.GetPlayerFromSlot(slot);
        if (pl == null || !pl.IsValid) return;
        if (IsBotPlayer(pl)) return;

        if (!playersStartVoted.ContainsKey(pl.SteamID))
            playersStartVoted[pl.SteamID] = false;
    }

    private void OnClientDisconnect(int slot)
    {
        var pl = Utilities.GetPlayerFromSlot(slot);
        if (pl == null || !pl.IsValid) return;

        if (playersStartVoted.ContainsKey(pl.SteamID))
            playersStartVoted.Remove(pl.SteamID);
    }

    // --------------------------------------
    //   Команда !start (до ножа)
    // --------------------------------------
    [ConsoleCommand("start", "Vote to start the knife round.")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void StartGameCommand(CCSPlayerController? player, CommandInfo cmd)
    {
        if (player == null || !player.IsValid) return;
        if (IsBotPlayer(player)) return;

        // Чтобы нельзя было вызвать !start после ножа
        if (!infiniteWarmupActive)
        {
            PrintToChat(player, "!start доступна только до начала ножевого раунда.");
            return;
        }

        ulong sid = player.SteamID;
        if (!playersStartVoted.ContainsKey(sid))
            playersStartVoted[sid] = false;

        if (playersStartVoted[sid])
        {
            PrintToChat(player, "Вы уже голосовали за старт ножа!");
            return;
        }

        playersStartVoted[sid] = true;
        PrintToChat(player, "Ваш голос за запуск ножевого учтён.");

        CheckAllStartVotes();
    }

    private void CheckAllStartVotes()
    {
        var realPlayers = Utilities.GetPlayers().Where(p => !IsBotPlayer(p) && !p.IsHLTV).ToList();
        if (realPlayers.Count == 0) return;

        bool allVoted = true;
        foreach (var rp in realPlayers)
        {
            if (!playersStartVoted.ContainsKey(rp.SteamID) || !playersStartVoted[rp.SteamID])
            {
                allVoted = false;
                break;
            }
        }

        if (allVoted)
        {
            Console.WriteLine("[KnifeRound] Все реальные игроки проголосовали. Запускаем ножевой.");

            infiniteWarmupActive = false;
            Server.ExecuteCommand("mp_warmup_end");

            mp_roundtime = ConVar.Find("mp_roundtime")!.GetPrimitiveValue<float>();
            mp_roundtime_defuse = ConVar.Find("mp_roundtime_defuse")!.GetPrimitiveValue<float>();
            mp_team_intro_time = ConVar.Find("mp_team_intro_time")!.GetPrimitiveValue<float>();

            knifemode = true;
            BlockTeam = true;

            // Настройки ножа
            float knifeIntro = MyConfigManager.Config.TeamIntroTimeKnifeStart; // напр. 6.5
            float knifeTime = MyConfigManager.Config.KnifeRoundTimer; // напр. 2
            // freezetime = 15 (как вы просили)
            Server.ExecuteCommand(
                $"mp_team_intro_time {knifeIntro}; " +
                $"mp_roundtime {knifeTime}; " +
                $"mp_roundtime_defuse {knifeTime}; " +
                $"mp_give_player_c4 0;" +
                $"sv_buy_status_override 3;" + // 3 => отключить магазин
                $"mp_freezetime 15;" +
                $"mp_warmup_pausetimer 0"
            );

            // Запускаем mp_restartgame <knifeIntro> => короткий отсчёт, затем нож
            Server.ExecuteCommand($"mp_restartgame {knifeIntro}");

            Utilities.GetPlayers().ForEach(p =>
            {
                if (p.IsValid)
                {
                    PrintToChat(p, "Ножевой раунд запускается!");
                }
            });
        }
    }

    // --------------------------------------
    //    !switch / !stay
    // --------------------------------------
    [ConsoleCommand("switch", "Switch teams after knife round.")]
    public void SwitchTeamCommand(CCSPlayerController? p, CommandInfo cmd)
    {
        if (p == null || !p.IsValid) return;
        if ((TWINNER && p.TeamNum == (int)CsTeam.Terrorist)
            || (CTWINNER && p.TeamNum == (int)CsTeam.CounterTerrorist))
        {
            ChangeTeamVote(p, true);
        }
    }

    [ConsoleCommand("stay", "Stay on current team after knife round.")]
    public void StayTeamCommand(CCSPlayerController? p, CommandInfo cmd)
    {
        if (p == null || !p.IsValid) return;
        if ((TWINNER && p.TeamNum == (int)CsTeam.Terrorist)
            || (CTWINNER && p.TeamNum == (int)CsTeam.CounterTerrorist))
        {
            ChangeTeamVote(p, false);
        }
    }

    private void ChangeTeamVote(CCSPlayerController player, bool isSwitch)
    {
        ulong sid = player.SteamID;
        if (isSwitch)
        {
            if (_rtvCountT.Contains(sid)) { _rtvCountT.Remove(sid); ostavit--; }
            if (!_rtvCountCT.Contains(sid)) { _rtvCountCT.Add(sid); smena++; }
        }
        else
        {
            if (_rtvCountCT.Contains(sid)) { _rtvCountCT.Remove(sid); smena--; }
            if (!_rtvCountT.Contains(sid)) { _rtvCountT.Add(sid); ostavit++; }
        }

        var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        var realWinners = Utilities.GetPlayers()
            .Where(p => p.TeamNum == (int)winningTeam && !IsBotPlayer(p) && !p.IsHLTV)
            .ToList();
        if (realWinners.Count == 0) return;

        int required = (int)Math.Ceiling(realWinners.Count * 0.6);
        if (smena >= required)
        {
            ForceSidesChosen(true);
        }
        else if (ostavit >= required)
        {
            ForceSidesChosen(false);
        }
        else
        {
            PrintVoteInfo();
        }
    }

    private void PrintVoteInfo()
    {
        var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        var winners = Utilities.GetPlayers().Where(w => w.TeamNum == (int)winningTeam);
        foreach (var w in winners)
        {
            if (!w.IsValid) continue;
            w.PrintToChat($"{ChatColors.Green}[" + $"{ChatColors.Purple}AVA{ChatColors.Green}]!switch => {ChatColors.Purple}{smena}{ChatColors.Green} голосов, !stay => {ChatColors.Purple}{ostavit}{ChatColors.Green} голосов"
            );
        }
    }

    // Когда голосов достаточно — запускаем логику обычной игры
    private void ForceSidesChosen(bool doSwitch)
    {
        TWINNER = false;
        CTWINNER = false;
        BlockTeam = false;

        _rtvCountCT.Clear();
        _rtvCountT.Clear();

        if (doSwitch)
        {
            // Меняем команды
            foreach (var pl in Utilities.GetPlayers())
            {
                if (!pl.IsValid) continue;
                if (pl.TeamNum == (int)CsTeam.Terrorist)
                    pl.SwitchTeam(CsTeam.CounterTerrorist);
                else if (pl.TeamNum == (int)CsTeam.CounterTerrorist)
                    pl.SwitchTeam(CsTeam.Terrorist);
            }
        }

        // Включаем магазин, freezetime=15, C4=1
        float normalIntro = 10f; // пробуем 10
        Server.ExecuteCommand(
            $"mp_team_intro_time {normalIntro}; " +
            $"mp_roundtime {mp_roundtime}; " +
            $"mp_roundtime_defuse {mp_roundtime_defuse}; " +
            $"mp_freezetime 15; " +
            $"sv_buy_status_override -1; " +
            $"mp_give_player_c4 1;" +
            $"mp_warmup_end"
        );

        // mp_restartgame 1 => короткая пауза
    Server.ExecuteCommand($"mp_restartgame {normalIntro}");

    // Через normalIntro + 0.5 → говорим "Игра началась!"
    AddTimer(normalIntro + 0.5f, () =>
    {
        foreach (var pl in Utilities.GetPlayers())
        {
            if (pl.IsValid)
            {
                pl.PrintToChat($"{ChatColors.Green}Игра началась!!!");
            }
        }
        Unload(false);
    });
    }

    // --------------------------------------
    //   OnRoundEnd(нож)
    // --------------------------------------
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundEnd(EventRoundEnd e, GameEventInfo info)
    {
        if (!knifemode) return HookResult.Continue;
        if (e == null) return HookResult.Continue;

        // Подсчитываем победителя
        int countT = 0, countCT = 0;
        var players = Utilities.GetPlayers();
        foreach (var p in players)
        {
            if (!p.IsValid) continue;
            if (p.PlayerPawn?.Value != null
                && p.PlayerPawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            {
                if (p.TeamNum == (int)CsTeam.Terrorist) countT++;
                else if (p.TeamNum == (int)CsTeam.CounterTerrorist) countCT++;
            }
        }
        if (countT > countCT) TWINNER = true;
        else if (countCT > countT) CTWINNER = true;
        else CTWINNER = true; // при равенстве => CT

        knifemode = false;
        BlockTeam = true;

        // Вот здесь — «пауза» + «win панель»?
        // 1) Например, mp_match_end_restart 1 => заставляем показать "экран конца матча"
        // 2) Через 2 секунды запускаем «разминку 180 сек» для выбора сторон
        Server.ExecuteCommand("mp_match_end_restart 1"); // Показывает панель победителя
        // Затем через 2 секунды — разминка для голосования
        AddTimer(2.0f, () =>
        {
            // Запускаем разминку на 180 сек
            Server.ExecuteCommand("mp_warmuptime 180");
            Server.ExecuteCommand("mp_warmup_start");

            // Когда 180 сек истечёт — смотрим, кто победил (switch / stay)
            AddTimer(180f, () => ResolveSideChoiceTimeExpired(), TimerFlags.STOP_ON_MAPCHANGE);
        });

        foreach (var pl in players)
        {
            if (!pl.IsValid) continue;
            PrintToChat(pl, "Ножевой закончился! Победители могут голосовать !switch / !stay в течение 180 секунд.");
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// Вызывается, когда 180 секунд разминки на голосование истекли.
    /// </summary>
    private void ResolveSideChoiceTimeExpired()
    {
        // Если TWINNER||CTWINNER всё ещё true — значит, BlockTeam => никто не завершил голос
        // Смотрим smena vs ostavit
        if (!BlockTeam)
        {
            // Стороны уже выбраны
            return;
        }

        // 1) Сравниваем smena vs ostavit
        if (smena > ostavit)
        {
            ForceSidesChosen(true);
        }
        else if (ostavit > smena)
        {
            ForceSidesChosen(false);
        }
        else
        {
            // Равенство / нет голосов => switch
            ForceSidesChosen(true);
        }
    }

    // --------------------------------------
    //  OnPlayerSpawn => выдача ножевой брони
    // --------------------------------------
    [GameEventHandler]
    public HookResult EventPlayerSpawn(EventPlayerSpawn e, GameEventInfo info)
    {
        if (e == null) return HookResult.Continue;
        var p = e.Userid;
        if (p == null || !p.IsValid) return HookResult.Continue;

        if (knifemode && BlockTeam)
        {
            ulong sid = p.SteamID;
            OnSpawn[sid] = true;

            // Выдаём броню:
            if (MyConfigManager.Config.GiveArmorOnKnifeRound == 1)
                p.GiveNamedItem("item_kevlar");
            else if (MyConfigManager.Config.GiveArmorOnKnifeRound == 2)
                p.GiveNamedItem("item_assaultsuit");

            AddTimer(2f, () => { OnSpawn.Remove(sid); }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        else
        {
            if ((TWINNER || CTWINNER) && MyConfigManager.Config.FreezeOnVote)
            {
                Server.NextFrame(() =>
                {
                    if (p.PlayerPawn?.Value != null)
                        p.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
                });
            }
        }

        return HookResult.Continue;
    }

    // --------------------------------------
    //  OnTick => убираем оружие кроме ножа
    // --------------------------------------
    public void OnTick()
    {
        if (infiniteWarmupActive) return;

        if (knifemode && BlockTeam)
        {
            var plist = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var p in plist)
            {
                if (p == null || !p.IsValid)
                    continue;

                if (p.PlayerPawn?.Value == null || !p.PlayerPawn.Value.IsValid)
                    continue;

                if (p.PlayerPawn.Value.WeaponServices?.MyWeapons == null)
                    continue;

                foreach (var w in p.PlayerPawn.Value.WeaponServices.MyWeapons)
                {
                    if (w == null || !w.IsValid || w.Value == null || !w.Value.IsValid)
                        continue;

                    if (!w.Value.DesignerName.Contains("weapon_knife"))
                    {
                        p.ExecuteClientCommand("slot3");
                        p.DropActiveWeapon();
                        w.Value.Remove();
                    }
                }
            }
        }

        if ((TWINNER || CTWINNER) && !WinMessageSent)
        {
            var winningTeam = TWINNER ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
            var wList = Utilities.GetPlayers().Where(x => x.TeamNum == (int)winningTeam);
            foreach (var w in wList)
            {
                if (!w.IsValid) continue;
                PrintToChat(w, "Используйте !switch / !stay для выбора стороны.");
            }
            WinMessageSent = true;
        }
    }

    // --------------------------------------
    //  jointeam (блок)
    // --------------------------------------
    private HookResult OnCommandJoinTeam(CCSPlayerController? p, CommandInfo cmd)
    {
        if (p == null || !p.IsValid) return HookResult.Continue;
        if (MyConfigManager.Config.BlockTeamChangeOnVoteAndKnife && BlockTeam)
        {
            return HookResult.Handled;
        }
        return HookResult.Continue;
    }

    // --------------------------------------
    //  OnMapEnd => сброс
    // --------------------------------------
    private void OnMapEnd()
    {
        knifemode = false;
        TWINNER = false;
        CTWINNER = false;
        BlockTeam = false;
        infiniteWarmupActive = false;
        WinMessageSent = false;

        _rtvCountCT.Clear();
        _rtvCountT.Clear();
        playersStartVoted.Clear();
        OnSpawn.Clear();

        smena = 0;
        ostavit = 0;
    }

    // --------------------------------------
    //  Помощник: бот?
    // --------------------------------------
    public bool IsBotPlayer(CCSPlayerController pl)
    {
        if (!pl.IsValid) return false;
        return (pl.SteamID == 0UL || pl.IsBot);
    }

    private static void PrintToChat(CCSPlayerController p, string message)
    {
        p.PrintToChat($"{ChatColors.Green}[{ChatColors.Purple}AVA{ChatColors.Green}] {message}");
    }
}
