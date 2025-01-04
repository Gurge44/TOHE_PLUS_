using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using static EHR.Translator;

namespace EHR.Modules;

public enum CustomRPC
{
    VersionCheck = 102,
    RequestRetryVersionCheck,
    SyncCustomSettings,
    SetDeathReason,
    EndGame,
    PlaySound,
    SetCustomRole,
    SetNameColorData,
    SetRealKiller,
    ShowChat,
    SyncLobbyTimer,
    AntiBlackout,
    PlayCustomSound,
    SetKillTimer,
    SyncAllPlayerNames,
    SyncAllClientRealNames,
    SyncNameNotify,
    ShowPopUp,
    KillFlash,
    SyncAbilityUseLimit,
    RemoveAbilityUseLimit,
    RemoveSubRole,
    Arrow,
    FixModdedClientCNO,
    SyncAbilityCD,
    SyncGeneralOptions,
    SyncRoleData,
    RequestSendMessage,
    NotificationPopper,
    RequestCommandProcessing,

    // Roles
    DoSpell,
    SniperSync,
    SetLoversPlayers,
    SetExecutionerTarget,
    RemoveExecutionerTarget,
    SetLawyerTarget,
    RemoveLawyerTarget,
    SendFireWorksState,
    SetCurrentDousingTarget,
    SetEvilTrackerTarget,
    SetBountyTarget,
    SetKillOrSpell,
    SetDousedPlayer,
    SetPlaguedPlayer,
    SetDrawPlayer,
    SyncHeadHunter,
    SyncRabbit,
    SyncSoulHunter,

    // BAU should always be 150
    BAU = 150,

    SyncMycologist,
    SyncBubble,
    AddTornado,
    SyncHookshot,
    SyncStressedTimer,
    SetLibrarianMode,
    SyncYinYanger,
    DruidAddTrigger,
    SyncBenefactorMarkedTask,
    SyncMafiosoData,
    SyncMafiosoPistolCD,
    SyncDamoclesTimer,
    SyncChronomancer,
    PenguinSync,
    SyncPlagueDoctor,
    SetAlchemistPotion,
    SetRicochetTarget,
    SetTetherTarget,
    SetHitmanTarget,
    SetWeaponMasterMode,
    SyncGlitchTimers,
    SyncSpy,
    SetSabotageMasterLimit,
    SetNiceHackerLimit,
    SetCurrentDrawTarget,
    SetCpTasksDone,
    SetGamerHealth,
    SetPelicanEtenNum,
    SwordsManKill,
    SetGhostPlayer,
    SetDarkHiderKillCount,
    SetEvilDiviner,
    SetGreedierOe,
    SetCollectorVotes,
    SetQuickShooterShotLimit,
    GuessKill,
    SetMarkedPlayer,
    SetMedicalerProtectList,
    SyncPsychicRedList,
    SetCleanserCleanLimit,
    SetJailorTarget,
    SetDoppelgangerStealLimit,
    SetJailorExeLimit,
    SetWwTimer,
    SetNiceSwapperVotes,
    Judge,
    Guess,
    MeetingKill,
    MafiaRevenge,
    SetSwooperTimer,
    SetBanditStealLimit,
    SetBkTimer,
    SyncTotocalcioTargetAndTimes,
    SyncRomanticTarget,
    SyncVengefulRomanticTarget,
    SetRevealedPlayer,
    SetCurrentRevealTarget,
    SetDoomsayerProgress,
    SetTrackerTarget,
    RpcPassBomb,
    SetAlchemistTimer,
    SyncPostman,
    SyncChangeling,
    SyncTiger,
    SyncSentry,
    SyncBargainer,
    SyncOverheat,
    SyncIntrovert,
    SyncAllergic,

    // Game Modes
    RoomRushDataSync,
    FFAKill
}

