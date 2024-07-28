using BattleTech;
using CustAmmoCategories;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VekMechs.Patches;

static class DeferredEffectExtension {
    private static Regex TriggerAfterActivation = new("TriggerAfterActivation");
    private static Regex EnableAOECrit = new("EnableAoECrit");
    private static Regex Building = new("Building");

    //Update display counts for stuck tagged units
    [HarmonyPatch(typeof(DeferredEffect), nameof(DeferredEffect.RoundsRemain))]
    static class DeferredEffect_RoundsRemain_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref DeferredEffect __instance, ref int __result) {
            if (TriggerAfterActivation.IsMatch(__instance.definition.id) && __instance.ancor != null) {
                __result = 1;
            }
        }
    }
    //Update display counts for stuck tagged units
    [HarmonyPatch(typeof(DeferredEffect), nameof(DeferredEffect.Init), new Type[] {
        typeof(Weapon),
        typeof(DeferredEffectDef),
        typeof(int),
        typeof(ICombatant),
    })]
    static class DeferredEffect_Init_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref DeferredEffect __instance, Weapon weapon, DeferredEffectDef def, int currentRound, ICombatant ancor) {
            int roundsText = 1;
            if (ancor is Building) {
                __instance.offset = __instance.ancor.position;
                __instance.ancor = null;
            }
            if (ancor == null || !TriggerAfterActivation.IsMatch(__instance.definition.id)) {
                return;
            }

            __instance.CountDownFloatie.Text.SetText(def.text + ":" + roundsText.ToString());
        }
    }
    //Trigger tagged effects after a mech activates
    [HarmonyPatch(typeof(AbstractActor), nameof(AbstractActor.OnActivationEnd))]
    static class AbstractActor_OnActivationEnd_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref AbstractActor __instance) {
            HashSet<DeferredEffect> hashSet = new HashSet<DeferredEffect>();
            Transform ancor = __instance.GameRep?.transform;

            foreach (DeferredEffect deferredEffect in DeferredEffectHelper.deferredEffects) {
                if (TriggerAfterActivation.IsMatch(deferredEffect.definition.id)
                    && ancor == deferredEffect.ancor
                ) {
                    hashSet.Add(deferredEffect);
                    deferredEffect.PlayEffect();
                    deferredEffect.gameObject.SetActive(true);
                }
            }
            foreach (DeferredEffect item in hashSet) {
                DeferredEffectHelper.deferredEffects.Remove(item);
                DeferredEffectHelper.playingEffects.Add(item);
            }
        }
    }
    //Detach when a mech dies so that it reverts back to normal behavior
    [HarmonyPatch(typeof(AbstractActor), nameof(AbstractActor.FlagForDeath))]
    static class AbstractActor_FlagForDeath_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref AbstractActor __instance) {
            Transform ancor = __instance.GameRep?.transform;

            foreach (DeferredEffect deferredEffect in DeferredEffectHelper.deferredEffects) {
                if (TriggerAfterActivation.IsMatch(deferredEffect.definition.id)
                    && ancor == deferredEffect.ancor
                ) {
                    deferredEffect.ancor = null;
                    deferredEffect.offset = ancor.position;
                    deferredEffect.CountDownFloatie.Text.SetText(deferredEffect.definition.text + ":" + deferredEffect.RoundsRemain(DeferredEffectHelper.CurrentRound).ToString());
                }
            }
        }
    }
    //Enable Crit support
    [HarmonyPatch(typeof(DeferredEffect), nameof(DeferredEffect.applyAOEDamage))]
    static class DeferredEffect_applyAOEDamage_CritPatch {
        static readonly MethodInfo DeferredEffectExtentsion_ApplyCritEffect = AccessTools.Method(
            typeof(DeferredEffectExtension.DeferredEffect_applyAOEDamage_CritPatch),
            nameof(DeferredEffectExtension.DeferredEffect_applyAOEDamage_CritPatch.ApplyCritEffect)
        );
        static readonly MethodInfo ICombatant_TakeWeaponDamage = AccessTools.Method(
            typeof(ICombatant),
            nameof(ICombatant.TakeWeaponDamage)
        );
        /* Pre Transpiler
         * ...
         * key.TakeWeaponDamage(hitInfo, keyValuePair5.Key, this.weapon, keyValuePair5.Value.Damage, 0f, 0, DamageType.AmmoExplosion);
         * ...
         * Post Transpiler
         * ...
         * key.TakeWeaponDamage(hitInfo, keyValuePair5.Key, this.weapon, keyValuePair5.Value.Damage, 0f, 0, DamageType.AmmoExplosion);
         * ApplyCritEffect(this, key, keyValuePair5, hitInfo);
         * ...
         */
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {

            var codes = new List<CodeInstruction>(instructions);
            var target = FindInsertionTarget(codes);

            codes.InsertRange(target, new CodeInstruction[] {
                new CodeInstruction(OpCodes.Ldarg_0),       //instance
                new CodeInstruction(OpCodes.Ldloc_S, 63),   //key
                new CodeInstruction(OpCodes.Ldloc_S, 66),   //keyValuePair5
                new CodeInstruction(OpCodes.Ldloc_S, 10),   //hitInfo
                new CodeInstruction(OpCodes.Call, DeferredEffectExtentsion_ApplyCritEffect)
            });

            return codes.AsEnumerable();
        }

        private static int FindInsertionTarget(List<CodeInstruction> codes) {
            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].Calls(ICombatant_TakeWeaponDamage)) {
                    return i + 1;
                }
            }
            Main.s_log.Log("DeferredEffect_applyAOEDamage_CritPatch: COULD NOT FIND TARGET");
            return -1;
        }
        private static void ApplyCritEffect(DeferredEffect instance, ICombatant target, KeyValuePair<int, AoEExplosionHitRecord> locationRecord, WeaponHitInfo hitInfo) {
            if (!EnableAOECrit.IsMatch(instance.definition.id)) { return; }
            var weapon = instance.weapon;
            var location = locationRecord.Key;
            var unit = target as AbstractActor;
            if (unit == null) { return; }
            var critInfo = new AdvCritLocationInfo(location, unit);

            unit.CheckForCrit(ref hitInfo, critInfo, weapon);
        }
    }
}
