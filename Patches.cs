using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>Changes the name, description and icon the inventory shows for the engine parts.</summary>
    internal static class Patches
    {
        public static void Apply(HarmonyLib.Harmony h)
        {
            foreach (string m in new[] { "GetDisplayNameWithoutConditionForInventoryInterfaces", "GetBasicDisplayNameForInventoryInterfaces", "GetDisplayNameForInventoryInterfaces", "GetTitleToDisplayWhenSelected", "get_DisplayName", "get_DisplayNameWithCondition", "GetCraftingDisplayName" })
                Patch(h, typeof(GearItem), m, new Type[0], "PostName");
            foreach (string m in new[] { "GetDescriptionToDisplayOnInventoryScreen", "GetDescription", "get_Description" })
                Patch(h, typeof(GearItem), m, new Type[0], "PostDescription");
            Patch(h, typeof(GearItem), "GetInventoryIconTexture", new Type[0], "PostIcon");
            Patch(h, typeof(InventoryGridItem), "Refresh", new[] { typeof(GearItem), typeof(int) }, "PostRefresh");
            Patch(h, typeof(InventoryGridItem), "RefreshDataItem", new[] { typeof(InventoryGridDataItem), typeof(int) }, "PostRefreshData");
            Patch(h, typeof(GearItemCoverflow), "SetGearItem", new[] { typeof(GearItem), typeof(string), typeof(bool) }, "PostCoverflow");
            // save / load: the part's mark travels inside the saved game
            Patch(h, typeof(GearItem), "Serialize", new[] { typeof(bool) }, "PostSave");
            Patch(h, typeof(GearItem), "Deserialize", new[] { typeof(GearItemSaveDataProxy), typeof(bool) }, "PostLoad");
            // the part's own name (GEAR_Knife_CP5) never reaches the save file or the lookup of the base object
            Patch(h, typeof(Inventory), "Serialize", new Type[0], "PostSerializeInventory");
            Patch(h, typeof(GearItem), "CreateSaveData", new Type[0], "PostCreateData");
            // the vehicle's position is only written when the game saves; on load it goes back to what was saved
            Patch(h, typeof(SaveGameSystem), "SaveGlobalData", new[] { typeof(SlotData) }, "PostSaveGame");
            Patch(h, typeof(SaveGameSystem), "RestoreGlobalData", new[] { typeof(string) }, "PostLoadGame");
            Patch(h, typeof(PlayerManager), "SetGearTransformForInspection", new Type[0], "PostInspection");
            Patch(h, typeof(PlayerManager), "UpdateGearTransformForInspection", new Type[0], "PostInspection");
            Patch(h, typeof(GearItem), "LoadGearItemPrefab", new[] { typeof(string) }, null, "PreNameBase");
            Patch(h, typeof(GearItem), "InstantiateGearItem", new[] { typeof(string) }, null, "PreNameBase");
            Patch(h, typeof(GearItem), "InstantiateAndDeserializeGearItem", new[] { typeof(string), typeof(string), typeof(Transform), typeof(bool), typeof(bool) }, null, "PreNameBase");
            Patch(h, typeof(GearItem), "InstantiateAndDeserializeGearItem", new[] { typeof(string), typeof(GearItemSaveDataProxy), typeof(Transform), typeof(bool), typeof(bool) }, null, "PreNameBase");
        }

        static void Patch(HarmonyLib.Harmony h, Type type, string method, Type[] args, string post, string pre = null)
        {
            try
            {
                MethodInfo orig = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null);
                if (orig == null) { MelonLoader.MelonLogger.Warning("[Patches] does not exist: " + type.Name + "." + method); return; }
                HarmonyMethod hpost = post != null ? new HarmonyMethod(typeof(Patches).GetMethod(post, BindingFlags.Public | BindingFlags.Static)) : null;
                HarmonyMethod hpre = pre != null ? new HarmonyMethod(typeof(Patches).GetMethod(pre, BindingFlags.Public | BindingFlags.Static)) : null;
                h.Patch(orig, prefix: hpre, postfix: hpost);
                MelonLoader.MelonLogger.Msg("[Patches] " + type.Name + "." + method + " patched");
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Patches] " + method + ": " + e.Message); }
        }

        public static void PostSaveGame() { try { Persistence.Confirm(); } catch (Exception) { } }
        public static void PostLoadGame() { try { Persistence.Revert(); } catch (Exception) { } }

        public static void PostInspection(PlayerManager __instance)
        {
            PartItems.FitInspection(__instance);
        }

        public static void PostSerializeInventory(ref string __result)
        {
            try { if (__result != null && __result.Contains(PartItems.Suf)) __result = PartItems.BaseOf(__result); } catch (Exception) { }
        }

        public static void PostCreateData(ref GearSaveData __result)
        {
            try
            {
                if (__result == null) return;
                string n = __result.m_PrefabName;
                if (n != null && n.Contains(PartItems.Suf)) __result.m_PrefabName = PartItems.BaseOf(n);
            }
            catch (Exception) { }
        }

        public static void PreNameBase(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0) return;
                string n = __args[0] as string;
                if (n != null && n.Contains(PartItems.Suf)) __args[0] = PartItems.BaseOf(n);
            }
            catch (Exception) { }
        }

        public static void PostCoverflow(GearItemCoverflow __instance, object[] __args)
        {
            try
            {
                int t;
                GearItem gi = __args[0] as GearItem;
                if (gi == null || !PartItems.IsPart(gi, out t)) return;
                Texture2D tx = PartItems.Icon(t);
                if (tx != null)
                {
                    if (__instance.m_Texture != null) __instance.m_Texture.mainTexture = tx;
                    if (__instance.m_TextureWithDamage != null) __instance.m_TextureWithDamage.mainTexture = tx;
                }
                if (__instance.m_Label != null) __instance.m_Label.text = Parts.NameOf(t);
                if (!noticeI) { noticeI = true; MelonLoader.MelonLogger.Msg("[Patches] large part icon applied"); }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Patches] coverflow: " + e.Message); }
        }

        public static void PostRefreshData(InventoryGridItem __instance, object[] __args)
        {
            try
            {
                InventoryGridDataItem d = __args[0] as InventoryGridDataItem;
                if (d == null || d.m_GearItem == null) return;
                Mark(__instance, d.m_GearItem);
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Patches] refresh data: " + e.Message); }
        }

        static void Mark(InventoryGridItem it, GearItem gi)
        {
            int t;
            if (gi == null || !PartItems.IsPart(gi, out t)) return;
            if (it.m_NameLabel != null) it.m_NameLabel.text = Parts.NameOf(t);
            Texture2D tx = PartItems.Icon(t);
            if (tx != null && it.m_GearSprite != null) it.m_GearSprite.mainTexture = tx;
        }

        static bool noticeN, noticeI;

        public static void PostSave(GearItem __instance, ref GearItemSaveDataProxy __result) { try { PartItems.OnSave(__instance, __result); } catch (Exception) { } }
        public static void PostLoad(GearItem __instance, object[] __args) { try { PartItems.OnLoad(__instance, __args[0] as GearItemSaveDataProxy); } catch (Exception) { } }

        public static void PostRefresh(InventoryGridItem __instance, object[] __args)
        {
            try
            {
                int t;
                GearItem gi = __args[0] as GearItem;
                if (gi == null || !PartItems.IsPart(gi, out t)) return;
                if (__instance.m_NameLabel != null) __instance.m_NameLabel.text = Parts.NameOf(t);
                Texture2D tx = PartItems.Icon(t);
                if (tx != null && __instance.m_GearSprite != null) __instance.m_GearSprite.mainTexture = tx;
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Patches] refresh: " + e.Message); }
        }

        public static void PostName(GearItem __instance, ref string __result)
        {
            int t;
            if (PartItems.IsPart(__instance, out t)) { __result = Parts.NameOf(t); if (!noticeN) { noticeN = true; MelonLoader.MelonLogger.Msg("[Patches] part name applied: " + __result); } }
        }

        public static void PostDescription(GearItem __instance, ref string __result)
        {
            int t;
            if (PartItems.IsPart(__instance, out t))
                __result = L.T("Pieza del motor de la camioneta. Se instala desde el menú del motor (mirá el motor con el capó abierto). Estado: ", "Truck engine part. Install it from the engine menu (inspect the engine with the hood open). Condition: ") + Parts.Pct(__instance.GetNormalizedCondition()) + ".";
        }

        public static void PostIcon(GearItem __instance, ref Texture2D __result)
        {
            int t;
            if (!PartItems.IsPart(__instance, out t)) return;
            Texture2D tx = PartItems.Icon(t);
            if (tx != null) __result = tx;
        }
    }
}
