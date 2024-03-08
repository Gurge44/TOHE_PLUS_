﻿using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Mayor : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.MayorUsedButtonCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown =
                !Main.MayorUsedButtonCount.TryGetValue(playerId, out var count) || count < MayorNumOfUseButton.GetInt()
                    ? opt.GetInt(Int32OptionNames.EmergencyCooldown)
                    : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Button(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            Button(pc);
        }

        private static void Button(PlayerControl pc)
        {
            if (!MayorHasPortableButton.GetBool()) return;

            if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < MayorNumOfUseButton.GetInt())
            {
                pc.ReportDeadBody(null);
            }
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(9500, TabGroup.CrewmateRoles, CustomRoles.Mayor);
            MayorAdditionalVote = IntegerOptionItem.Create(9510, "MayorAdditionalVote", new(0, 90, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor])
                .SetValueFormat(OptionFormat.Votes);
            MayorHasPortableButton = BooleanOptionItem.Create(9511, "MayorHasPortableButton", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorNumOfUseButton = IntegerOptionItem.Create(9512, "MayorNumOfUseButton", new(1, 90, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(MayorHasPortableButton)
                .SetValueFormat(OptionFormat.Times);
            MayorHideVote = BooleanOptionItem.Create(9513, "MayorHideVote", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorRevealWhenDoneTasks = BooleanOptionItem.Create(9514, "MayorRevealWhenDoneTasks", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorTasks = OverrideTasksData.Create(9515, TabGroup.CrewmateRoles, CustomRoles.Mayor);
        }
    }
}