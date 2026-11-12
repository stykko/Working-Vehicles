using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(WorkingVehicles.Mod), "Working Vehicles", "1.0.13", "stykko")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace WorkingVehicles
{
    /// <summary>What the mod knows about each vehicle in the world.</summary>
    internal class Veh
    {
        public Transform root;
        public Vector3 orig;
        public bool isRunning;
        public float steering;            // degrees, positive = to the right
        public Body body;
        public bool bodyTried;
        public float level = 1f;         // snow: 0 = clean, 1 = all
        public float hour = -1f;         // in-game hours of the last snow update
        public float levelSaved = 1f;
        public bool isTruck;         // the trucks (and cars) need a battery: "motorized vehicle"
        public bool isCar;           // car (uses the car settings); isTruck is also true for cars
        public bool battery;
        public float batteryCondition = 1f;
        public Hood hood;
        public bool hoodTried;
        public Dashboard dashboard;          // dashboard gauges, buttons and lights (trucks)
        public bool dashboardTried;
        public float dashboardT;
        public float[] parts;           // condition of each engine part; -1 = not there
        public float fuel = 0.4f; // tank: 0..1 (60 L)
        public Tank tank;            // fuel cap (trucks)
        public bool tankTried;
        public bool noticeLow;
        public string gid;            // garage id: this vehicle was brought out of the garage (it is not a vehicle of the game)

        /// <summary>Why the truck cannot run (null = it can).</summary>
        public string Reason()
        {
            if (isTruck && Settings.AuroraOnly() && !AuroraGate.Active()) return L.T("sin aurora: no hay energía", "no aurora: no power");
            if (!isTruck || !Settings.RequireBattery()) return null;
            if (!battery) return L.T("falta la batería", "the battery is missing");
            if (batteryCondition <= Parts.Min) return L.T("la batería está descargada (", "the battery is dead (") + Parts.Pct(batteryCondition) + ")";
            if (parts == null) return null;
            for (int i = 0; i < Parts.Count; i++)
            {
                if (parts[i] < 0f) return L.T("falta: ", "missing: ") + Parts.NameOf(i);
                if (parts[i] <= Parts.Min) return Parts.NameOf(i) + L.T(" muy dañada (", " is too damaged (") + Parts.Pct(parts[i]) + ")";
            }
            return null;
        }

        public bool Broken { get { return Reason() != null; } }

        /// <summary>Empty tank (trucks only, if consumption is on).</summary>
        public bool WithoutFuel { get { return isTruck && Settings.UseFuel() && fuel <= 0.001f; } }

        /// <summary>How many parts (counting the battery) are below 50 %.</summary>
        public int WornOnes()
        {
            int n = battery && batteryCondition < Parts.Worn ? 1 : 0;
            if (parts != null)
                for (int i = 0; i < Parts.Count; i++)
                    if (parts[i] >= 0f && parts[i] < Parts.Worn) n++;
            return n;
        }
    }

    /// <summary>
    /// Moves the ORIGINAL truck while the player is seated (PlayerInVehicle.IsInside()).
    /// It doesn't touch getting in/out or the camera: the game already leaves the player at the door's "CameraDummy";
    /// the mod only moves the truck and drags the player and their view along with it.
    /// </summary>
    public partial class Mod : MelonMod
    {
        // Game references (requested again when sitting down)
        PlayerInVehicle inVeh;
        vp_FPSCamera vpCam;
        vp_FPSController ctrl;
        Transform root, dummyCam, playerRoot;
        Vector3 fwdLocal;

        bool driving;
        float vel;
        Vector3 normalSmooth = Vector3.up;
        float velV, withoutGroundT, goodT;                 // vertical speed (gravity), time without ground found, timer for the safe spot
        Vector3 lastGood;                           // last position where the truck stood on the ground
        Quaternion lastRotGood;
        bool hasGood;
        float logT;
        bool errorLogged;
        Veh current;

        // Persistence and dragged objects
        readonly HashSet<int> seen = new HashSet<int>();
        readonly Dictionary<int, Veh> vehs = new Dictionary<int, Veh>();
        float scanningT, saveT;
        readonly Engine engine = new Engine();
        readonly List<Transform> carried = new List<Transform>();
        readonly List<Rigidbody> bodiesFixed = new List<Rigidbody>();

        // Vehicle menu
        bool menuOpen;
        int sel;
        static string NameOption(int i)
        {
            switch (i)
            {
                case 0: return L.T("Motor", "Engine");
                case 1: return L.T("Nieve", "Snow");
                case 2: return L.T("Luces", "Lights");
                case 3: return L.T("Aire acondicionado", "Air conditioning");
                case 4: return L.T("Combustible", "Fuel");
                default: return L.T("Cerrar", "Close");
            }
        }

        static readonly string[] options = { "Motor", "Nieve", "Luces", "Aire acondicionado", "Combustible", "Cerrar" };
        string notice = "";
        float noticeT;

        public override void OnInitializeMelon()
        {
            Cfg.Init();
            // translations of the Mod Options texts: the patches must be in place before the options page is registered
            try { HarmonyInstance.CreateClassProcessor(typeof(LocalizationPatch)).Patch(); } catch (Exception e) { LoggerInstance.Warning("Localization patch (game strings): " + e.Message); }
            try { HarmonyInstance.CreateClassProcessor(typeof(DescriptionTextTranslatePatch)).Patch(); } catch (Exception e) { LoggerInstance.Warning("Localization patch (descriptions): " + e.Message); }
            Settings.Start();
            try { Radio.Scan(); } catch (Exception) { }           // creates the music folder (Mods\WorkingVehicles) so it is easy to find
            try { Patches.Apply(HarmonyInstance); } catch (Exception e) { LoggerInstance.Warning("Could not apply the inventory patches: " + e.Message); }
            LoggerInstance.Msg("Working Vehicles 1.0.8 loaded. Keys (change them in Mod Options): " + Settings.KeyGarage() + " = garage, " + Settings.KeyMenu() + " = vehicle menu. Sit in a truck or car and use W A S D / Space.");
        }

        public override void OnUpdate()
        {
            try
            {
                if (noticeT > 0f) noticeT -= Time.unscaledDeltaTime;

                if (!Cfg.Enabled.Value || Time.timeScale < 0.01f)
                {
                    menuOpen = false;
                    hoodMenu = false;
                    confirmStore = false;
                    garageMenu = false;
                    if (driving && !Cfg.Enabled.Value) Stop("mod desactivado");
                    return;
                }

                if (!driving)
                {
                    scanningT -= Time.unscaledDeltaTime;
                    if (scanningT <= 0f) { scanningT = 2f * Settings.ScanScale(); RestoreVehicles(); }
                }

                ProcessJob();
                UpdateSnow(Time.unscaledDeltaTime);

                VehicleDoor door;
                bool seated = IsSeated(out door);

                // Only the trucks are driven (the other vehicles have no engine, battery or fuel); there is an experimental option for the rest.
                if (seated && !driving && job == null && !Settings.DriveOther())
                {
                    Transform rv = RootOf(door);
                    if (rv == null || !IsTruckRoot(rv)) seated = false;
                }

                if (seated && !driving && job == null)
                {
                    // Seated suddenly shortly after loading the game (without having seen the entry): that was the game, not you.
                    bool forced = Cfg.ExitOnLoad.Value && Time.realtimeSinceStartup - lastLoad < 25f && Time.realtimeSinceStartup - enteringT > 3f;
                    if (forced) StartExit(door);
                    else Begin(door);
                }
                else if (!seated && driving) Stop("the player left or is in transition");

                TickRadio();

                if (driving)
                {
                    if (current.isRunning && Settings.AuroraOnly() && !AuroraGate.Active())
                    {
                        current.isRunning = false;
                        engine.TurnOff();
                        Notice(L.T("Terminó la aurora: el motor se apaga", "The aurora ended: the engine shuts off"));
                        SavePosition();
                    }
                    UpdateStarter(Mathf.Min(Time.deltaTime, 0.05f));
                    if (!current.isRunning && startsT <= 0f) offT += Time.unscaledDeltaTime; else offT = 0f;
                    ControlAc(Mathf.Min(Time.deltaTime, 0.05f));
                    HandleMenu();
                    Drive(Mathf.Min(Time.deltaTime, 0.05f));
                    HandleDashboard(Mathf.Min(Time.deltaTime, 0.05f));
                }
                else
                {
                    menuOpen = false;
                    if (Input.GetKeyDown(Settings.KeyGenerate())) GenerateParts();
                    UpdateProps(Time.unscaledDeltaTime);
                    GarageTick(Time.unscaledDeltaTime);
                    HandleHood(seated);
                    HandleFuel(seated);
                }
            }
            catch (Exception e)
            {
                if (driving) { driving = false; vel = 0f; menuOpen = false; DropObjects(); engine.TurnOff(); }
                if (!errorLogged)
                {
                    errorLogged = true;
                    LoggerInstance.Error("Error in Working Vehicles (shown only once): " + e);
                }
            }
        }

        // ------------------------------------------------------------------ vehicle menu

        void Notice(string t) { notice = t; noticeT = 2.5f; }

        void HandleMenu()
        {
            if (Radio.menu) return;                                     // the radio's menu has the keys
            if (Input.GetKeyDown(Settings.KeyMenu()))
            {
                menuOpen = !menuOpen;
                sel = 0;
                return;
            }
            if (!menuOpen) return;

            if (Input.GetKeyDown(KeyCode.Backspace)) { menuOpen = false; return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) sel = (sel + 1) % options.Length;
            if (Input.GetKeyDown(KeyCode.UpArrow)) sel = (sel + options.Length - 1) % options.Length;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (sel == 0) ToggleEngine();
                else if (sel == 1) ToggleSnow();
                else if (sel == 2) Notice(current.dashboard != null ? current.dashboard.ToggleLights(current.Broken) : L.T("El tablero todavía no está listo (solo camionetas)", "The dashboard isn't ready yet (trucks only)"));
                else if (sel == 3) Notice(current.dashboard != null ? current.dashboard.ToggleAc(current.Broken) : L.T("El tablero todavía no está listo (solo camionetas)", "The dashboard isn't ready yet (trucks only)"));
                else if (sel == 5) menuOpen = false;
                else Notice(L.T("Combustible: ", "Fuel: ") + Mathf.RoundToInt(current.fuel * DataTank.Capacity) + L.T(" L de ", " L of ") + Mathf.RoundToInt(DataTank.Capacity) + L.T(". Se carga en las bombas de las gasolineras (abrí el tapón del tanque).", ". Fill up at the gas station pumps (open the fuel cap first)."));
            }
        }

        // ------------------------------------------------------------------ engine part wear (by distance traveled)

        void WearParts(float dt, bool on)
        {
            if (!on || current == null || !current.isTruck || current.parts == null) return;
            float per10 = Settings.WearPer10Km();
            if (per10 <= 0f) return;
            float meters = Mathf.Abs(vel) * dt;
            if (meters <= 0f) return;
            float loss = per10 / 100f * meters / 10000f;           // fraction of condition per meter
            for (int i = 0; i < Parts.Count; i++)
            {
                float c = current.parts[i];
                if (c < 0f) continue;                                  // part that is not installed
                float n = Mathf.Max(0f, c - loss);
                current.parts[i] = n;
                if (c >= Parts.Worn && n < Parts.Worn)
                    Notice(Parts.NameOf(i) + L.T(" está gastada (" , " is worn (") + Parts.Pct(n) + ")");
                if (c > Parts.Min && n <= Parts.Min)
                {
                    Notice(Parts.NameOf(i) + L.T(" se rompió: el motor se detiene", " broke: the engine stops"));
                    if (current.isRunning) { current.isRunning = false; engine.TurnOff(); }
                    SavePosition();
                }
            }
        }

        // ------------------------------------------------------------------ battery: drains only when starting and with lights / A/C on while the engine is OFF; recharges while the engine runs

        // The radio left on keeps playing from the vehicle when you get out (quieter and muffled), using the battery.
        Veh radioVeh;

        void TickRadio()
        {
            if (current != null)
            {
                if (radioVeh != current && Radio.on) Radio.TurnOff();      // you sat in another vehicle: the old one's radio goes off
                radioVeh = current;
            }
            bool inside = driving && current != null;
            if (inside)
            {
                Radio.Tick(true, current.isTruck && (current.isRunning ? !current.Broken && !current.WithoutFuel : current.battery && current.batteryCondition > Parts.Min), current.root);
                return;
            }
            Veh v = radioVeh;
            bool ok = v != null && v.root != null && v.root.gameObject.activeInHierarchy && v.battery && v.batteryCondition > Parts.Min;
            if (ok)
            {
                Transform pl = GameManager.GetPlayerTransform();
                ok = pl != null && (pl.position - v.root.position).sqrMagnitude < 150f * 150f;      // far away it is switched off (and stops using the battery)
            }
            if (Radio.on && ok)
            {
                v.batteryCondition = Mathf.Clamp01(v.batteryCondition - Settings.BatteryDrain() / 5f / 100f * Time.deltaTime);
                if (v.batteryCondition <= Parts.Min) ok = false;
            }
            if (Radio.on && !ok) { Radio.TurnOff(); try { if (v != null && v.root != null) SaveVeh(v); } catch (Exception) { } }
            Radio.Tick(false, ok && Radio.on, ok ? v.root : null);
        }

        void DrainBattery(float dt)
        {
            if (current == null || !current.isTruck || !current.battery) return;
            bool ac = current.dashboard != null && current.dashboard.ac;
            bool lights = current.dashboard != null && current.dashboard.lights;
            bool running = current.isRunning && !current.WithoutFuel;
            float change = 0f;
            if (startsT > 0f) change -= Settings.BatteryDrain() / 5f * 3f;             // starting the engine costs battery
            else if (!running && (ac || lights || Radio.on)) change -= Settings.BatteryDrain() / 5f;   // lights / A/C / radio with the engine OFF drain it
            else if (running) change += Settings.BatteryRecharge() / 10f;              // engine running: recharges, nothing drains it
            if (change == 0f) return;
            float before = current.batteryCondition;
            current.batteryCondition = Mathf.Clamp01(current.batteryCondition + change / 100f * dt);
            if (before > Parts.Min && current.batteryCondition <= Parts.Min)
            {
                // ran out of battery: the engine turns off (and the dashboard turns off lights and A/C by itself)
                if (current.isRunning) { current.isRunning = false; engine.TurnOff(); }
                SavePosition();
                Notice(L.T("Batería agotada", "Battery dead"));
            }
        }

        // ------------------------------------------------------------------ air conditioning (heats the cabin)

        float tempBase = float.NaN;        // temperature increase the game gives for being inside the vehicle (the original)
        float acExtra;                   // degrees the air conditioning adds on top of the original
        float acT0 = float.NaN;          // interior temperature without A/C (so we never overshoot)
        float acLog;

        void ControlAc(float dt)
        {
            try
            {
                PlayerInVehicle pv = GameManager.GetPlayerInVehicle();
                Weather w = GameManager.GetWeatherComponent();
                if (pv == null || w == null || current == null) return;
                if (float.IsNaN(tempBase)) tempBase = pv.m_TempIncrease;

                bool active = current.dashboard != null && current.dashboard.ac && current.isRunning && !current.Broken && !current.WithoutFuel;
                float target = Settings.TempAc();
                float temp = w.GetCurrentTemperature();
                if (acExtra <= 0.001f && !active) acT0 = temp;               // "normal" cabin temperature
                if (active && float.IsNaN(acT0)) acT0 = temp - acExtra;

                float before = acExtra;
                if (active)
                {
                    if (temp < target - 0.2f) acExtra += 0.12f * dt;        // rises gradually (~2 °C every 17 seconds)
                    else if (temp > target + 0.5f) acExtra -= 0.12f * dt;
                    float limit = Mathf.Max(0f, target - acT0 + 2f);       // never more than needed to reach the target
                    acExtra = Mathf.Clamp(acExtra, 0f, limit);
                }
                else acExtra = Mathf.Max(0f, acExtra - 0.08f * dt);       // without A/C it gradually cools down

                if (Mathf.Abs(acExtra - before) > 0f) pv.m_TempIncrease = tempBase + acExtra;

                if ((active || acExtra > 0.01f) && Time.unscaledTime > acLog)
                {
                    acLog = Time.unscaledTime + 15f;
                    LoggerInstance.Msg("[AC] cabin " + temp.ToString("F1") + " °C (target " + target.ToString("F0") + "), original increase +" + tempBase.ToString("F1") + ", A/C +" + acExtra.ToString("F1") + (active ? " (on)" : " (off)"));
                }
            }
            catch (Exception e)
            {
                if (!acError) { acError = true; LoggerInstance.Warning("[AC] could not control the temperature: " + e.Message); }
            }
        }
        bool acError;

        void RestoreAc()
        {
            try
            {
                acExtra = 0f;
                acT0 = float.NaN;
                if (float.IsNaN(tempBase)) return;
                PlayerInVehicle pv = GameManager.GetPlayerInVehicle();
                if (pv != null) pv.m_TempIncrease = tempBase;
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ hood, engine and battery

        Veh hoodVeh;
        bool hoodMenu;
        int selHood;
        // The "get parts / drop pack" row is a testing trick: only in debug mode.
        static bool RowHidden(int i) { return i == Parts.Count + 1 && !Settings.Debug(); }
        string hint = "";
        // 0 = battery, 1..6 = engine parts, then pack and exit
        static readonly string[] optionsHood = { "Batería", Parts.NameOf(0), Parts.NameOf(1), Parts.NameOf(2), Parts.NameOf(3), Parts.NameOf(4), Parts.NameOf(5), "Soltar la mochila en el suelo", "Guardar el vehículo en el garaje", "Salir" };
        const string NameBattery = "GEAR_CarBattery";

        public override void OnLateUpdate()
        {
            Blur.Update(Time.unscaledDeltaTime);
            try { Trunk.Tick(Time.deltaTime); } catch (Exception) { }
            try
            {
                foreach (Veh v in vehs.Values)
                    if (v.hood != null) v.hood.Update();
            }
            catch (Exception) { }
        }

        static int BatteriesInInventory()
        {
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                return inv != null ? inv.GetNumGearWithName(NameBattery) : 0;
            }
            catch (Exception) { return 0; }
        }

        bool TakeBattery(Veh v)
        {
            try
            {
                Inventory inv = GameManager.GetInventoryComponent();
                if (inv == null || inv.GetNumGearWithName(NameBattery) <= 0) return false;
                GearItem gi = inv.GetHighestConditionGearThatMatchesName(NameBattery);
                if (gi == null) return false;
                v.batteryCondition = Mathf.Clamp01(gi.GetNormalizedCondition());
                inv.RemoveGear(gi.gameObject);
                UnityEngine.Object.Destroy(gi.gameObject);
                return true;
            }
            catch (Exception e) { LoggerInstance.Warning("Could not take the battery out of the inventory: " + e.Message); return false; }
        }

        bool GiveBackBattery(Veh v)
        {
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                GearItem gi = GearItem.InstantiateGearItem(NameBattery);
                if (gi == null || pm == null) return false;
                try { gi.SetNormalizedHP(v.batteryCondition, false); } catch (Exception) { }
                pm.AddItemToPlayerInventory(gi, false, true);
                return true;
            }
            catch (Exception e) { LoggerInstance.Warning("Could not return the battery to the inventory: " + e.Message); return false; }
        }

        void ActionBattery(Veh v)
        {
            if (v.battery)
            {
                if (!GiveBackBattery(v)) { Notice(L.T("No pude devolverla al inventario", "Could not return it to the inventory")); return; }
                v.battery = false;
                v.isRunning = false;
                ApplyHood(v);
                Notice(L.T("Batería retirada", "Battery removed"));
            }
            else
            {
                if (!TakeBattery(v)) { Notice(L.T("No tenés una batería de auto en el inventario", "You don't have a car battery in your inventory")); return; }
                v.battery = true;
                ApplyHood(v);
                Notice(L.T("Batería instalada", "Battery installed"));
            }
            SaveVeh(v);
            LoggerInstance.Msg("Battery of '" + v.root.name + "': " + (v.battery ? "INSTALLED" : "removed"));
        }

        void ChangeOfParts(Veh v)
        {
            v.isRunning = false;
            ApplyHood(v);
            SaveVeh(v);
            Storage.Save(Persistence.Slot());
        }

        void ActionPart(Veh v, int i)
        {
            if (v.parts == null) return;
            if (v.parts[i] >= 0f)
            {
                // removing: whatever is mounted on top must be removed first
                for (int j = 0; j < Parts.Count; j++)
                    if (Parts.GoesOn[j] == i && v.parts[j] >= 0f) { Notice(L.T("Primero sacá: ", "Remove first: ") + Parts.NameOf(j)); return; }
                if (PartItems.Active)
                {
                    if (!PartItems.Give(i, v.parts[i])) { Notice(L.T("No pude pasarla al inventario", "Could not move it to the inventory")); return; }
                    Notice(Parts.NameOf(i) + L.T(" retirada (está en tu inventario)", " removed (it's in your inventory)"));
                }
                else
                {
                    Storage.pack.Add(new InPack { type = i, condition = v.parts[i] });
                    Notice(Parts.NameOf(i) + L.T(" retirada (está en tu mochila)", " removed (it's in your pack)"));
                }
                v.parts[i] = -1f;
            }
            else
            {
                int baseP = Parts.GoesOn[i];
                if (baseP >= 0 && v.parts[baseP] < 0f) { Notice(L.T("Primero instalá: ", "Install first: ") + Parts.NameOf(baseP)); return; }
                bool objects = PartItems.Active;
                float c = objects ? PartItems.Take(i) : Storage.TakeBest(i);
                if (c < 0f) { Notice(L.T("No tenés ", "You don't have ") + Parts.NameOf(i) + L.T(" en el inventario", " in your inventory")); return; }
                v.parts[i] = c;
                Notice(Parts.NameOf(i) + L.T(" instalada (", " installed (") + Parts.Pct(c) + ")");
            }
            ChangeOfParts(v);
            LoggerInstance.Msg("Parts of '" + v.root.name + "': " + string.Join(", ", Array.ConvertAll(v.parts, x => x < 0f ? "-" : Parts.Pct(x))));
        }

        void GenerateParts()
        {
            if (PartItems.Active)
            {
                int n = PartItems.Generate();
                if (n > 0)
                {
                    Notice(n + L.T(" piezas nuevas en tu inventario", " new parts in your inventory"));
                    LoggerInstance.Msg("[Parts] generated " + n + " new parts in the inventory");
                    return;
                }
                Notice(L.T("No pude crear las piezas como objetos del juego (mirá el log)", "Could not create the parts as game items (check the log)"));
                return;
            }
            for (int i = 0; i < Parts.Count; i++)
                Storage.pack.Add(new InPack { type = i, condition = 0.7f + UnityEngine.Random.value * 0.3f });
            Storage.Save(Persistence.Slot());
            Notice(L.T("Piezas nuevas en la mochila", "New parts in your pack"));
        }

        static string Scene()
        {
            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; }
            catch (Exception) { return ""; }
        }

        // Ground height at that point (null if nothing was found).
        static float? HeightGround(Vector3 xz, float yRef)
        {
            try
            {
                RaycastHit[] hs = Physics.RaycastAll(new Vector3(xz.x, yRef + 1.5f, xz.z), Vector3.down, 5f);
                float best = float.MaxValue;
                bool exists = false;
                foreach (RaycastHit h in hs)
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    string n = h.collider.transform.root.name;
                    if (n.StartsWith("WV_Part_") || n.Contains("Player") || n.Contains("PLAYER")) continue;
                    if (h.distance < best) { best = h.distance; exists = true; }
                }
                if (exists) return yRef + 1.5f - best;
            }
            catch (Exception) { }
            return null;
        }

        void DropPack()
        {
            if (Storage.pack.Count == 0) { Notice(L.T("La mochila está vacía", "Your pack is empty")); return; }
            Camera cam = GameManager.GetMainCamera();
            Transform pj = GameManager.GetPlayerTransform();
            if (cam == null) return;
            Vector3 feet = pj != null ? pj.position : cam.transform.position + Vector3.down * 1.6f;
            Vector3 fr = cam.transform.forward; fr.y = 0f;
            if (fr.sqrMagnitude < 0.01f) fr = Vector3.forward;
            fr.Normalize();
            Vector3 sideSign = new Vector3(fr.z, 0f, -fr.x);
            string sc = Scene();
            int n = 0;
            foreach (InPack m in Storage.pack)
            {
                Vector3 xz = feet + fr * 1.0f + sideSign * (0.35f * (n - (Storage.pack.Count - 1) * 0.5f));
                GameObject go = Hood.CreateObject(m.type);
                float? ground = HeightGround(xz, feet.y);
                float y = (ground.HasValue ? ground.Value : feet.y) + Hood.SemiHeight(go) + 0.01f;
                Vector3 p = new Vector3(xz.x, y, xz.z);
                go.transform.position = p;
                Storage.ground.Add(new Loose { type = m.type, condition = m.condition, scene = sc, pos = p, go = go });
                LoggerInstance.Msg("[Parts] " + Parts.NameOf(m.type) + " dropped at " + p + " (scene '" + sc + "', ground " + (ground.HasValue ? "found" : "not found: using the feet position") + ")");
                n++;
            }
            Storage.pack.Clear();
            Storage.Save(Persistence.Slot());
            Notice(L.T("Piezas dejadas en el suelo", "Parts dropped on the ground"));
        }

        float propsT;
        // Keeps the loose parts in the world: creates them as you approach and remembers where they were left.
        void UpdateProps(float dt)
        {
            if (PartItems.Active)
            {
                PartItems.Ensure(Persistence.Slot());
                PartItems.Sweep(dt);
            }
            propsT -= dt;
            if (propsT > 0f) return;
            propsT = 1.5f * Settings.ScanScale();
            Storage.Ensure(Persistence.Slot());
            if (PartItems.Active && !PartItems.Blocked && Storage.ground.Count > 0)
            {
                // the parts that were lying under the engine (old system) go to the inventory
                for (int i = Storage.ground.Count - 1; i >= 0; i--)
                {
                    Loose sl = Storage.ground[i];
                    if (!PartItems.Give(sl.type, sl.condition)) continue;
                    if (sl.go != null) UnityEngine.Object.Destroy(sl.go);
                    Storage.ground.RemoveAt(i);
                }
                Storage.Save(Persistence.Slot());
                Notice(L.T("Las piezas que estaban en el suelo pasaron a tu inventario", "The parts that were on the ground moved to your inventory"));
            }
            if (PartItems.Active && !PartItems.Blocked && Storage.pack.Count > 0)
            {
                // what was in the old pack goes to the inventory
                for (int i = Storage.pack.Count - 1; i >= 0; i--)
                    if (PartItems.Give(Storage.pack[i].type, Storage.pack[i].condition)) Storage.pack.RemoveAt(i);
                Storage.Save(Persistence.Slot());
                Notice(L.T("Las piezas de la mochila pasaron a tu inventario", "The parts from your pack moved to your inventory"));
            }
            Transform pj = GameManager.GetPlayerTransform();
            if (pj == null) return;
            string sc = Scene();
            foreach (Loose s in Storage.ground)
            {
                if (PartItems.Active || s.scene != sc || s.go != null) continue;
                if ((s.pos - pj.position).sqrMagnitude > 14400f) continue;
                s.go = Hood.CreateObject(s.type);
                s.go.transform.position = s.pos;
                LoggerInstance.Msg("[Parts] " + Parts.NameOf(s.type) + " appears again at " + s.pos);
            }
        }

        // The loose part I'm looking at up close (null if none).
        Loose PartLooked(Vector3 o, Vector3 f, out float cos)
        {
            Loose best = null;
            cos = 0.93f;
            foreach (Loose s in Storage.ground)
            {
                if (s.go == null) continue;
                Vector3 d = s.go.transform.position - o;
                float dist = d.magnitude;
                if (dist > 3f || dist < 0.01f) continue;
                float c = Vector3.Dot(f, d / dist);
                if (c > cos) { cos = c; best = s; }
            }
            return best;
        }

        void HandleHood(bool seated)
        {
            hint = "";
            if (seated || job != null) { hoodMenu = false; confirmStore = false; return; }
            Camera cam = GameManager.GetMainCamera();
            if (cam == null) return;
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            KeyCode hotkey = Settings.KeyHood();

            // ---- engine menu open
            if (hoodMenu)
            {
                if (hoodVeh == null || hoodVeh.root == null || hoodVeh.hood == null || (hoodVeh.hood.PointEngine() - o).sqrMagnitude > 25f)
                {
                    hoodMenu = false;
                    confirmStore = false;
                }
                else
                {
                    if (HandleConfirmStore()) return;
                    if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(hotkey)) { hoodMenu = false; return; }
                    if (Input.GetKeyDown(KeyCode.DownArrow)) { selHood = (selHood + 1) % optionsHood.Length; if (RowHidden(selHood)) selHood = (selHood + 1) % optionsHood.Length; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { selHood = (selHood + optionsHood.Length - 1) % optionsHood.Length; if (RowHidden(selHood)) selHood = (selHood + optionsHood.Length - 1) % optionsHood.Length; }
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        if (selHood == 0) ActionBattery(hoodVeh);
                        else if (selHood <= Parts.Count) ActionPart(hoodVeh, selHood - 1);
                        else if (selHood == Parts.Count + 1) { if (PartItems.Active) GenerateParts(); else DropPack(); }
                        else if (selHood == Parts.Count + 2) { confirmStore = true; selConfirm = 1; }
                        else hoodMenu = false;
                    }
                    return;
                }
            }

            // ---- which truck am I looking at?
            Veh best = null;
            float bestCos = 0.85f;
            foreach (Veh v in vehs.Values)
            {
                if (!v.isTruck || v.root == null) continue;
                if ((v.root.position - o).sqrMagnitude > 49f) continue;

                CreateHood(v);
                if (v.hood == null) continue;

                if (!v.hood.opened) continue;                 // the game opens the hood (by holding the click)
                Vector3 point = v.hood.PointEngine();
                Vector3 dir = point - o;
                float dist = dir.magnitude;
                if (dist > 3.4f || dist < 0.01f) continue;
                float c = Vector3.Dot(f, dir / dist);
                if (c > bestCos) { bestCos = c; best = v; }
            }

            float cosPart;
            Loose part = PartLooked(o, f, out cosPart);
            if (part != null && (best == null || cosPart > bestCos))
            {
                hint = "[" + hotkey + "] " + L.T("Recoger ", "Pick up ") + Parts.NameOf(part.type) + " (" + Parts.Pct(part.condition) + ")";
                if (Input.GetKeyDown(hotkey))
                {
                    if (!PartItems.Active || !PartItems.Give(part.type, part.condition))
                        Storage.pack.Add(new InPack { type = part.type, condition = part.condition });
                    if (part.go != null) UnityEngine.Object.Destroy(part.go);
                    Storage.ground.Remove(part);
                    Storage.Save(Persistence.Slot());
                    Notice(Parts.NameOf(part.type) + (PartItems.Active ? L.T(" guardada en el inventario", " added to your inventory") : L.T(" guardada en la mochila", " added to your pack")));
                }
                return;
            }
            if (best == null) return;

            hint = "[" + hotkey + "] " + L.T("Revisar el motor", "Inspect the engine");
            if (Input.GetKeyDown(hotkey)) { hoodVeh = best; hoodMenu = true; selHood = 0; }
        }

        void CreateHood(Veh v)
        {
            if (!v.isTruck || v.hood != null || v.hoodTried || v.root == null) return;
            v.hoodTried = true;
            VehicleDoor d = v.root.GetComponentInChildren<VehicleDoor>(true);
            Vector3 eye;
            v.hood = Hood.Create(v.root, Front(v.root, d, out eye), v.isCar);
            ApplyHood(v);
        }

        static void ApplyHood(Veh v)
        {
            if (v.hood != null && v.parts != null) v.hood.Apply(v.parts, v.battery);
        }

        bool uiLogged, uiLogged2;

        void DrawHood(float W, float H)
        {
            float e = Ui.Sc;
            if (confirmStore && hoodVeh != null) { DrawGarage(W, H); return; }
            if (hoodMenu && hoodVeh != null)
            {
                Veh v = hoodVeh;
                float k = 1.08f * e;                                       // 1 unit = 1 pixel of the reference screen (panel 640 x 666)
                string mot = v.Reason();
                int nBtn = RowHidden(Parts.Count + 1) ? 2 : 3;
                bool worn2 = mot == null && v.WornOnes() >= 2;
                float rowH = 59.7f * k;
                float yBtn = 579f * k;
                float yEnd = yBtn + nBtn * 39f * k - 8f * k;
                float hPanel = yEnd + 25f * k + 13f * k + 32f * k + (worn2 ? 26f * k : 0f);
                Rect box = new Rect(W * 0.5f - 320f * k, H * 0.5f - hPanel * 0.5f, 640f * k, hPanel);
                string sub = mot == null ? L.T("Todas las piezas están en su lugar. El motor está listo para arrancar.", "All parts are in place. Engine is ready to start.") : L.T("No arranca: ", "Won't start: ") + mot;
                if (!uiLogged) { uiLogged = true; LoggerInstance.Msg("[Ui] drawing the engine screen (first frame)"); }
                Ui.Panel(box, L.T("Motor", "Engine"), sub, mot == null ? Ui.Green : Ui.Red, 7);
                float x0 = box.x + 36f * k, wRow = 568f * k;
                float y = box.y + 125f * k;
                for (int i = 0; i <= Parts.Count; i++)
                {
                    Rect row = new Rect(x0, y, wRow, rowH);
                    y += rowH;
                    string value; string tag = ""; Color cv = Ui.White, ct = Ui.Gray, cb = Ui.Sage; float bar = 0f;
                    float c = i == 0 ? (v.battery ? v.batteryCondition : -1f) : (v.parts != null ? v.parts[i - 1] : -1f);
                    if (c >= 0f)
                    {
                        value = Parts.Pct(c); bar = c;
                        if (c <= Parts.Min) { cv = ct = cb = Ui.Red; tag = L.T("inservible", "useless"); }
                        else if (c < Parts.Worn) { cv = ct = cb = Ui.Amber; tag = L.T("gastada", "worn"); }
                    }
                    else
                    {
                        int have = i == 0 ? BatteriesInInventory() : (PartItems.Active ? PartItems.HowMany(i - 1) : Storage.HowMany(i - 1));
                        value = L.T("FALTA", "MISSING"); cv = Ui.Red; ct = Ui.Gray; tag = L.T("tenés ", "you have ") + have;
                    }
                    Ui.IconRow(row, k, i == 0 ? 0 : i, i == 0 ? L.T("Batería", "Battery") : Parts.NameOf(i - 1), value, tag, cv, ct, i == selHood, bar, cb);
                }
                if (!uiLogged2) { uiLogged2 = true; LoggerInstance.Msg("[Ui] engine screen: rows drawn"); }
                // hairline above the buttons
                Ui.Fill(new Rect(x0, box.y + 560f * k, wRow, Mathf.Max(1f, e)), new Color(40f / 255f, 46f / 255f, 48f / 255f, 1f));
                float by = box.y + yBtn;
                for (int j = Parts.Count + 1; j < optionsHood.Length; j++)
                {
                    if (RowHidden(j)) continue;
                    string label = j == Parts.Count + 1 ? (PartItems.Active ? L.T("Conseguir piezas nuevas (juego de 6)", "Get new parts (set of 6)") : L.T("Soltar la mochila en el suelo", "Drop the pack on the ground"))
                                 : j == Parts.Count + 2 ? L.T("Guardar el vehículo en el garaje", "Store the vehicle in the garage") : L.T("Salir", "Exit");
                    float bw = Mathf.Max(227f * k, Ui.TextWidth(Ui.Upper(label), Ui.FontFit(19f, k), false) + 70f * k);
                    Ui.ButtonPaint(new Rect(box.x + (640f * k - bw) * 0.5f, by, bw, 31f * k), k, label, j == selHood);
                    by += 39f * k;
                }
                string inv = PartItems.Active ? L.T("Piezas del motor en tu inventario: ", "Engine parts in your inventory: ") + PartItems.Total() : L.T("Mochila: ", "Pack: ") + Storage.Summary();
                float iy = box.y + yEnd + 25f * k;
                Ui.Text(new Rect(box.x, iy - 13f * k, 640f * k, 26f * k), inv, Ui.FontFit(17f, k), new Color(0.78f, 0.8f, 0.8f), TextAnchor.MiddleCenter, false);
                if (worn2)
                    Ui.Text(new Rect(box.x, iy + 13f * k, 640f * k, 26f * k), L.T("Hay ", "There are ") + v.WornOnes() + L.T(" piezas gastadas: puede costar arrancar", " worn parts: starting may take a few tries"), Ui.FontFit(17f, k), Ui.Amber, TextAnchor.MiddleCenter, false);
                Ui.KeysBelow(box, L.T("Flechas", "Arrows"), L.T("Mover", "Move"), "Enter", L.T("Instalar / sacar", "Install / remove"), Settings.KeyHood().ToString(), L.T("Cerrar", "Close"));
                return;
            }

            if (garageMenu) { DrawGarage(W, H); return; }
            string line = noticeT > 0f ? notice : hint;
            if (!string.IsNullOrEmpty(line))
            {
                if (noticeT > 0f) Ui.Notice(W, H, line, false);
                else Ui.Hint(W, H * 0.62f, line);
            }
        }

        void DrawMenu(float W, float H)
        {
            float e = Ui.Sc;
            float[] height = { 46f * e, 58f * e, 46f * e, 46f * e, 58f * e, 42f * e };
            float sum = 0f;
            foreach (float a in height) sum += a;
            float hPanel = 82f * e + sum + 56f * e + 14f * e;
            Rect box = new Rect(W * 0.5f - 270f * e, H * 0.5f - hPanel * 0.5f, 540f * e, hPanel);
            string state = current.Broken ? L.T("Averiada: ", "Broken: ") + current.Reason() : current.isRunning ? L.T("Motor encendido", "Engine on") : L.T("Motor apagado", "Engine off");
            if (current.battery) state += "   ·   " + L.T("Batería ", "Battery ") + Parts.Pct(current.batteryCondition);
            Rect u = Ui.Panel(box, L.T("Vehículo", "Vehicle"), state, current.Broken ? Ui.Red : current.isRunning ? Ui.Green : Ui.Accent);
            float y = u.y;
            for (int i = 0; i < options.Length; i++)
            {
                Rect row = new Rect(u.x, y, u.width, height[i]);
                y += height[i];
                string value = ""; Color cv = Ui.Gray; float bar = -1f; Color cb = Ui.Green;
                if (i == 0)
                {
                    if (startsT > 0f) { value = L.T("ARRANCANDO", "STARTING"); cv = Ui.Yellow; }
                    else if (current.Broken) { value = L.T("AVERIADO", "BROKEN"); cv = Ui.Red; }
                    else if (current.isRunning) { value = L.T("ENCENDIDO", "ON"); cv = Ui.Green; }
                    else { value = L.T("APAGADO", "OFF"); cv = Ui.Gray; }
                }
                else if (i == 1)
                {
                    value = Mathf.RoundToInt(current.level * 100f) + " %"; bar = current.level; cb = new Color(0.72f, 0.86f, 1f); cv = cb;
                }
                else if (i == 2)
                {
                    bool on = current.dashboard != null && current.dashboard.lights;
                    value = on ? L.T("ENCENDIDAS", "ON") : L.T("APAGADAS", "OFF"); cv = on ? Ui.Green : Ui.Gray;
                }
                else if (i == 3)
                {
                    bool on = current.dashboard != null && current.dashboard.ac;
                    value = on ? L.T("ENCENDIDO", "ON") : L.T("APAGADO", "OFF"); cv = on ? Ui.Green : Ui.Gray;
                }
                else if (i == 4)
                {
                    float f = current.fuel;
                    cv = cb = f > 0.25f ? Ui.Green : f > 0.1f ? Ui.Yellow : Ui.Red;
                    value = Mathf.RoundToInt(f * DataTank.Capacity) + " L / " + Mathf.RoundToInt(DataTank.Capacity) + " L";
                    bar = f;
                }
                string names = i == 1 ? (current.level > 0.02f ? L.T("Quitar la nieve", "Remove snow") : L.T("Poner la nieve", "Add snow")) : NameOption(i);
                Ui.Row(row, names, value, cv, i == sel, bar, cb);
            }
            Ui.Footer(box, L.T("Flechas", "Arrows"), L.T("Mover", "Move"), "Enter", L.T("Elegir", "Select"), Settings.KeyMenu().ToString(), L.T("Cerrar", "Close"));
        }

        void ToggleSnow()
        {
            if (current == null || root == null) return;
            bool remove = current.level > 0.02f;
            current.level = remove ? 0f : 1f;
            current.hour = Climate.Hours();
            Snow.Apply(root, current.level);
            SavePosition();
            Notice(remove ? L.T("Nieve quitada (vuelve con el tiempo y las nevadas)", "Snow removed (it comes back over time and with snowfall)") : L.T("Nieve puesta", "Snow added"));
            LoggerInstance.Msg("Snow of '" + root.name + "': " + (remove ? "REMOVED" : "added"));
        }

        float snowT;
        // Snow builds up again with in-game hours and, faster, if it is snowing.
        void UpdateSnow(float dt)
        {
            snowT -= dt;
            if (snowT > 0f) return;
            snowT = 5f;
            float h = Climate.Hours();
            if (h < 0f) return;
            float pace = Climate.Pace();
            foreach (Veh v in vehs.Values)
            {
                if (v.root == null) continue;
                if (v.hour < 0f) v.hour = h;
                float dh = h - v.hour;
                v.hour = h;
                if (dh <= 0f || v.level >= 1f) continue;
                if (v == current && driving && Mathf.Abs(vel) > 1f) continue;   // it doesn't build up while moving
                v.level = Mathf.Min(1f, v.level + Mathf.Min(dh, 72f) * pace);
                if (Mathf.Abs(v.level - v.levelSaved) >= 0.04f || v.level >= 1f)
                {
                    Snow.Apply(v.root, v.level);
                    SaveVeh(v);
                }
            }
        }

        float startsT;
        float offT;
        bool startsOk;
        int startsN;

        void ToggleEngine()
        {
            if (current == null || startsT > 0f) return;
            if (current.isRunning)
            {
                current.isRunning = false;
                engine.TurnOff();
                SavePosition();
                offT = 0f;
                Notice(L.T("Motor apagado", "Engine off"));
                return;
            }
            string mot = current.Reason();
            if (mot != null) { Notice(L.T("No arranca: ", "Won't start: ") + mot); return; }
            if (current.WithoutFuel) { Notice(L.T("No arranca: sin combustible", "Won't start: out of fuel")); return; }

            // with several worn parts (below 50 %) it gets harder and harder to start
            int n = current.WornOnes();
            float fail = n >= 2 ? Mathf.Min(0.8f, 0.1f + 0.2f * (n - 1)) : 0f;
            startsOk = UnityEngine.Random.value >= fail;
            startsN = n;
            engine.PlayStarter(root, startsOk);
            startsT = startsOk ? 1.35f : 2.6f;
            menuOpen = false;
        }

        /// <summary>After the starter sound: really starts (or not, if it failed).</summary>
        void UpdateStarter(float dt)
        {
            if (startsT <= 0f) return;
            startsT -= dt;
            if (startsT > 0f) return;
            startsT = 0f;
            if (current == null || root == null) return;
            if (startsOk)
            {
                current.isRunning = true;
                engine.TurnOn(root);
                SavePosition();
                Notice(L.T("Motor encendido", "Engine on"));
            }
            else Notice(L.T("El motor no arranca, probá de nuevo (hay ", "The engine won't start, try again (") + startsN + L.T(" piezas gastadas)", " worn parts)"));
        }

        public override void OnGUI()
        {
            if (driving && current == null) return;
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            try
            {
                float W = Screen.width, H = Screen.height;
                Ui.Start();
                if (!driving) { DrawHood(W, H); return; }

                if (menuOpen) { DrawMenu(W, H); return; }
                if (Radio.menu) { Radio.Draw(W, H); return; }

                if (current.dashboard != null && current.dashboard.inAdjust)
                {
                    Ui.Calibration(W, H, current.dashboard.TextAdjust());
                    return;
                }
                if (dashboardLine.Length > 0) Ui.Hint(W, H * 0.56f, dashboardLine);

                string line = null;
                bool alert = false;
                if (noticeT > 0f) line = notice;
                else if (startsT > 0f) line = L.T("Arrancando...", "Starting...");
                else if (current.Broken) { line = L.T("No arranca: ", "Won't start: ") + current.Reason() + L.T(" (abrí el capó y revisá el motor con ", " (open the hood and check the engine with ") + Settings.KeyHood() + ")"; alert = true; }
                else if (!current.isRunning && offT < 5f) line = L.T("Motor apagado - ", "Engine off - ") + Settings.KeyMenu() + L.T(" abre el menú del vehículo", " opens the vehicle menu");
                if (line != null) Ui.Notice(W, H, line, alert);
            }
            catch (Exception e)
            {
                if (!errorLogged) { errorLogged = true; LoggerInstance.Error("Error drawing the menu: " + e); }
            }
        }

        // ------------------------------------------------------------------ detection

        bool IsSeated(out VehicleDoor door)
        {
            door = null;
            inVeh = GameManager.GetPlayerInVehicle();
            if (inVeh == null) return false;
            if (inVeh.IsEntering() || inVeh.IsTransitioning) enteringT = Time.realtimeSinceStartup;
            if (!inVeh.IsInside() || inVeh.IsEntering() || inVeh.IsTransitioning) return false;
            door = inVeh.m_VehicleDoorUsed;
            return door != null && door.m_CameraDummy != null;
        }

        // Front of the vehicle: perpendicular to the line joining the left and right seats. 'ojo' = driver's seat.
        static Vector3 Front(Transform r, VehicleDoor door, out Vector3 eye)
        {
            Vector3 right = Vector3.zero;
            Transform lft = null, rgt = null;
            foreach (Transform h in r.GetComponentsInChildren<Transform>(true))
            {
                if (h.name == "FrontLeftCameraDummy") lft = h;
                else if (h.name == "FrontRightCameraDummy") rgt = h;
            }
            eye = lft != null ? lft.position : r.position + Vector3.up;
            if (door != null && door.m_CameraDummy != null) eye = door.m_CameraDummy.transform.position;

            if (lft != null && rgt != null)
            {
                right = rgt.position - lft.position;
            }
            else if (door != null && door.m_ExitPoint != null && door.m_CameraDummy != null)
            {
                Vector3 towardOutward = door.m_ExitPoint.transform.position - door.m_CameraDummy.transform.position;
                bool left = door.name.Contains("Left");
                right = left ? -towardOutward : towardOutward;
            }
            right.y = 0f;
            return right.sqrMagnitude > 0.01f ? Vector3.Cross(right.normalized, Vector3.up) : Flat(r.forward);
        }

        // Positions of the two front seats (the steering wheel is in front of one of them).
        static Vector3[] Eyes(Transform r, VehicleDoor door)
        {
            var l = new List<Vector3>();
            if (door != null && door.m_CameraDummy != null) l.Add(door.m_CameraDummy.transform.position);
            foreach (Transform h in r.GetComponentsInChildren<Transform>(true))
            {
                if (h.name != "FrontLeftCameraDummy" && h.name != "FrontRightCameraDummy") continue;
                bool repeated = false;
                foreach (Vector3 q in l) if ((q - h.position).sqrMagnitude < 0.01f) repeated = true;
                if (!repeated) l.Add(h.position);
            }
            if (l.Count == 0) l.Add(r.position + Vector3.up);
            return l.ToArray();
        }

        Veh GetVeh(Transform r)
        {
            int id = r.GetInstanceID();
            Veh v;
            if (vehs.TryGetValue(id, out v) && v.root == r) return v;
            v = new Veh { root = r, orig = r.position };
            Data d = Persistence.Find(Persistence.Slot(), v.orig);
            v.isRunning = d != null ? d.isRunning : Settings.StartOnSit();
            v.steering = d != null ? d.steering : 0f;
            v.level = d != null ? d.level : 1f;
            v.isTruck = IsTruckRoot(r);
            v.isCar = v.isTruck && IsCarRoot(r);
            v.battery = d != null && d.battery;
            v.batteryCondition = d != null ? d.batteryCondition : 1f;
            v.fuel = d != null ? d.fuel : 0.4f;
            if (v.isTruck) v.parts = d != null && d.parts != null ? d.parts : Parts.Initial(v.orig);
            v.levelSaved = v.level;
            v.hour = d != null ? d.hour : -1f;
            vehs[id] = v;
            return v;
        }

        void PrepareBody(Veh v, VehicleDoor door)
        {
            if (v.bodyTried) return;
            v.bodyTried = true;
            try
            {
                Vector3 eye;
                Vector3 fwd = Front(v.root, door, out eye);
                v.body = Body.Create(v.root, Eyes(v.root, door), fwd);
                if (v.body != null) { v.body.car = v.isCar; v.body.Apply(v.steering); }
            }
            catch (Exception e) { LoggerInstance.Warning("Could not prepare the steering wheel/wheels of '" + v.root.name + "': " + e.Message); }
        }

        void Begin(VehicleDoor door)
        {
            // The truck is the "ENCAMPMENT_..." ancestor of the door (otherwise, the direct parent).
            root = RootOf(door);
            dummyCam = door.m_CameraDummy.transform;

            vpCam = GameManager.GetVpFPSCamera();
            vp_FPSPlayer fps = GameManager.GetVpFPSPlayer();
            ctrl = fps != null ? fps.GetComponent<vp_FPSController>() : null;
            playerRoot = ctrl != null ? ctrl.transform.root : null;

            if (root == null || ctrl == null || vpCam == null)
            {
                LoggerInstance.Warning("Could not set up driving (truck=" + (root != null) + ", controller=" + (ctrl != null) + ", camera=" + (vpCam != null) + ").");
                return;
            }

            RestoreOne(root, door);   // in case you sat down before the scan put it back in place

            Vector3 eye;
            Vector3 fwd = Front(root, door, out eye);
            fwdLocal = root.InverseTransformDirection(fwd);

            vel = 0f;
            camApplied = Vector3.zero;
            normalSmooth = Vector3.up;
            velV = 0f; withoutGroundT = 0f; goodT = 0f; hasGood = true; lastGood = root.position; lastRotGood = root.rotation;
            driving = true;
            saveT = 0f;
            menuOpen = false;

            seen.Add(root.GetInstanceID());
            current = GetVeh(root);
            Cfg.CarNow = current.isCar;
            LoggerInstance.Msg("[Options] " + Settings.Describe(current.isCar));
            CaptureObjects(fwd);
            Hierarchy.Dump(root);
            PrepareBody(current, door);
            if (current.isRunning) engine.TurnOn(root);

            int fixedOnes = 0, total = 0;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                total++;
                if (mr.isPartOfStaticBatch) fixedOnes++;
            }
            LoggerInstance.Msg("Driving ENABLED on '" + root.name + "'. Front (world): " + fwd + ". Engine " + (current.isRunning ? "on" : "off") + ". Meshes: " + total + ", statically batched: " + fixedOnes);
            if (fixedOnes > 0)
                LoggerInstance.Warning("There are " + fixedOnes + " statically batched meshes (static batching): those parts would NOT move with the truck. Report it to the author.");
        }

        string dashboardLine = "";
        float accelCurrent;

        void HandleDashboard(float dt)
        {
            dashboardLine = "";
            Veh v = current;
            if (v == null || !v.isTruck || root == null) return;
            v.dashboardT += dt;
            if (v.dashboard == null)
            {
                if (v.dashboardTried || v.dashboardT < 0.4f) return;
                v.dashboardTried = true;
                Camera c0 = GameManager.GetMainCamera();
                if (c0 != null) v.dashboard = Dashboard.Create(v.root, fwdLocal, c0.transform.position, v.isCar);
                if (v.dashboard == null) return;
                v.dashboard.Resume();
            }
            Dashboard t = v.dashboard;
            Camera cam = GameManager.GetMainCamera();
            bool broken = v.Broken;
            t.fuel = v.fuel;
            t.Update(v.isRunning && !broken && !v.WithoutFuel, broken, vel, accelCurrent, Cfg.F(Cfg.VelMaxKmh, Cfg.CarVelMaxKmh), dt);
            if (menuOpen) return;
            if (Radio.menu) { Radio.HandleMenu(); return; }
            if (!t.inAdjust && Input.GetKeyDown(Settings.KeyDashboard())) { t.StartAdjust(); return; }
            if (t.inAdjust)
            {
                string m = t.ProcessAdjust(cam);
                if (m != null) Notice(m);
                return;
            }
            int which;
            string txt = t.Aim(cam, broken, out which);
            // the game's own radio (the one that shows "Radio" when you aim at it): the click opens the mod's radio menu
            if (txt == null && GameRadioLooked())
            {
                if (Input.GetMouseButtonDown(0)) Radio.Open();
                return;
            }
            if (txt == null) return;
            dashboardLine = txt;
            if (Input.GetMouseButtonDown(0))
                Notice(which == Dashboard.Lights ? t.ToggleLights(broken) : which == Dashboard.Trunk ? t.ToggleTrunk() : t.ToggleAc(broken));
        }

        readonly HashSet<string> lookedNames = new HashSet<string>();

        /// <summary>True if the object under the crosshair is the radio of the dashboard (found by the game's own aim: name or label "Radio").</summary>
        bool GameRadioLooked()
        {
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm == null) return false;
                GameObject o = pm.GetInteractiveObjectUnderCrosshairs(3f);
                string h = null;
                try { h = pm.GetInteractiveObjectHoverText(); } catch (Exception) { }
                string chain = "";
                if (o != null)
                {
                    int i = 0;
                    for (Transform tr = o.transform; tr != null && i < 4; tr = tr.parent, i++) chain += (i > 0 ? " < " : "") + tr.name;
                }
                if (chain.Length > 0 && lookedNames.Count < 25 && lookedNames.Add(chain + " | " + h))
                    LoggerInstance.Msg("[Radio] aiming at: '" + chain + "' (text '" + h + "')");
                if (chain.ToLowerInvariant().Contains("radio")) return true;
                return o != null && !string.IsNullOrEmpty(h) && h.Trim().ToLowerInvariant() == "radio";
            }
            catch (Exception) { return false; }
        }

        // ------------------------------------------------------------------ fuel

        /// <summary>Burns fuel while the engine is running: little at idle, more with speed.</summary>
        void Consume(float dt, bool on)
        {
            if (current == null || !current.isTruck || !Settings.UseFuel()) return;
            if (!current.isRunning) return;
            if (!on && current.WithoutFuel)
            {
                current.isRunning = false;
                engine.TurnOff();
                Notice(L.T("Se acabó el combustible", "Out of fuel"));
                SavePosition();
                return;
            }
            if (!on) return;
            // consumption by distance (configurable km per liter) plus a little at idle (0.2 mL per second)
            float liters = Mathf.Abs(vel) * dt / (Settings.KmPerLiter(current.isCar) * 1000f) + 0.0002f * dt;
            current.fuel = Mathf.Max(0f, current.fuel - liters / DataTank.Capacity);
            if (current.fuel < 0.1f && !current.noticeLow) { current.noticeLow = true; Notice(L.T("Combustible bajo", "Low fuel")); }
            if (current.fuel >= 0.12f) current.noticeLow = false;
            if (current.fuel <= 0.001f)
            {
                current.fuel = 0f;
                current.isRunning = false;
                engine.TurnOff();
                Notice(L.T("Se acabó el combustible", "Out of fuel"));
                SavePosition();
            }
        }

        readonly SoundLoad soundLoad = new SoundLoad();
        float scanningPumpT;
        Veh filling;
        readonly List<Jerry.Can> cans = new List<Jerry.Can>();
        float jerryScanT;

        void CutLoad()
        {
            soundLoad.Play(false, Vector3.zero);
            if (filling != null) { SaveVeh(filling); filling = null; }
        }

        /// <summary>Truck next to a pump + open fuel cap + looking at the pump = hold click to fill up.</summary>
        void HandleFuel(bool seated)
        {
            if (seated || job != null || !Settings.UseFuel()) { CutLoad(); return; }
            Camera cam = GameManager.GetMainCamera();
            if (cam == null) { CutLoad(); return; }
            Vector3 o = cam.transform.position, f = cam.transform.forward;

            bool hasTruck = false;
            foreach (Veh t in vehs.Values)
                if (t.isTruck && t.root != null && (t.root.position - o).sqrMagnitude < 400f) { hasTruck = true; break; }
            // jerry cans in the inventory (checked twice a second)
            jerryScanT -= Time.unscaledDeltaTime;
            if (jerryScanT <= 0f) { jerryScanT = 0.5f; Jerry.Find(cans); }
            bool hasCan = cans.Count > 0;

            if (!hasTruck && !(hasCan && Settings.JerryAtPumps())) { Pumps.near.Clear(); CutLoad(); return; }

            scanningPumpT -= Time.unscaledDeltaTime;
            if (scanningPumpT <= 0f)
            {
                scanningPumpT = 0.5f * Mathf.Min(Settings.ScanScale(), 3f);
                bool capOpenState = false;
                foreach (Veh t in vehs.Values)
                {
                    if (!t.isTruck || t.root == null || (t.root.position - o).sqrMagnitude > 400f) continue;
                    if (!t.tankTried) { t.tankTried = true; t.tank = Tank.Create(t.root); }
                    if (t.tank != null) { t.tank.Update(); if (t.tank.open) capOpenState = true; }
                }
                Pumps.Scan(o, 10f, capOpenState);
            }
            else
            {
                foreach (Veh t in vehs.Values) if (t.tank != null && t.root != null) t.tank.Update();
            }

            if (hasTruck && hasCan && PourFromCan(o, f)) return;

            Pumps.Pump crosshair = null;
            float bestCos = 0.8f;
            foreach (Pumps.Pump b in Pumps.near)
            {
                Vector3 dir = b.center - o;
                float d = dir.magnitude;
                if (d > 5.5f || d < 0.01f) continue;
                float c = Vector3.Dot(f, dir / d);
                if (c > bestCos) { bestCos = c; crosshair = b; }
            }
            if (crosshair == null) { CutLoad(); return; }

            Veh veh = null;
            float bestD = 1e9f;
            foreach (Veh t in vehs.Values)
            {
                if (!t.isTruck || t.root == null || t.tank == null) continue;
                Vector3 p = t.tank.Point() - crosshair.center;
                p.y = 0f;
                float dd = p.magnitude;
                if (dd < bestD) { bestD = dd; veh = t; }
            }
            // the truck is not ready to be filled at the pump (far away, cap closed or full): a jerry can can be filled instead
            bool truckReady = veh != null && bestD <= 5f && veh.tank.open && veh.fuel < 0.999f;
            if (!truckReady && hasCan && FillCan(crosshair)) return;
            if (veh == null) { CutLoad(); return; }
            if (bestD > 5f) { hint = L.T("Acercá la camioneta a la bomba (con el tapón del tanque de su lado)", "Park the truck next to the pump (fuel cap side facing it)"); CutLoad(); return; }
            if (!veh.tank.open) { hint = L.T("Abrí el tapón del tanque de la camioneta (mantené el clic sobre la tapa)", "Open the truck's fuel cap (hold click on the cover)"); CutLoad(); return; }

            if (veh.fuel >= 0.999f) { hint = L.T("Tanque lleno (", "Tank full (") + Mathf.RoundToInt(DataTank.Capacity) + " L)"; CutLoad(); return; }

            hint = L.T("[Mantené el clic] Cargar combustible: ", "[Hold click] Fill up: ") + Mathf.RoundToInt(veh.fuel * 100f) + " %  (" + Mathf.RoundToInt(veh.fuel * DataTank.Capacity) + " L)";
            if (Input.GetMouseButton(0))
            {
                filling = veh;
                veh.fuel = Mathf.Min(1f, veh.fuel + DataTank.Airflow * Time.deltaTime / DataTank.Capacity);
                veh.noticeLow = false;
                soundLoad.Play(true, crosshair.center);
                if (veh.fuel >= 0.999f) { veh.fuel = 1f; Notice(L.T("Tanque lleno", "Tank full")); CutLoad(); }
            }
            else CutLoad();
        }

        /// <summary>Looking at the open fuel cap of a truck with a jerry can in the inventory: hold click to pour it in (the can keeps 1 %).</summary>
        bool PourFromCan(Vector3 o, Vector3 f)
        {
            try
            {
                LiquidItem li = Jerry.FullestPourable(cans);
                if (li == null) return false;
                Veh best = null;
                float bestC = 0.9f;
                foreach (Veh t in vehs.Values)
                {
                    if (!t.isTruck || t.root == null || t.tank == null || !t.tank.open) continue;
                    Vector3 d = t.tank.Point() - o;
                    float dist = d.magnitude;
                    if (dist > 3.2f || dist < 0.05f) continue;
                    float c = Vector3.Dot(f, d / dist);
                    if (c > bestC) { bestC = c; best = t; }
                }
                if (best == null) return false;
                if (best.fuel >= 0.999f) { hint = L.T("Tanque lleno (", "Tank full (") + Mathf.RoundToInt(DataTank.Capacity) + " L)"; CutLoad(); return true; }

                float have = Jerry.Available(li);
                hint = L.T("[Mantené el clic] Verter el bidón en el tanque: ", "[Hold click] Pour the jerry can into the tank: ") + have.ToString("0.0") + " L";
                if (Input.GetMouseButton(0))
                {
                    float liters = Mathf.Min(DataTank.Airflow * Time.deltaTime, have, (1f - best.fuel) * DataTank.Capacity);
                    filling = best;
                    best.fuel = Mathf.Min(1f, best.fuel + liters / DataTank.Capacity);
                    best.noticeLow = false;
                    Jerry.Set(li, Jerry.Amount(li) - liters);
                    soundLoad.Play(true, best.tank.Point());
                    if (best.fuel >= 0.999f) { best.fuel = 1f; Notice(L.T("Tanque lleno", "Tank full")); CutLoad(); }
                    else if (Jerry.Available(li) <= 0.01f) { Notice(L.T("El bidón quedó casi vacío (1 %)", "The jerry can is almost empty (1 %)")); CutLoad(); }
                }
                else CutLoad();
                return true;
            }
            catch (Exception e) { if (Cfg.DebugLog) LoggerInstance.Warning("[Jerry] pour: " + e.Message); return false; }
        }

        /// <summary>Looking at a pump with a jerry can in the inventory: hold click to fill it.</summary>
        bool FillCan(Pumps.Pump pump)
        {
            try
            {
                if (!Settings.JerryAtPumps()) return false;
                LiquidItem li = Jerry.EmptiestFillable(cans);
                if (li == null) return false;
                float cap = Jerry.Capacity(li), vol = Jerry.Amount(li);
                hint = L.T("[Mantené el clic] Llenar el bidón: ", "[Hold click] Fill the jerry can: ") + vol.ToString("0.0") + " L / " + cap.ToString("0.0") + " L";
                if (Input.GetMouseButton(0))
                {
                    Jerry.Set(li, Mathf.Min(cap, vol + DataTank.Airflow * Time.deltaTime));
                    soundLoad.Play(true, pump.center);
                    if (Jerry.Amount(li) >= cap - 0.01f) { Jerry.Set(li, cap); Notice(L.T("Bidón lleno", "Jerry can full")); CutLoad(); }
                }
                else CutLoad();
                return true;
            }
            catch (Exception e) { if (Cfg.DebugLog) LoggerInstance.Warning("[Jerry] fill: " + e.Message); return false; }
        }

        void Stop(string reason)
        {
            if (current != null && current.dashboard != null) current.dashboard.Pause();
            startsT = 0f;
            offT = 0f;
            engine.CutStarter();
            RestoreAc();
            if (driving && Cfg.DebugLog) LoggerInstance.Msg("Driving disabled (" + reason + ").");
            if (driving && root != null) SavePosition();
            camApplied = Vector3.zero;
            driving = false;
            menuOpen = false;
            vel = 0f;
            DropObjects();
            engine.TurnOff();
            root = null;
            current = null;
            Cfg.CarNow = false;
        }

        // ------------------------------------------------------------------ saving the state

        static Transform RootOf(VehicleDoor door)
        {
            Transform t = door.transform;
            while (t != null)
            {
                if (t.name.StartsWith("ENCAMPMENT")) return t;
                t = t.parent;
            }
            // Some vehicles (for example the one at Breyerhouse, Mystery Lake) are not under an "ENCAMPMENT_..." object:
            // use the closest ancestor that looks like a truck, otherwise the direct parent.
            t = door.transform.parent;
            while (t != null)
            {
                if (NameLooksLikeTruck(t.name)) return t;
                t = t.parent;
            }
            return door.transform.parent;
        }

        static bool NameLooksLikeTruck(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return NameLooksLikeTruckOnly(n) || NameLooksLikeCar(n);
        }

        static bool NameLooksLikeTruckOnly(string lowerName)
        {
            return lowerName.Contains("cartruck") || lowerName.Contains("truck") || lowerName.Contains("pickup");
        }

        // Vehicles that must never get the engine system.
        static readonly string[] notCars = { "tractor", "trailer", "snowmobile", "cart", "carcass", "carry", "carrot", "carbon", "carpet", "cargo", "carton", "card", "care", "cartridge" };

        /// <summary>Does the name look like a passenger car (sedan, coupe, wagon, SUV, "Car...")? Cars use the same engine system as the truck.</summary>
        static bool NameLooksLikeCar(string lowerName)
        {
            if (string.IsNullOrEmpty(lowerName)) return false;
            string n = lowerName.ToLowerInvariant();
            if (NameLooksLikeTruckOnly(n)) return false;
            foreach (string bad in notCars) if (n.Contains(bad)) return false;
            return n.Contains("sedan") || n.Contains("coupe") || n.Contains("wagon") || n.Contains("suv") || n.Contains("hatch") || n.Contains("car");
        }

        // 0 = not a motorized vehicle, 1 = truck, 2 = car
        static readonly Dictionary<int, int> kindCache = new Dictionary<int, int>();
        static readonly HashSet<string> rejectedLogged = new HashSet<string>();

        static int KindOf(Transform r)
        {
            if (r == null) return 0;
            int id = r.GetInstanceID();
            int kind;
            if (kindCache.TryGetValue(id, out kind)) return kind;
            string rn = r.name.ToLowerInvariant();
            if (NameLooksLikeTruckOnly(rn)) kind = 1;
            else if (NameLooksLikeCar(rn)) kind = 2;
            else
            {
                bool hood = false, fuel = false;
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("hoodhinge")) hood = true;
                    else if (n.Contains("fuelcoverhinge")) fuel = true;
                    if (hood && fuel) break;
                }
                bool excluded = false;
                foreach (string bad in notCars) if (rn.Contains(bad)) { excluded = true; break; }
                if (hood && fuel) kind = 1;                                   // Breyerhouse truck (any name)
                else if (hood && !excluded && r.GetComponentInChildren<VehicleDoor>(true) != null) kind = 2;   // a car with only a hood hinge
                else kind = 0;
            }
            kindCache[id] = kind;
            if (kind == 0 && rejectedLogged.Add(r.name))
                MelonLogger.Msg("Vehicle '" + r.name + "' is not treated as a truck or car (enable 'Drive other vehicles' in Mod Options to try it anyway). If this is a car or truck, report this name.");
            else if (kind != 0 && rejectedLogged.Add("ok:" + r.name))
                MelonLogger.Msg("Vehicle '" + r.name + "' detected as a " + (kind == 1 ? "truck" : "car") + ".");
            return kind;
        }

        /// <summary>Is this a motorized vehicle (truck or car)?</summary>
        static bool IsTruckRoot(Transform r) { return KindOf(r) != 0; }
        static bool IsCarRoot(Transform r) { return KindOf(r) == 2; }

        void SavePosition()
        {
            if (root == null || current == null) return;
            SaveVeh(current);
        }

        void SaveVeh(Veh v)
        {
            if (v == null || v.root == null) return;
            v.levelSaved = v.level;
            if (v.gid != null) { SaveGarageVehicle(v); return; }
            Persistence.Save(Persistence.Slot(), new Data
            {
                orig = v.orig,
                pos = v.root.position,
                rot = v.root.rotation,
                steering = v.steering,
                isRunning = v.isRunning,
                level = v.level,
                hour = v.hour,
                battery = v.battery,
                batteryCondition = v.batteryCondition,
                parts = v.parts,
                fuel = v.fuel
            });
        }

        // Every time a vehicle appears in the world (when loading the game or approaching), it puts it back where you left it,
        // with the snow, steering wheel and wheels as you left them.
        void RestoreVehicles()
        {
            List<VehicleDoor> doors = FindDoors();
            if (!scanLogged && (doors.Count > 0 || Time.realtimeSinceStartup - lastLoad > 20f))
            {
                scanLogged = true;
                LoggerInstance.Msg("[Vehicles] scan in scene '" + Scene() + "': " + doors.Count + " vehicle doors found.");
            }
            foreach (VehicleDoor d in doors)
            {
                if (d == null) continue;
                Transform r = RootOf(d);
                if (r != null) RestoreOne(r, d);
            }
        }

        bool scanLogged;

        static bool doorsReported;

        /// <summary>
        /// All the vehicle doors in the loaded scenes. FindObjectsOfType is used first; mods that filter it (e.g. SeamlessInteriors) can make it
        /// return nothing, so FindObjectsOfTypeAll (only objects that live in a loaded scene, not prefabs) is added as a second source.
        /// </summary>
        static List<VehicleDoor> FindDoors()
        {
            var res = new List<VehicleDoor>();
            var ids = new HashSet<int>();
            int normal = 0;
            try
            {
                foreach (VehicleDoor d in UnityEngine.Object.FindObjectsOfType<VehicleDoor>())
                    if (d != null && ids.Add(d.GetInstanceID())) { res.Add(d); normal++; }
            }
            catch (Exception) { }
            try
            {
                foreach (VehicleDoor d in Resources.FindObjectsOfTypeAll<VehicleDoor>())
                {
                    if (d == null || d.gameObject == null) continue;
                    UnityEngine.SceneManagement.Scene sc = d.gameObject.scene;
                    if (!sc.IsValid() || !sc.isLoaded) continue;           // prefabs and assets are left out
                    if (!d.gameObject.activeInHierarchy) continue;
                    if (ids.Add(d.GetInstanceID())) res.Add(d);
                }
            }
            catch (Exception) { }
            if (res.Count > normal && !doorsReported)
            {
                doorsReported = true;
                MelonLoader.MelonLogger.Msg("[Vehicles] FindObjectsOfType found " + normal + " vehicle doors, the fallback search found " + (res.Count - normal) + " more (another mod is probably filtering the search).");
            }
            return res;
        }

        bool RestoreOne(Transform r, VehicleDoor d)
        {
            if (!seen.Add(r.GetInstanceID())) return false;

            Data dat0 = Persistence.Find(Persistence.Slot(), r.position);
            if (dat0 != null && dat0.stored) { r.gameObject.SetActive(false); return true; }   // it is in the garage
            if (IsTruckRoot(r)) Garage.CacheTemplate(r);     // pristine copy, to be able to build it in other regions

            Veh v = GetVeh(r);
            CreateHood(v);
            Data dat = Persistence.Find(Persistence.Slot(), v.orig);
            if (dat == null) return true;

            r.SetPositionAndRotation(dat.pos, dat.rot);
            if (v.level < 0.999f) Snow.Apply(r, v.level);
            if (Mathf.Abs(dat.steering) > 0.5f) PrepareBody(v, d);   // so the wheels stay turned even if nobody sits down
            if (Cfg.DebugLog) LoggerInstance.Msg("Vehicle '" + r.name + "' put back where you left it: " + dat.pos + ", steering wheel " + dat.steering.ToString("0") + "°");

            return true;
        }

        // ---- loading a saved game while inside a vehicle
        // The game seats you "silently" in a vehicle on load, but doesn't know we moved it and the camera ends up wrong.
        // You are taken out with the game's normal exit and left next to the door.
        class Job
        {
            public VehicleDoor door;
            public Transform root;
            public float t;
            public bool exited;
            public float wait;
        }
        Job job;
        float lastLoad = -100f;
        float enteringT = -100f;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            lastLoad = Time.realtimeSinceStartup;
            scanLogged = false;
            PartItems.ResetExamine();
            live.Clear();
            Trunk.Reset();
            Radio.Reset();
            confirmStore = false;
            garageMenu = false;
        }

        void StartExit(VehicleDoor door)
        {
            Transform r = RootOf(door);
            if (r == null) { Begin(door); return; }
            RestoreOne(r, door);      // so the vehicle is already where you left it
            job = new Job { door = door, root = r };
            LoggerInstance.Msg("The game seated you in '" + r.name + "' on load: leaving you next to the door.");
        }

        void ProcessJob()
        {
            if (job == null) return;
            job.t += Time.unscaledDeltaTime;
            if (job.door == null || job.root == null) { job = null; return; }
            if (job.t < 0.7f) return;                                      // let the game finish seating you

            PlayerInVehicle pv = GameManager.GetPlayerInVehicle();
            bool inside = pv != null && pv.IsInside();
            bool busy = pv != null && (pv.IsEntering() || pv.IsTransitioning);

            if (inside && !busy && !job.exited)
            {
                job.exited = true;
                job.wait = 0f;
                try { pv.ExitVehicle(job.door); LoggerInstance.Msg("Taking the player out of the vehicle (door '" + job.door.name + "')."); }
                catch (Exception e) { LoggerInstance.Warning("Could not take the player out using the game: " + e.Message); }
                return;
            }

            if (job.exited)
            {
                job.wait += Time.unscaledDeltaTime;
                if ((inside || busy) && job.wait < 6f) return;        // wait for the game's exit to finish
            }

            vp_FPSPlayer fps = GameManager.GetVpFPSPlayer();
            vp_FPSController c = fps != null ? fps.GetComponent<vp_FPSController>() : null;
            if (c != null)
            {
                Vector3 dest = job.door.m_ExitPoint != null ? job.door.m_ExitPoint.transform.position : job.door.transform.position + Vector3.up;
                c.SetPosition(dest);
                LoggerInstance.Msg("Player left next to '" + job.root.name + "': " + dest + (inside ? " (the game still had them inside)" : ""));
            }
            job = null;
        }

        // ------------------------------------------------------------------ loose objects (things inside and around)

        // Finds the objects in the world (dropped inventory items, boxes) that are on or inside the vehicle.
        void CaptureObjects(Vector3 fwd)
        {
            carried.Clear();
            bodiesFixed.Clear();
            Vector3 rgt = Vector3.Cross(Vector3.up, fwd);
            float middleLength = Cfg.F(Cfg.Length, Cfg.CarLength) * 0.5f + Cfg.MarginObjects.Value;
            float middleWidth = Cfg.F(Cfg.Width, Cfg.CarWidth) * 0.5f + Cfg.MarginObjects.Value;

            foreach (GearItem g in UnityEngine.Object.FindObjectsOfType<GearItem>())
                if (g != null) TryCarry(g.transform, fwd, rgt, middleLength, middleWidth);
            foreach (Container c in UnityEngine.Object.FindObjectsOfType<Container>())
                if (c != null) TryCarry(c.transform, fwd, rgt, middleLength, middleWidth);

            if (Cfg.DebugLog) LoggerInstance.Msg("Objects traveling with the truck: " + carried.Count);
        }

        void TryCarry(Transform t, Vector3 fwd, Vector3 rgt, float middleLength, float middleWidth)
        {
            if (t.IsChildOf(root)) return;                                   // the children already move on their own
            if (playerRoot != null && t.IsChildOf(playerRoot)) return;
            if (!t.gameObject.activeInHierarchy) return;

            Vector3 d = t.position - root.position;
            if (Mathf.Abs(Vector3.Dot(d, fwd)) > middleLength) return;
            if (Mathf.Abs(Vector3.Dot(d, rgt)) > middleWidth) return;
            if (d.y < -0.7f || d.y > 3.5f) return;

            carried.Add(t);
            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true;     // so physics doesn't fight it while it travels
                bodiesFixed.Add(rb);
            }
        }

        void DropObjects()
        {
            foreach (Rigidbody rb in bodiesFixed)
                if (rb != null) rb.isKinematic = false;
            bodiesFixed.Clear();
            carried.Clear();
        }

        // ------------------------------------------------------------------ driving

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        Vector3 camApplied;
        Vector3 OffsetWorld(Vector3 o)
        {
            Vector3 rgt = Vector3.Cross(Vector3.up, fwdLocal);
            return root.TransformDirection(rgt * o.x + Vector3.up * o.y + fwdLocal * o.z);
        }

        void Drive(float dt)
        {
            if (root == null || current == null || dt <= 0f) return;

            bool withoutBat = current.Broken;
            bool on = current.isRunning && !withoutBat && !current.WithoutFuel;
            float vmax = Cfg.F(Cfg.VelMaxKmh, Cfg.CarVelMaxKmh) / 3.6f;
            float vback = Cfg.F(Cfg.VelBackKmh, Cfg.CarVelBackKmh) / 3.6f;
            bool blocksEntry = menuOpen || Radio.menu || (current.dashboard != null && current.dashboard.inAdjust);
            bool accel = !blocksEntry && on && Input.GetKey(Cfg.Key(Cfg.TAccelerate, KeyCode.W));
            bool brakes = !blocksEntry && Input.GetKey(Cfg.Key(Cfg.TBrake, KeyCode.S));
            bool hand = !blocksEntry && Input.GetKey(Cfg.Key(Cfg.TBrakeHand, KeyCode.Space));
            float turn = blocksEntry ? 0f : (Input.GetKey(Cfg.Key(Cfg.TRight, KeyCode.D)) ? 1f : 0f) - (Input.GetKey(Cfg.Key(Cfg.TLeft, KeyCode.A)) ? 1f : 0f);

            // ---- speed (progressive acceleration, braking, reversing, inertia). With the engine off there is only inertia and brakes.
            if (accel && !brakes)
            {
                vel += (vel < 0f ? Cfg.F(Cfg.Braking, Cfg.CarBraking) : Cfg.F(Cfg.Acceleration, Cfg.CarAcceleration)) * dt;
            }
            else if (brakes && !accel)
            {
                if (vel > 0.3f) vel -= Cfg.F(Cfg.Braking, Cfg.CarBraking) * dt;
                else if (on) vel -= Cfg.F(Cfg.Acceleration, Cfg.CarAcceleration) * 0.6f * dt;
                else vel = Mathf.MoveTowards(vel, 0f, Cfg.F(Cfg.Braking, Cfg.CarBraking) * dt);
            }
            else
            {
                vel = Mathf.MoveTowards(vel, 0f, Cfg.F(Cfg.Drag, Cfg.CarDrag) * (on ? 1f : 2.5f) * dt);
            }
            if (hand) vel = Mathf.MoveTowards(vel, 0f, Cfg.F(Cfg.BrakeHand, Cfg.CarBrakeHand) * dt);
            vel = Mathf.Clamp(vel, -vback, vmax);
            if (withoutBat) vel = 0f;                      // without battery it doesn't move
            Consume(dt, on);
            DrainBattery(dt);
            WearParts(dt, on);

            // ---- steering wheel: follows the A/D keys. With the engine on and the vehicle moving it returns to center on its own;
            //      with the engine off it stays where you left it.
            float limit = Mathf.Max(Cfg.SteeringDegrees.Value, 1f);
            float pace = limit / 0.7f * Settings.VelSteering(current.isCar);
            if (turn != 0f)
                current.steering = Mathf.MoveTowards(current.steering, turn * limit, pace * dt);
            else if (on)
                current.steering = Mathf.MoveTowards(current.steering, 0f, pace * 0.8f * Mathf.Max(0.35f, Mathf.Clamp01(Mathf.Abs(vel) / 5f)) * dt);
            current.steering = Mathf.Clamp(current.steering, -limit, limit);
            if (current.body != null) current.body.Apply(current.steering);
            float dirVol = current.steering / limit;

            // ---- steering: turns more the faster it goes, and little at low speed; reversed when backing up
            float factor = Mathf.Clamp01(Mathf.Abs(vel) / 3f) * Mathf.Sign(vel);
            float smoothing = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(Mathf.Abs(vel) / Mathf.Max(vmax, 1f)));
            float dy = dirVol * Cfg.F(Cfg.TurnMax, Cfg.CarTurnMax) * factor * smoothing * dt;

            accelCurrent = accel ? 1f : 0f;
            if (on) engine.Update(Mathf.Clamp01(Mathf.Abs(vel) / Mathf.Max(vmax, 1f)), accel ? 1f : 0f, dt);

            Vector3 pos = root.position;
            Vector3 fwd = Flat(root.TransformDirection(fwdLocal));
            Vector3 half = Quaternion.AngleAxis(dy * 0.5f, Vector3.up) * fwd;
            Vector3 step = half * (vel * dt);

            // ---- collision: a box projected toward where it is heading
            if (Mathf.Abs(vel) > 0.01f)
            {
                Vector3 dir = vel > 0f ? half : -half;
                if (Blocked(pos, fwd, dir, Mathf.Abs(vel * dt)))
                {
                    if (Cfg.DebugLog && Mathf.Abs(vel) > 3f) LoggerInstance.Msg("Collision at " + (vel * 3.6f).ToString("0") + " km/h.");
                    vel = -vel * 0.1f;
                    step = Vector3.zero;
                    dy = 0f;
                }
            }

            Vector3 newPos = pos + step;
            Vector3 newFwd = Quaternion.AngleAxis(dy, Vector3.up) * fwd;
            Quaternion rot = Quaternion.AngleAxis(dy, Vector3.up) * root.rotation;

            // ---- terrain: 4 points (like wheels) to adjust height and tilt
            float L = Cfg.F(Cfg.Length, Cfg.CarLength) * 0.4f, A = Cfg.F(Cfg.Width, Cfg.CarWidth) * 0.5f;
            Vector3 rgt = Vector3.Cross(Vector3.up, newFwd);
            Vector3 pFD, pFI, pTD, pTI;
            bool ok = Ground(newPos + newFwd * L + rgt * A, pos.y, out pFD)
                    & Ground(newPos + newFwd * L - rgt * A, pos.y, out pFI)
                    & Ground(newPos - newFwd * L + rgt * A, pos.y, out pTD)
                    & Ground(newPos - newFwd * L - rgt * A, pos.y, out pTI);
            if (ok)
            {
                withoutGroundT = 0f;
                float h = (pFD.y + pFI.y + pTD.y + pTI.y) * 0.25f;
                Vector3 n = Vector3.Cross(pFI - pTD, pFD - pTI);
                if (n.y < 0f) n = -n;
                n.Normalize();
                normalSmooth = Vector3.Slerp(normalSmooth, n, 1f - Mathf.Exp(-8f * dt));
                Quaternion tilt = Quaternion.FromToRotation(rot * Vector3.up, normalSmooth);
                rot = tilt * rot;
                float diff = h - newPos.y;
                if (diff > 1.5f) { newPos.y = h; velV = 0f; }                                   // ground well above: climb onto it
                else if (diff >= -0.35f)
                {
                    newPos.y = Mathf.Lerp(newPos.y, h, 1f - Mathf.Exp(-15f * dt));        // on the ground: follow it
                    velV = 0f;
                    goodT += dt;
                    if (goodT > 1f) { goodT = 0f; lastGood = newPos; lastRotGood = rot; hasGood = true; }
                }
                else
                {
                    // ground clearly below (cliff, ramp): gravity, until it touches down
                    velV = Mathf.Max(velV - 20f * dt, -60f);
                    newPos.y += velV * dt;
                    if (newPos.y <= h) { newPos.y = h; velV = 0f; }
                }
            }
            else
            {
                // no ground found (even far below): it falls; if it goes on for a few seconds, the truck is put back at its last safe spot
                withoutGroundT += dt;
                if (withoutGroundT > 0.5f)
                {
                    velV = Mathf.Max(velV - 20f * dt, -60f);
                    newPos.y += velV * dt;
                }
                if (withoutGroundT > 4f && hasGood)
                {
                    newPos = lastGood;
                    rot = lastRotGood;
                    vel = 0f; velV = 0f; withoutGroundT = 0f;
                    Notice(global::WorkingVehicles.L.T("Vehículo recuperado: había caído del mapa", "Vehicle recovered: it had fallen through the map"));
                }
            }

            // ---- apply and drag the player along with the truck
            Vector3 before = dummyCam.position;
            Vector3 dummyFwdBefore = Flat(dummyCam.forward);
            Vector3 posOld = root.position;
            Quaternion rotOld = root.rotation;
            root.SetPositionAndRotation(newPos, rot);

            if (carried.Count > 0)
            {
                Quaternion dq = rot * Quaternion.Inverse(rotOld);
                foreach (Transform o in carried)
                {
                    if (o == null) continue;
                    o.SetPositionAndRotation(newPos + dq * (o.position - posOld), dq * o.rotation);
                }
            }

            saveT += dt;
            if (saveT > 10f) { saveT = 0f; SavePosition(); }
            Vector3 delta = dummyCam.position - before;

            // Camera adjustment inside the truck (Mod Options): the player is offset from the seat and turns with the truck.
            Vector3 offM = current.isTruck ? OffsetWorld(Settings.Cam(current.isCar)) : Vector3.zero;
            Vector3 move = delta + (offM - camApplied);
            camApplied = offM;
            if (move.sqrMagnitude > 0f)
                ctrl.SetPosition(ctrl.transform.position + move);

            // REAL rotation of the seat (including what the terrain tilt adds), so the view doesn't drift.
            float dyView = Vector3.SignedAngle(dummyFwdBefore, Flat(dummyCam.forward), Vector3.up);
            if (Mathf.Abs(dyView) > 0.0001f || Mathf.Abs(dy) > 0.0001f)
            {
                vpCam.AddToYaw(Cfg.InvertTurnView.Value ? -dyView : dyView);
                if (Cfg.CorrectLimitView.Value && inVeh != null)
                    vpCam.SetYawLimit(dummyCam.rotation, inVeh.m_YawLimitDegrees);
            }

            if (Cfg.DebugLog)
            {
                logT += dt;
                if (logT > 2f && Mathf.Abs(vel) > 0.2f)
                {
                    logT = 0f;
                    LoggerInstance.Msg("vel " + (vel * 3.6f).ToString("0") + " km/h | pos " + newPos);
                }
            }
        }

        // ------------------------------------------------------------------ physics

        bool Own(Collider c)
        {
            Transform t = c.transform;
            if (root != null && t.IsChildOf(root)) return true;
            if (playerRoot != null && t.IsChildOf(playerRoot)) return true;
            // Loose things (dropped inventory, boxes) don't stop the truck: it carries them or ignores them.
            if (c.GetComponentInParent<GearItem>() != null) return true;
            if (c.GetComponentInParent<Container>() != null) return true;
            return false;
        }

        bool Blocked(Vector3 pos, Vector3 fwd, Vector3 dir, float dist)
        {
            float height = Cfg.F(Cfg.Height, Cfg.CarHeight);
            Vector3 center = pos + Vector3.up * (0.45f + height * 0.5f);
            Vector3 half = new Vector3(Cfg.F(Cfg.Width, Cfg.CarWidth) * 0.5f, height * 0.5f, Cfg.F(Cfg.Length, Cfg.CarLength) * 0.5f);
            Quaternion orient = Quaternion.LookRotation(fwd, Vector3.up);

            RaycastHit[] hits = Physics.BoxCastAll(center, half, dir, orient, dist + 0.05f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (h.collider == null || Own(h.collider)) continue;
                if (h.distance <= 0f) continue;                          // was already touching (for example the snow under the wheels)
                if (h.normal.y >= Cfg.NormalGround.Value) continue;       // it is the ground / a slope that can be climbed
                if (h.point.y < pos.y + 0.4f) continue;                  // low edges (snow, curbs)
                return true;
            }
            return false;
        }

        bool Ground(Vector3 xz, float yRef, out Vector3 point)
        {
            point = Vector3.zero;
            // first nearby; if nothing is found (cliff, hole) look much farther down
            for (int pass = 0; pass < 2; pass++)
            {
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(xz.x, yRef + 2.5f, xz.z), Vector3.down, pass == 0 ? 8f : 400f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                bool exists = false;
                foreach (RaycastHit h in hits)
                {
                    if (h.collider == null || Own(h.collider)) continue;
                    if (h.distance < best) { best = h.distance; point = h.point; exists = true; }
                }
                if (exists) return true;
            }
            return false;
        }
    }
}
