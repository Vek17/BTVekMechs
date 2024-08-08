using BattleTech.Data;
using HarmonyLib;
using HBS.Extensions;
using HBS.Logging;
using UnityEngine;

namespace VekMechs;

public static class Main {
    public static readonly ILog s_log = HBS.Logging.Logger.GetLogger(nameof(VekMechs));
    public static void Start() {
        s_log.Log("Starting");

        // apply all patches that are in classes annotated with [HarmonyPatch]
        Harmony.CreateAndPatchAll(typeof(Main).Assembly);

        // run a specific patch found in a class which wasn't annotated with HarmonyPatch and therefore wasn't applied earlier
        //Harmony.CreateAndPatchAll(typeof(Main));
        s_log.Log("Started");
    }

    //[HarmonyPatch(typeof(VersionInfo), nameof(VersionInfo.GetReleaseVersion))]
    //[HarmonyPostfix]
    //[HarmonyAfter("io.github.mpstark.ModTek")]
    static void GetReleaseVersion(ref string __result) {
        var old = __result;
        __result = old + "\nHTWHX(2)";
    }

    [HarmonyPatch(typeof(DataManager), nameof(DataManager.PooledInstantiate))]
    static class DeferredEffect_RoundsRemain_DefferedEffectTrigger {

        [HarmonyPostfix]
        static void Postfix(ref GameObject __result, string id) {
            switch (id) {
                case "chrPrfWeap_riflemaniic_rightarm_laser_eh3":
                    __result.FindFirstChildNamed("jm6_right_arm_forearm_uac5_bh2").transform.DetachChildren();
                    break;
                case "chrPrfWeap_riflemaniic_rightarm_laser_eh1":
                    __result.FindFirstChildNamed("jm6_right_arm_forearm_uac5_bh1").transform.DetachChildren();
                    break;
                case "chrPrfWeap_riflemaniic_leftarm_laser_eh1":
                    __result.transform.localPosition = new Vector3(0.13f, 0, 0);
                    break;
                default:
                    break;
            }
        }
    }
}