public enum Sounds
{
    KillSound,
    TaskComplete,
    TaskUpdateSound,
    ImpTransform
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class RPCHandlerPatch
{
    public static readonly Dictionary<byte, int> ReportDeadBodyRPCs = [];

    private static bool TrustedRpc(byte id)
    {
        return (CustomRPC)id is CustomRPC.VersionCheck or CustomRPC.RequestRetryVersionCheck or CustomRPC.AntiBlackout or CustomRPC.SyncNameNotify or CustomRPC.RequestSendMessage or CustomRPC.RequestCommandProcessing or CustomRPC.Judge or CustomRPC.SetNiceSwapperVotes or CustomRPC.MeetingKill or CustomRPC.Guess or CustomRPC.MafiaRevenge or CustomRPC.BAU or CustomRPC.FFAKill;
    }

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        var rpcType = (RpcCalls)callId;
        MessageReader subReader = MessageReader.Get(reader);
        if (EAC.ReceiveRpc(__instance, callId, reader)) return false;

        Logger.Info($"From ID: {__instance?.Data?.PlayerId} ({(__instance?.Data?.PlayerId == 0 ? "Host" : __instance?.Data?.PlayerName)}) : {callId} ({RPC.GetRpcName(callId)})", "ReceiveRPC");

        if (callId == 11 && __instance != null)
        {
            if (!ReportDeadBodyRPCs.ContainsKey(__instance.PlayerId)) ReportDeadBodyRPCs.TryAdd(__instance.PlayerId, 0);

            ReportDeadBodyRPCs[__instance.PlayerId]++;
            Logger.Info($"ReportDeadBody RPC count: {ReportDeadBodyRPCs[__instance.PlayerId]}, from {__instance.Data?.PlayerName}", "EAC");
        }

        switch (rpcType)
        {
            case RpcCalls.SetName:
                subReader.ReadUInt32();
                string name = subReader.ReadString();
                if (subReader.BytesRemaining > 0 && subReader.ReadBoolean()) return false;

                Logger.Info($"RPC Set Name For Player: {__instance.GetNameWithRole().RemoveHtmlTags()} => {name}", "SetName");
                break;
            case RpcCalls.SetRole:
                var role = (RoleTypes)subReader.ReadUInt16();
                bool canOverriddenRole = subReader.ReadBoolean();
                Logger.Info($"RPC Set Role For Player: {__instance.GetRealName()} => {role} CanOverrideRole: {canOverriddenRole}", "SetRole");
                break;
            case RpcCalls.SendChat:
                string text = subReader.ReadString();
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}:{text}", "ReceiveChat");
                ChatCommands.OnReceiveChat(__instance, text, out bool canceled);
                if (canceled) return false;

                break;
            case RpcCalls.StartMeeting:
                PlayerControl p = Utils.GetPlayerById(subReader.ReadByte());
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {p?.GetNameWithRole() ?? "null"}", "StartMeeting");
                break;
            case RpcCalls.Pet:
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} petted their pet", "RpcHandlerPatch");
                break;
            case RpcCalls.SetScanner when Main.HasJustStarted:
                Logger.Fatal($"{__instance.GetNameWithRole().RemoveHtmlTags()} triggered this bs ---- revive was attempted", "RpcHandlerPatch");
                __instance?.Revive();
                return false;
        }

        if (__instance != null && !__instance.IsHost() && Enum.IsDefined(typeof(CustomRPC), (int)callId) && !TrustedRpc(callId))
        {
            Logger.Warn($"{__instance.Data?.PlayerName}:{callId}({RPC.GetRpcName(callId)}) canceled because it was sent by someone other than the host.", "CustomRPC");
            if (!AmongUsClient.Instance.AmHost) return false;

            if (!EAC.ReceiveInvalidRpc(__instance, callId)) return false;

            AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), false);
            Logger.Warn($"The RPC received from {__instance.Data?.PlayerName} is not trusted, so they were kicked.", "Kick");
            Logger.SendInGame(string.Format(GetString("Warning.InvalidRpc"), __instance.Data?.PlayerName));
            return false;
        }

        if (__instance != null && (ReportDeadBodyRPCs.TryGetValue(__instance.PlayerId, out int times) && times > 4))
        {
            AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), true);
            Logger.Warn($"{__instance?.Data?.PlayerName} has sent 5 or more ReportDeadBody RPCs in the last 1 second, they were banned for hacking.", "EAC");
            Logger.SendInGame(string.Format(GetString("Warning.ReportDeadBodyHack"), __instance?.Data?.PlayerName));
            return false;
        }

        return true;
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        var rpcType = (CustomRPC)callId;
        if (AmongUsClient.Instance.AmHost && !TrustedRpc(callId)) return;

        switch (rpcType)
        {
            case CustomRPC.AntiBlackout:
            {
                if (Options.EndWhenPlayerBug.GetBool())
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): {reader.ReadString()} - Error, terminate the game according to settings", "Anti-blackout");
                    ChatUpdatePatch.DoBlockChat = true;
                    Main.OverrideWelcomeMsg = string.Format(GetString("RpcAntiBlackOutNotifyInLobby"), __instance?.Data?.PlayerName, GetString("EndWhenPlayerBug"));
                    LateTask.New(() => { Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutEndGame"), __instance?.Data?.PlayerName) /*, true*/); }, 3f, "Anti-Black Msg SendInGame");

                    LateTask.New(() =>
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                        GameManager.Instance.LogicFlow.CheckEndCriteria();
                        RPC.ForceEndGame(CustomWinner.Error);
                    }, 5.5f, "Anti-Black End Game");

                    LateTask.New(() => ChatUpdatePatch.DoBlockChat = false, 6f, log: false);
                }
                else if (GameStates.IsOnlineGame)
                {
                    Logger.Fatal($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): Change Role Setting Postfix - Error, continue the game according to settings", "Anti-blackout");
                    LateTask.New(() => { Logger.SendInGame(string.Format(GetString("RpcAntiBlackOutIgnored"), __instance?.Data?.PlayerName) /*, true*/); }, 3f, "Anti-Black Msg SendInGame");
                }

                break;
            }
            case CustomRPC.VersionCheck:
            {
                try
                {
                    Version version = Version.Parse(reader.ReadString());
                    string tag = reader.ReadString();
                    string forkId = reader.ReadString();

                    try
                    {
                        if (!Main.PlayerVersion.ContainsKey(__instance.PlayerId)) RPC.RpcVersionCheck();
                    }
                    catch { }

                    Main.PlayerVersion[__instance.PlayerId] = new(version, tag, forkId);

                    // Kick Unmached Player Start
                    if (AmongUsClient.Instance.AmHost && tag != $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" && forkId != Main.ForkId)
                    {
                        LateTask.New(() =>
                        {
                            if (__instance.Data?.Disconnected is not null and not true)
                            {
                                string msg = string.Format(GetString("KickBecauseDiffrentVersionOrMod"), __instance.Data?.PlayerName);
                                Logger.Warn(msg, "Version Kick");
                                Logger.SendInGame(msg);
                                AmongUsClient.Instance.KickPlayer(__instance.GetClientId(), false);
                            }
                        }, 5f, "Kick");
                    }
                    // Kick Unmached Player End
                }
                catch (Exception e)
                {
                    Logger.Warn($"{__instance?.Data?.PlayerName}({__instance?.PlayerId}): error during version check", "RpcVersionCheck");
                    Utils.ThrowException(e);
                    if (GameStates.InGame || AmongUsClient.Instance.IsGameStarted) break;

                    LateTask.New(() =>
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, __instance.GetClientId());
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }, 1f, "Retry Version Check Task");
                }

                break;
            }
            case CustomRPC.RequestRetryVersionCheck:
            {
                RPC.RpcVersionCheck();
                break;
            }
            case CustomRPC.SyncCustomSettings:
            {
                if (AmongUsClient.Instance.AmHost) break;

                List<OptionItem> listOptions = [];
                int startAmount = reader.ReadInt32();
                int lastAmount = reader.ReadInt32();

                int countAllOptions = OptionItem.AllOptions.Count;

                // Add Options
                for (int option = startAmount; option < countAllOptions && option <= lastAmount; option++)
                    listOptions.Add(OptionItem.AllOptions[option]);

                int countOptions = listOptions.Count;
                Logger.Msg($"StartAmount: {startAmount} - LastAmount: {lastAmount} ({startAmount}/{lastAmount}) :--: ListOptionsCount: {countOptions} - AllOptions: {countAllOptions} ({countOptions}/{countAllOptions})", "SyncCustomSettings");

                // Sync Settings
                foreach (OptionItem option in listOptions)
                {
                    try { option.SetValue(reader.ReadPackedInt32()); }
                    catch { }
                }

                OptionShower.GetText();
                break;
            }
            case CustomRPC.SetDeathReason:
            {
                RPC.GetDeathReason(reader);
                break;
            }
            case CustomRPC.EndGame:
            {
                RPC.EndGame(reader);
                break;
            }
            case CustomRPC.PlaySound:
            {
                byte playerID = reader.ReadByte();
                var sound = (Sounds)reader.ReadByte();
                RPC.PlaySound(playerID, sound);
                break;
            }
            case CustomRPC.ShowPopUp:
            {
                string msg = reader.ReadString();
                HudManager.Instance.ShowPopUp(msg);
                break;
            }
            case CustomRPC.SyncAllClientRealNames:
            {
                Main.AllClientRealNames.Clear();
                int num2 = reader.ReadPackedInt32();
                for (var i = 0; i < num2; i++) Main.AllClientRealNames.TryAdd(reader.ReadInt32(), reader.ReadString());

                break;
            }
            case CustomRPC.SetCustomRole:
            {
                byte CustomRoleTargetId = reader.ReadByte();
                var role = (CustomRoles)reader.ReadPackedInt32();
                bool replaceAllAddons = reader.ReadBoolean();
                RPC.SetCustomRole(CustomRoleTargetId, role, replaceAllAddons);
                break;
            }
            case CustomRPC.SyncAbilityUseLimit:
            {
                PlayerControl pc = Utils.GetPlayerById(reader.ReadByte());
                pc.SetAbilityUseLimit(reader.ReadSingle(), false);
                break;
            }
            case CustomRPC.RemoveAbilityUseLimit:
            {
                Main.AbilityUseLimit.Remove(reader.ReadByte());
                break;
            }
            case CustomRPC.RemoveSubRole:
            {
                byte id = reader.ReadByte();

                if (reader.ReadPackedInt32() == 2)
                    Main.PlayerStates[id].SubRoles.Clear();
                else
                    Main.PlayerStates[id].RemoveSubRole((CustomRoles)reader.ReadPackedInt32());

                break;
            }
            case CustomRPC.Arrow:
            {
                if (reader.ReadBoolean())
                    TargetArrow.ReceiveRPC(reader);
                else
                    LocateArrow.ReceiveRPC(reader);

                break;
            }
            case CustomRPC.SyncAbilityCD:
            {
                switch (reader.ReadPackedInt32())
                {
                    case 1:
                    {
                        byte id = reader.ReadByte();
                        int cd = reader.ReadPackedInt32();
                        long ts = Utils.TimeStamp;
                        Main.AbilityCD[id] = (ts, cd);
                        break;
                    }
                    case 2:
                    {
                        Main.AbilityCD.Clear();
                        break;
                    }
                    case 3:
                    {
                        byte id = reader.ReadByte();
                        Main.AbilityCD.Remove(id);
                        break;
                    }
                }

                break;
            }
            case CustomRPC.SyncRoleData:
            {
                byte id = reader.ReadByte();
                RoleBase r = Main.PlayerStates[id].Role;
                r.GetType().GetMethod("ReceiveRPC")?.Invoke(r, [reader]);
                break;
            }
            case CustomRPC.FixModdedClientCNO:
            {
                var CNO = reader.ReadNetObject<PlayerControl>();
                bool active = reader.ReadBoolean();
                CNO.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(active);
                break;
            }
            case CustomRPC.SyncGeneralOptions:
            {
                byte id = reader.ReadByte();
                var role = (CustomRoles)reader.ReadPackedInt32();
                bool dead = reader.ReadBoolean();
                var dr = (PlayerState.DeathReason)reader.ReadPackedInt32();

                if (Main.PlayerStates.TryGetValue(id, out PlayerState state))
                {
                    state.MainRole = role;
                    state.IsDead = dead;
                    state.deathReason = dr;
                }

                float kcd = reader.ReadSingle();
                float speed = reader.ReadSingle();
                Main.AllPlayerKillCooldown[id] = kcd;
                Main.AllPlayerSpeed[id] = speed;
                break;
            }
            case CustomRPC.RequestSendMessage:
            {
                if (!AmongUsClient.Instance.AmHost) break;

                string text = reader.ReadString();
                byte sendTo = reader.ReadByte();
                string title = reader.ReadString();
                bool noSplit = reader.ReadBoolean();
                Utils.SendMessage(text, sendTo, title, noSplit);
                break;
            }
            case CustomRPC.NotificationPopper:
            {
                byte typeId = reader.ReadByte();
                int item = reader.ReadPackedInt32();
                int customRole = reader.ReadPackedInt32();
                bool playSound = reader.ReadBoolean();
                OptionItem key = OptionItem.AllOptions[item];

                switch (typeId)
                {
                    case 0:
                        NotificationPopperPatch.AddSettingsChangeMessage(item, key, playSound);
                        break;
                    case 1:
                        NotificationPopperPatch.AddRoleSettingsChangeMessage(item, key, (CustomRoles)customRole, playSound);
                        break;
                }

                break;
            }
            case CustomRPC.RequestCommandProcessing:
            {
                if (!AmongUsClient.Instance.AmHost) break;

                string methodName = reader.ReadString();
                PlayerControl player = reader.ReadByte().GetPlayer();
                string text = reader.ReadString();

                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                typeof(ChatCommands).GetMethod(methodName, flags)?.Invoke(null, [null, player, text, text.Split(' ')]);
                Logger.Info($"Invoke Command: {methodName} ({player?.Data?.PlayerName}, {text})", "RequestCommandProcessing");
                break;
            }
            case CustomRPC.SyncPostman:
            {
                byte id = reader.ReadByte();
                byte target = reader.ReadByte();
                bool isFinished = reader.ReadBoolean();
                if (Main.PlayerStates[id].Role is not Postman pm) break;

                pm.Target = target;
                pm.IsFinished = isFinished;
                break;
            }
            case CustomRPC.SyncChangeling:
            {
                if (Main.PlayerStates[reader.ReadByte()].Role is not Changeling changeling) break;

                changeling.CurrentRole = (CustomRoles)reader.ReadPackedInt32();
                break;
            }
            case CustomRPC.SyncTiger:
            {
                if (Main.PlayerStates[reader.ReadByte()].Role is not Tiger tiger) break;

                tiger.EnrageTimer = reader.ReadSingle();
                break;
            }
            case CustomRPC.SyncSentry:
            {
                byte id = reader.ReadByte();
                if (Main.PlayerStates[id].Role is not Crewmate.Sentry sentry) break;

                sentry.MonitoredRoom = Utils.GetPlayerById(id).GetPlainShipRoom();
                break;
            }
            case CustomRPC.SyncOverheat:
            {
                ((Overheat)Main.PlayerStates[reader.ReadByte()].Role).Temperature = reader.ReadPackedInt32();
                break;
            }
            case CustomRPC.SyncIntrovert:
            {
                Introvert.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncAllergic:
            {
                Allergic.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetBountyTarget:
            {
                byte bountyId = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[bountyId].Role as BountyHunter)?.ReceiveRPC(bountyId, targetId);
                break;
            }
            case CustomRPC.SyncBargainer:
            {
                Bargainer.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetKillOrSpell:
            {
                Witch.ReceiveRPC(reader, false);
                break;
            }
            case CustomRPC.SetCpTasksDone:
            {
                Crewpostor.RecieveRPC(reader);
                break;
            }
            case CustomRPC.SetLibrarianMode:
            {
                Librarian.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncSoulHunter:
            {
                SoulHunter.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetDousedPlayer:
            {
                byte ArsonistId = reader.ReadByte();
                byte dousedId = reader.ReadByte();
                bool doused = reader.ReadBoolean();
                Arsonist.IsDoused[(ArsonistId, dousedId)] = doused;
                break;
            }
            case CustomRPC.SetPlaguedPlayer:
            {
                PlagueBearer.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncHeadHunter:
            {
                HeadHunter.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncDamoclesTimer:
            {
                Damocles.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetDrawPlayer:
            {
                byte RevolutionistId = reader.ReadByte();
                byte DrawId = reader.ReadByte();
                bool drawed = reader.ReadBoolean();
                Revolutionist.IsDraw[(RevolutionistId, DrawId)] = drawed;
                break;
            }
            case CustomRPC.SetRevealedPlayer:
            {
                byte FarseerId = reader.ReadByte();
                byte RevealId = reader.ReadByte();
                bool revealed = reader.ReadBoolean();
                Farseer.IsRevealed[(FarseerId, RevealId)] = revealed;
                break;
            }
            case CustomRPC.SetNameColorData:
            {
                NameColorManager.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.RpcPassBomb:
            {
                Agitater.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.DoSpell:
            {
                Witch.ReceiveRPC(reader, true);
                break;
            }
            case CustomRPC.SetBanditStealLimit:
            {
                Bandit.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SniperSync:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Sniper)?.ReceiveRPC(reader);
            }

                break;
            case CustomRPC.SyncSpy:
            {
                Spy.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetAlchemistPotion:
            {
                Alchemist.ReceiveRPCData(reader);
                break;
            }
            case CustomRPC.SetRicochetTarget:
            {
                Ricochet.ReceiveRPCSyncTarget(reader);
                break;
            }
            case CustomRPC.SyncRabbit:
            {
                Rabbit.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncYinYanger:
            {
                YinYanger.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetTetherTarget:
            {
                Tether.ReceiveRPCSyncTarget(reader);
                break;
            }
            case CustomRPC.SetHitmanTarget:
            {
                byte hitmanId = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[hitmanId].Role as Hitman)?.ReceiveRPC(targetId);
            }

                break;
            case CustomRPC.SetWeaponMasterMode:
            {
                WeaponMaster.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncGlitchTimers:
            {
                Glitch.ReceiveRPCSyncTimers(reader);
                break;
            }
            case CustomRPC.DruidAddTrigger:
            {
                Druid.ReceiveRPCAddTrigger(reader);
                break;
            }
            case CustomRPC.SetSabotageMasterLimit:
            {
                SabotageMaster.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetNiceHackerLimit:
            {
                NiceHacker.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetLoversPlayers:
            {
                Main.LoversPlayers.Clear();
                int count = reader.ReadInt32();
                for (var i = 0; i < count; i++) Main.LoversPlayers.Add(Utils.GetPlayerById(reader.ReadByte()));
            }

                break;
            case CustomRPC.SetExecutionerTarget:
            {
                Executioner.ReceiveRPC(reader, true);
                break;
            }
            case CustomRPC.RemoveExecutionerTarget:
            {
                Executioner.ReceiveRPC(reader, false);
                break;
            }
            case CustomRPC.SetLawyerTarget:
            {
                Lawyer.ReceiveRPC(reader, true);
                break;
            }
            case CustomRPC.RemoveLawyerTarget:
            {
                Lawyer.ReceiveRPC(reader, false);
                break;
            }
            case CustomRPC.SendFireWorksState:
            {
                byte id = reader.ReadByte();
                int count = reader.ReadInt32();
                int state = reader.ReadInt32();
                var newState = (FireWorks.FireWorksState)state;
                (Main.PlayerStates[id].Role as FireWorks)?.ReceiveRPC(count, newState);
            }

                break;
            case CustomRPC.SyncMycologist:
            {
                Mycologist.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncBubble:
            {
                Bubble.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetCurrentDousingTarget:
            {
                byte arsonistId = reader.ReadByte();
                byte dousingTargetId = reader.ReadByte();
                if (PlayerControl.LocalPlayer.PlayerId == arsonistId) Arsonist.CurrentDousingTarget = dousingTargetId;

                break;
            }
            case CustomRPC.SetCurrentDrawTarget:
            {
                byte arsonistId1 = reader.ReadByte();
                byte doTargetId = reader.ReadByte();
                if (PlayerControl.LocalPlayer.PlayerId == arsonistId1) Revolutionist.CurrentDrawTarget = doTargetId;

                break;
            }
            case CustomRPC.SetEvilTrackerTarget:
            {
                EvilTracker.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncPlagueDoctor:
            {
                PlagueDoctor.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.PenguinSync:
            {
                byte id = reader.ReadByte();
                int operate = reader.ReadInt32();

                if (operate == 1)
                {
                    byte victim = reader.ReadByte();
                    (Main.PlayerStates[id].Role as Penguin)?.ReceiveRPC(victim);
                }
                else
                {
                    float timer = reader.ReadSingle();
                    (Main.PlayerStates[id].Role as Penguin)?.ReceiveRPC(timer);
                }

                break;
            }
            case CustomRPC.SetRealKiller:
            {
                byte targetId = reader.ReadByte();
                byte killerId = reader.ReadByte();
                RPC.SetRealKiller(targetId, killerId);
                break;
            }
            case CustomRPC.ShowChat:
            {
                uint clientId = reader.ReadPackedUInt32();
                bool show = reader.ReadBoolean();
                if (AmongUsClient.Instance.ClientId == clientId) HudManager.Instance.Chat.SetVisible(show);

                break;
            }
            case CustomRPC.SyncLobbyTimer:
            {
                GameStartManagerPatch.TimerStartTS = long.Parse(reader.ReadString());
                break;
            }
            case CustomRPC.SetGamerHealth:
            {
                Gamer.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetPelicanEtenNum:
            {
                Pelican.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetDoomsayerProgress:
            {
                Doomsayer.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SwordsManKill:
            {
                SwordsMan.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.PlayCustomSound:
            {
                CustomSoundsManager.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetGhostPlayer:
            {
                BallLightning.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetDarkHiderKillCount:
            {
                DarkHide.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetGreedierOe:
            {
                byte id = reader.ReadByte();
                bool isOdd = reader.ReadBoolean();
                (Main.PlayerStates[id].Role as Greedier)?.ReceiveRPC(isOdd);
            }

                break;
            case CustomRPC.SetCollectorVotes:
            {
                Collector.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetQuickShooterShotLimit:
            {
                QuickShooter.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.GuessKill:
            {
                GuessManager.RpcClientGuess(Utils.GetPlayerById(reader.ReadByte()));
                break;
            }
            case CustomRPC.SetMarkedPlayer:
            {
                Assassin.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncChronomancer:
            {
                byte id = reader.ReadByte();
                bool isRampaging = reader.ReadBoolean();
                int chargePercent = reader.ReadInt32();
                long lastUpdate = long.Parse(reader.ReadString());
                (Main.PlayerStates[id].Role as Chronomancer)?.ReceiveRPC(isRampaging, chargePercent, lastUpdate);
            }

                break;
            case CustomRPC.SetMedicalerProtectList:
            {
                Medic.ReceiveRPCForProtectList(reader);
                break;
            }
            case CustomRPC.SyncPsychicRedList:
            {
                Psychic.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetKillTimer:
            {
                float time = reader.ReadSingle();
                PlayerControl.LocalPlayer.SetKillTimer(time);
                break;
            }
            case CustomRPC.SyncAllPlayerNames:
            {
                Main.AllPlayerNames = [];
                int num = reader.ReadInt32();
                for (var i = 0; i < num; i++) Main.AllPlayerNames.TryAdd(reader.ReadByte(), reader.ReadString());

                break;
            }
            case CustomRPC.SyncMafiosoData:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Mafioso)?.ReceiveRPC(reader);
            }

                break;
            case CustomRPC.SyncMafiosoPistolCD:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Mafioso)?.ReceiveRPCSyncPistolCD(reader);
            }

                break;
            case CustomRPC.SyncNameNotify:
            {
                NameNotifyManager.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.Judge:
            {
                Judge.ReceiveRPC(reader, __instance);
                break;
            }
            case CustomRPC.MeetingKill:
            {
                Councillor.ReceiveRPC(reader, __instance);
                break;
            }
            case CustomRPC.Guess:
            {
                GuessManager.ReceiveRPC(reader, __instance);
                break;
            }
            case CustomRPC.MafiaRevenge:
            {
                Mafia.ReceiveRPC(reader, __instance);
                break;
            }
            case CustomRPC.SetSwooperTimer:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Swooper)?.ReceiveRPC(reader);
            }

                break;
            case CustomRPC.SetAlchemistTimer:
            {
                Alchemist.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetBkTimer:
            {
                Wildling.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncTotocalcioTargetAndTimes:
            {
                Totocalcio.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncRomanticTarget:
            {
                Romantic.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncVengefulRomanticTarget:
            {
                VengefulRomantic.ReceiveRPC(reader);
                break;
            }
            //case CustomRPC.SetCursedSoulCurseLimit:
            //    CursedSoul.ReceiveRPC(reader);
            //    break;
            case CustomRPC.SetEvilDiviner:
            {
                byte id = reader.ReadByte();
                byte targetId = reader.ReadByte();
                (Main.PlayerStates[id].Role as EvilDiviner)?.ReceiveRPC(targetId);
            }

                break;
            case CustomRPC.SyncHookshot:
            {
                Hookshot.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.AddTornado:
            {
                Tornado.ReceiveRPCAddTornado(reader);
                break;
            }
            case CustomRPC.SetDoppelgangerStealLimit:
            {
                Doppelganger.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncBenefactorMarkedTask:
            {
                Benefactor.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SyncStressedTimer:
            {
                Stressed.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.KillFlash:
            {
                Utils.FlashColor(new(1f, 0f, 0f, 0.3f));
                if (Constants.ShouldPlaySfx()) RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.KillSound);

                break;
            }
            case CustomRPC.SetCleanserCleanLimit:
            {
                Cleanser.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetJailorExeLimit:
            {
                Jailor.ReceiveRPC(reader, false);
                break;
            }
            case CustomRPC.SetJailorTarget:
            {
                Jailor.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.SetWwTimer:
            {
                byte id = reader.ReadByte();
                (Main.PlayerStates[id].Role as Werewolf)?.ReceiveRPC(reader);
            }

                break;
            case CustomRPC.SetNiceSwapperVotes:
            {
                NiceSwapper.ReceiveRPC(reader, __instance);
                break;
            }
            case CustomRPC.SetTrackerTarget:
            {
                Scout.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.RoomRushDataSync:
            {
                RoomRush.ReceiveRPC(reader);
                break;
            }
            case CustomRPC.FFAKill:
            {
                if (!CustomGameMode.FFA.IsActiveOrIntegrated())
                {
                    EAC.WarnHost();
                    EAC.Report(__instance, "FFA RPC when game mode is not FFA");
                    break;
                }

                var killer = reader.ReadNetObject<PlayerControl>();
                var target = reader.ReadNetObject<PlayerControl>();

                if (!killer.IsAlive() || !target.IsAlive() || AntiBlackout.SkipTasks || target.inMovingPlat || target.onLadder || target.inVent || MeetingHud.Instance) break;

                FFAManager.OnPlayerAttack(killer, target);
                break;
            }
        }
    }
}

internal static class RPC
{
    // Credit: https://github.com/music-discussion/TownOfHost-TheOtherRoles/blob/main/Modules/RPC.cs
    public static void SyncCustomSettingsRPC(int targetId = -1)
    {
        if (targetId != -1)
        {
            ClientData client = Utils.GetClientById(targetId);
            if (client == null || client.Character == null || !Main.PlayerVersion.ContainsKey(client.Character.PlayerId)) return;
        }

        if (!AmongUsClient.Instance.AmHost || PlayerControl.AllPlayerControls.Count <= 1 || (AmongUsClient.Instance.AmHost == false && PlayerControl.LocalPlayer == null)) return;

        int amount = OptionItem.AllOptions.Count;
        int divideBy = amount / 10;
        for (var i = 0; i <= 10; i++) SyncOptionsBetween(i * divideBy, (i + 1) * divideBy, targetId);
    }

    private static void SyncOptionsBetween(int startAmount, int lastAmount, int targetId = -1)
    {
        if (targetId != -1)
        {
            ClientData client = Utils.GetClientById(targetId);
            if (client == null || client.Character == null || !Main.PlayerVersion.ContainsKey(client.Character.PlayerId)) return;
        }

        if (!AmongUsClient.Instance.AmHost || PlayerControl.AllPlayerControls.Count <= 1 || (AmongUsClient.Instance.AmHost == false && PlayerControl.LocalPlayer == null)) return;

        int amountAllOptions = OptionItem.AllOptions.Count;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncCustomSettings, SendOption.Reliable, targetId);
        writer.Write(startAmount);
        writer.Write(lastAmount);

        List<OptionItem> listOptions = [];

        // Add Options
        for (int option = startAmount; option < amountAllOptions && option <= lastAmount; option++) listOptions.Add(OptionItem.AllOptions[option]);

        int countListOptions = listOptions.Count;
        Logger.Msg($"StartAmount: {startAmount} - LastAmount: {lastAmount} ({startAmount}/{lastAmount}) :--: ListOptionsCount: {countListOptions} - AllOptions: {amountAllOptions} ({countListOptions}/{amountAllOptions})", "SyncCustomSettings");

        // Sync Settings
        foreach (OptionItem option in listOptions) writer.WritePacked(option.GetValue());

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void PlaySoundRPC(byte PlayerID, Sounds sound)
    {
        if (AmongUsClient.Instance.AmHost) PlaySound(PlayerID, sound);

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlaySound, SendOption.Reliable);
        writer.Write(PlayerID);
        writer.Write((byte)sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SyncAllPlayerNames()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAllPlayerNames, SendOption.Reliable);
        writer.Write(Main.AllPlayerNames.Count);

        foreach (KeyValuePair<byte, string> name in Main.AllPlayerNames)
        {
            writer.Write(name.Key);
            writer.Write(name.Value);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendGameData(int clientId = -1)
    {
        foreach (NetworkedPlayerInfo innerNetObject in GameData.Instance.AllPlayers) innerNetObject.SetDirtyBit(uint.MaxValue);

        AmongUsClient.Instance.SendAllStreamedObjects();
    }

    public static void ShowPopUp(this PlayerControl pc, string msg)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShowPopUp, SendOption.Reliable, pc.GetClientId());
        writer.Write(msg);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SyncAllClientRealNames()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAllClientRealNames, SendOption.Reliable);
        writer.WritePacked(Main.AllClientRealNames.Count);

        foreach (KeyValuePair<int, string> name in Main.AllClientRealNames)
        {
            writer.Write(name.Key);
            writer.Write(name.Value);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ExileAsync(PlayerControl player)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        player.Exiled();
    }

    public static void RpcVersionCheck()
    {
        Main.Instance.StartCoroutine(VersionCheck());
        return;

        static IEnumerator VersionCheck()
        {
            while (PlayerControl.LocalPlayer == null) yield return null;

            if (AmongUsClient.Instance.AmHost)
                Utils.SendRPC(CustomRPC.VersionCheck, Main.PluginVersion, $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})", Main.ForkId);
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.VersionCheck);
                writer.Write(Main.PluginVersion);
                writer.Write($"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})");
                writer.Write(Main.ForkId);
                writer.EndMessage();
            }

            Main.PlayerVersion[PlayerControl.LocalPlayer.PlayerId] = new(Main.PluginVersion, $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})", Main.ForkId);
        }
    }

    public static void SendDeathReason(byte playerId, PlayerState.DeathReason deathReason)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDeathReason, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write((int)deathReason);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void GetDeathReason(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        var deathReason = (PlayerState.DeathReason)reader.ReadInt32();
        Main.PlayerStates[playerId].deathReason = deathReason;
        Main.PlayerStates[playerId].IsDead = true;
    }

    public static void ForceEndGame(CustomWinner win)
    {
        if (ShipStatus.Instance == null) return;

        try { CustomWinnerHolder.ResetAndSetWinner(win); }
        catch { }

        if (AmongUsClient.Instance.AmHost)
        {
            ShipStatus.Instance.enabled = false;

            try { GameManager.Instance.LogicFlow.CheckEndCriteria(); }
            catch { }

            try { GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false); }
            catch { }
        }
    }

    public static void EndGame(MessageReader reader)
    {
        try { CustomWinnerHolder.ReadFrom(reader); }
        catch (Exception ex) { Logger.Error($"正常にEndGameを行えませんでした。\n{ex}", "EndGame", false); }
    }

    public static void PlaySound(byte playerID, Sounds sound)
    {
        if (PlayerControl.LocalPlayer.PlayerId == playerID)
        {
            switch (sound)
            {
                case Sounds.KillSound:
                    SoundManager.Instance.PlaySound(PlayerControl.LocalPlayer.KillSfx, false);
                    break;
                case Sounds.TaskComplete:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskCompleteSound, false);
                    break;
                case Sounds.TaskUpdateSound:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HudManager>.Instance.TaskUpdateSound, false);
                    break;
                case Sounds.ImpTransform:
                    SoundManager.Instance.PlaySound(DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx, false, 0.8f);
                    break;
            }
        }
    }

    public static void SetCustomRole(byte targetId, CustomRoles role, bool replaceAllAddons = false)
    {
        if (role < CustomRoles.NotAssigned)
            Main.PlayerStates[targetId].SetMainRole(role);
        else
            Main.PlayerStates[targetId].SetSubRole(role, replaceAllAddons);

        HudManager.Instance.SetHudActive(true);
        if (PlayerControl.LocalPlayer.PlayerId == targetId) RemoveDisableDevicesPatch.UpdateDisableDevices();
    }

    public static void SyncLoversPlayers()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLoversPlayers, SendOption.Reliable);
        writer.Write(Main.LoversPlayers.Count);
        foreach (PlayerControl lp in Main.LoversPlayers) writer.Write(lp.PlayerId);

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendRpcLogger(uint targetNetId, byte callId, int targetClientId = -1)
    {
        if (!DebugModeManager.AmDebugger) return;

        string rpcName = GetRpcName(callId);
        var from = targetNetId.ToString();
        var target = targetClientId.ToString();

        try
        {
            target = targetClientId < 0 ? "All" : AmongUsClient.Instance.GetClient(targetClientId).PlayerName;
            from = Main.AllPlayerControls.FirstOrDefault(c => c.NetId == targetNetId)?.Data?.PlayerName;
        }
        catch { }

        Logger.Info($"FromNetID: {targetNetId} ({from}) / TargetClientID: {targetClientId} ({target}) / CallID: {callId} ({rpcName})", "SendRPC");
    }

    public static string GetRpcName(byte callId)
    {
        string rpcName;

        if ((rpcName = Enum.GetName(typeof(RpcCalls), callId)) != null) { }
        else if ((rpcName = Enum.GetName(typeof(CustomRPC), callId)) != null) { }
        else
            rpcName = callId.ToString();

        return rpcName;
    }

    public static void SetCurrentDousingTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
            Arsonist.CurrentDousingTarget = targetId;
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDousingTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SetCurrentDrawTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
            Revolutionist.CurrentDrawTarget = targetId;
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentDrawTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SetCurrentRevealTarget(byte arsonistId, byte targetId)
    {
        if (PlayerControl.LocalPlayer.PlayerId == arsonistId)
            Revolutionist.CurrentDrawTarget = targetId;
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCurrentRevealTarget, SendOption.Reliable);
            writer.Write(arsonistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void ResetCurrentDousingTarget(byte arsonistId)
    {
        SetCurrentDousingTarget(arsonistId, 255);
    }

    public static void ResetCurrentDrawTarget(byte arsonistId)
    {
        SetCurrentDrawTarget(arsonistId, 255);
    }

    public static void ResetCurrentRevealTarget(byte arsonistId)
    {
        SetCurrentRevealTarget(arsonistId, 255);
    }

    public static void SetRealKiller(byte targetId, byte killerId)
    {
        PlayerState state = Main.PlayerStates[targetId];
        state.RealKiller.TimeStamp = DateTime.Now;
        state.RealKiller.ID = killerId;

        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRealKiller, SendOption.Reliable);
        writer.Write(targetId);
        writer.Write(killerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class PlayerPhysicsRPCHandlerPatch
{
    public static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
    {
        bool host = __instance.IsHost();
        if (!host && EAC.PlayerPhysicsRpcCheck(__instance, callId, reader)) return false;

        PlayerControl player = __instance.myPlayer;

        if (!player)
        {
            Logger.Warn("Received Physics RPC without a player", "PlayerPhysics_ReceiveRPC");
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpc))]
internal static class StartRpcPatch
{
    public static void Prefix( /*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId)
    {
        RPC.SendRpcLogger(targetNetId, callId);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
internal static class StartRpcImmediatelyPatch
{
    public static void Prefix( /*InnerNet.InnerNetClient __instance,*/ [HarmonyArgument(0)] uint targetNetId, [HarmonyArgument(1)] byte callId, [HarmonyArgument(3)] int targetClientId = -1)
    {
        RPC.SendRpcLogger(targetNetId, callId, targetClientId);
    }
}