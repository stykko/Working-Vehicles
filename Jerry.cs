using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace WorkingVehicles
{
    /// <summary>
    /// The game's jerry can (a LiquidItem that holds kerosene) used as a fuel can for the trucks: it can be filled at the pumps and poured
    /// into the truck's tank. It never goes below <see cref="ReservePercent"/> % so the game does not destroy it when it reaches 0.
    /// </summary>
    internal static class Jerry
    {
        public const float ReservePercent = 0.01f;     // 1 % of the capacity always stays inside

        public class Can { public GearItem gear; public LiquidItem liquid; }

        static float unit;
        static bool loggedFound;

        static float PerLiter()
        {
            if (unit <= 0f) unit = (float)ItemLiquidVolume.FromLiters(1f).m_Units;
            return unit;
        }

        public static float Liters(ItemLiquidVolume v) { return (float)v.m_Units / PerLiter(); }
        public static ItemLiquidVolume Volume(float liters) { return ItemLiquidVolume.FromLiters(Mathf.Max(0f, liters)); }

        public static float Capacity(LiquidItem li) { return Liters(li.m_LiquidCapacity); }
        public static float Amount(LiquidItem li) { return Liters(li.m_Liquid); }
        public static float Reserve(LiquidItem li) { return Capacity(li) * ReservePercent; }
        /// <summary>Liters that can still be taken out of the can without touching the reserve.</summary>
        public static float Available(LiquidItem li) { return Mathf.Max(0f, Amount(li) - Reserve(li)); }

        static bool IsKerosene(LiquidItem li)
        {
            try { return li.m_LiquidType == Il2CppTLD.Gear.LiquidType.GetKerosene(); }
            catch (Exception) { return false; }
        }

        /// <summary>Does it hold kerosene (or nothing)? Cans with water or antiseptic are left alone.</summary>
        public static bool Compatible(LiquidItem li) { return IsKerosene(li) || Amount(li) <= 0.001f; }

        /// <summary>Jerry cans in the player's inventory.</summary>
        public static void Find(List<Can> result)
        {
            result.Clear();
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                if (inv == null || inv.m_Items == null) return;
                var items = inv.m_Items;
                for (int i = 0; i < items.Count; i++)
                {
                    GearItem g = null;
                    try { g = items[i].m_GearItem; } catch (Exception) { }
                    if (g == null) continue;
                    string n = g.name;
                    if (n == null || n.IndexOf("jerry", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    LiquidItem li = g.m_LiquidItem;
                    if (li == null) continue;
                    result.Add(new Can { gear = g, liquid = li });
                    if (!loggedFound)
                    {
                        loggedFound = true;
                        MelonLoader.MelonLogger.Msg("[Jerry] jerry can found in the inventory: '" + n + "' " + Amount(li).ToString("0.0") + " L / " + Capacity(li).ToString("0.0") + " L");
                    }
                }
            }
            catch (Exception e) { MelonLoader.MelonLogger.Warning("[Jerry] search: " + e.Message); }
        }

        /// <summary>The can with the most fuel that can be poured (null if none).</summary>
        public static LiquidItem FullestPourable(List<Can> cans)
        {
            LiquidItem best = null;
            float bestA = 0.01f;
            foreach (Can c in cans)
            {
                try
                {
                    if (c.gear == null || c.liquid == null || !IsKerosene(c.liquid)) continue;
                    float a = Available(c.liquid);
                    if (a > bestA) { bestA = a; best = c.liquid; }
                }
                catch (Exception) { }
            }
            return best;
        }

        /// <summary>The emptiest can that still has room and can take kerosene (null if none).</summary>
        public static LiquidItem EmptiestFillable(List<Can> cans)
        {
            LiquidItem best = null;
            float bestFree = 0f;
            foreach (Can c in cans)
            {
                try
                {
                    if (c.gear == null || c.liquid == null || !Compatible(c.liquid)) continue;
                    float free = Capacity(c.liquid) - Amount(c.liquid);
                    if (free > bestFree) { bestFree = free; best = c.liquid; }
                }
                catch (Exception) { }
            }
            return best;
        }

        public static void Set(LiquidItem li, float liters)
        {
            if (!IsKerosene(li)) li.m_LiquidType = Il2CppTLD.Gear.LiquidType.GetKerosene();
            li.m_Liquid = Volume(Mathf.Clamp(liters, 0f, Capacity(li)));
        }
    }
}
