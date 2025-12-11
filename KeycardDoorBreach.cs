using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using EFT;
using EFT.Interactive;

namespace Ciallo.KeycardDoorBreach
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ciallo.keycarddoorbreach";
        //public const string PluginName = "KeycardDoor simplified + 100% DoorBreach";
        public const string PluginName = "刷卡门简化 + 100% 踹门成功";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        internal static ConfigEntry<bool> AlwaysBreachSuccess;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);

            AlwaysBreachSuccess = Config.Bind(
                "General",
                "AlwaysBreachSuccess",
                false,
                //"Forced Door Breaching Success"
                "踹门必定成功"
            );

            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo($"{PluginName} v{PluginVersion} 已加载并完成 Harmony 打补丁。");
            }
            catch (Exception ex)
            {
                Log.LogError($"初始化补丁失败: {ex}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
                Log.LogInfo($"{PluginName} 已取消当前实例的补丁。");
            }
            catch (Exception e)
            {
                Log.LogWarning($"取消补丁失败: {e}");
            }
        }
    }

    // 刷卡门简化
    [HarmonyPatch]
    internal static class Patch_GetAvailableActions
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            var target = typeof(GetActionsClass)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "GetAvailableActions") return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[0].ParameterType == typeof(GamePlayerOwner);
                });

            if (target == null)
            {
                Plugin.Log?.LogError("未能定位 GetActionsClass.GetAvailableActions(owner, interactive) 重载。");
                throw new MissingMethodException("GetActionsClass.GetAvailableActions(GamePlayerOwner, *) not found.");
            }

            Plugin.Log?.LogInfo($"目标方法: {target.DeclaringType?.FullName}.{target.Name}");
            return target;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var list = instructions.ToList();
            int idxIsinst = list.FindIndex(ci => ci.opcode == OpCodes.Isinst && ci.operand is Type t && t == typeof(KeycardDoor));

            if (idxIsinst < 0)
            {
                Plugin.Log?.LogWarning("未找到 KeycardDoor 的 isinst 指令。保持原始 IL。");
                return list;
            }

            int removeCount = Math.Min(5, list.Count - idxIsinst);
            Plugin.Log?.LogInfo($"移除 KeycardDoor 判定 IL，从索引 {idxIsinst} 起 {removeCount} 条。");
            list.RemoveRange(idxIsinst, removeCount);

            return list;
        }
    }

    // 踹门必成功
    [HarmonyPatch(typeof(Door), nameof(Door.BreachSuccessRoll))]
    internal static class Patch_BreachSuccess
    {
        private static bool Prefix(ref bool __result)
        {
            if (Plugin.AlwaysBreachSuccess.Value)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
