using BattleTech;
using CustAmmoCategories;
using EasyLayout;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Localize;
using UnityEngine;

namespace VekMechs.Patches;

static class DeferredEffectExtension {
    private static Regex TriggerAfterActivation = new("TriggerAfterActivation");

    [HarmonyPatch(typeof(DeferredEffect), nameof(DeferredEffect.RoundsRemain))]
    static class DeferredEffect_RoundsRemain_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref DeferredEffect __instance, ref int __result) {
            Main.s_log.Log($"DeferredEffect.RoundsRemain");
            if (TriggerAfterActivation.IsMatch(__instance.definition.id)
                && __instance.ancor != null
            ) {
                Main.s_log.Log($"DeferredEffect.RoundsRemain = 1");
                __result = 1;
            }
        }
    }

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
            if (ancor == null || !TriggerAfterActivation.IsMatch(__instance.definition.id)) {
                return;
            }
            __instance.CountDownFloatie.Text.SetText(def.text + ":" + roundsText.ToString());
        }
    }
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
                    Main.s_log.Log($"{ancor?.name} Matched");
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
}
