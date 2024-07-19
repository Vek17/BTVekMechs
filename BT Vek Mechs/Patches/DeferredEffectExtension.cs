using BattleTech;
using CustAmmoCategories;
using EasyLayout;
using HarmonyLib;
using Org.BouncyCastle.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VekMechs.Patches;

static class DeferredEffectExtension {
    private static Regex TriggerAfterActivation = new("TriggerAfterActivation");

    //Update display counts for stuck tagged units
    [HarmonyPatch(typeof(DeferredEffect), nameof(DeferredEffect.RoundsRemain))]
    static class DeferredEffect_RoundsRemain_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref DeferredEffect __instance, ref int __result) {
            if (TriggerAfterActivation.IsMatch(__instance.definition.id)
                && __instance.ancor != null
            ) {
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
}
