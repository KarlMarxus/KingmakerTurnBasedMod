﻿using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Commands;
using TurnBased.Controllers;
using TurnBased.Utility;
using static TurnBased.Main;
using static TurnBased.Utility.SettingsWrapper;
using static TurnBased.Utility.StatusWrapper;

namespace TurnBased.HarmonyPatches
{
    static class Misc
    {
        // delay one round for summoned units after casting a summon spell
        [HarmonyPatch(typeof(RuleSummonUnit), nameof(RuleSummonUnit.OnTrigger), typeof(RulebookEventContext))]
        static class RuleSummonUnit_OnTrigger_Patch
        {
            [HarmonyPostfix]
            static void Postfix(RuleSummonUnit __instance)
            {
                if (IsEnabled())
                {
                    // remove the freezing time when the unit is summoned from a trap
                    if (__instance.Initiator.Faction?.AssetGuid == "d75c5993785785d468211d9a1a3c87a6")
                    {
                        __instance.SummonedUnit?.Descriptor.RemoveFact
                            (BlueprintRoot.Instance.SystemMechanics.SummonedUnitAppearBuff);
                        return;
                    }

                    // exclude RangedLegerdemainUnit
                    if (__instance.SummonedUnit?.Blueprint.AssetGuid != "661093277286dd5459cd825e0205f908")
                    {
                        __instance.SummonedUnit?.Descriptor.AddBuff
                            (BlueprintRoot.Instance.SystemMechanics.SummonedUnitAppearBuff, __instance.Context, 6.Seconds());
                    }
                }
            }
        }

        // speed up casting
        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.Init), typeof(UnitEntityData))]
        static class UnitUseAbility_Init_Patch
        {
            [HarmonyPostfix]
            static void Postfix(UnitUseAbility __instance)
            {
                if (IsInCombat() && __instance.Executor.IsInCombat && CastingTimeOfFullRoundSpell != 1f)
                {
                    float castTime = __instance.GetCastTime();
                    if (castTime >= 6f)
                    {
                        __instance.SetCastTime(castTime * CastingTimeOfFullRoundSpell);
                    }
                }
            }
        }

        // make flanking don't consider opponents' command anymore
        [HarmonyPatch(typeof(UnitCombatState), nameof(UnitCombatState.IsFlanked), MethodType.Getter)]
        static class UnitCombatState_get_IsFlanked_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitCombatState __instance, ref bool __result)
            {
                if (IsInCombat() && __instance.Unit.IsInCombat && FlankingCountAllOpponents)
                {
                    __result = __instance.EngagedBy.Count > 1 && !__instance.Unit.Descriptor.State.Features.CannotBeFlanked;
                    return false;
                }
                return true;
            }
        }

        // ** fix stealth check
        [HarmonyPatch(typeof(UnitStealthController), "TickUnit", typeof(UnitEntityData))]
        static class UnitStealthController_TickUnit_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitEntityData unit, ref float? __state)
            {
                if (IsInCombat() && !IsPassing())
                {
                    __state = Game.Instance.TimeController.GameDeltaTime;
                    Game.Instance.TimeController.SetGameDeltaTime(0f);

                    TurnController currentTurn = Mod.Core.Combat.CurrentTurn;
                    if (unit.IsCurrentUnit() &&
                        (currentTurn.WantEnterStealth != unit.Stealth.WantEnterStealth || currentTurn.NeedStealthCheck))
                    {
                        currentTurn.WantEnterStealth = unit.Stealth.WantEnterStealth;
                        currentTurn.NeedStealthCheck = false;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }

            [HarmonyPostfix]
            static void Postfix(ref float? __state)
            {
                if (__state.HasValue)
                {
                    Game.Instance.TimeController.SetGameDeltaTime(__state.Value);
                }
            }
        }
    }
}