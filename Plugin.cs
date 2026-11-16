using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Numerics;
using System.Text.Json;

namespace RutaEorzea;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui Gui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Lifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    private readonly Configuration config;
    private readonly ICallGateSubscriber<bool> navReady;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> pathfind;
    private readonly ICallGateSubscriber<Vector3?> flagFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> nearestFloor;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;
    private ZoneData? zoneData;
    private ZoneExit? transitExit;
    private Vector3? snappedExit;
    private Vector3 pendingGoal;
    private bool waitingForTransition;
    private bool exitSearchAttempted;
    private bool travelOnly;
    private bool open;
    private bool disposed;
    private string status = "Marca la casilla a la derecha de una misión para seguir su ruta.";
    private string minimapStatus = "";
    private Destination? target;
    private List<Destination> candidates = [];
    private List<Vector3> path = [];
    private RouteProgress progress;
    private MinimapSnapshot? minimap;
    private CancellationTokenSource? cancellation;
    private Task<List<Vector3>>? pending;
    private int generation;
    private int pendingGeneration;
    private int frame;
    private int readQuestFrame;
    private bool mapWasVisible;
    private (uint Quest, uint Map) lastQuest;
    private Destination? lastFlag;
    private uint lastTerritory;
    private uint lastMap;
    private bool arrived;
    private DateTime nextQuery;
    private Vector3 queryStart;
    private Vector3? snappedFlag;
    private Vector3? snappedQuest;
    private bool dependencyAvailable;
    private DateTime nextDependencyCheck;
    private List<TrackerRow> trackerRows = [];
    private readonly Dictionary<uint, int> questLevels = [];
    private int questToToggle;
    private uint trackedQuestId;
    private uint restoreQuestId;
    private uint restoreLevelId;
    private DateTime restoreDeadline;
    private DateTime nextRestoreAttempt;
    private bool settingsButtonMoved;
    private DateTime nextQuestRefresh;
    private readonly NavigationHud navigationHud;
    private readonly IFontHandle questLevelFont;
    private readonly RewardCard rewardCard;
    private Destination? mapMenuDestination;
    private Vector2 mapMenuPosition;
    private bool mapMenuRequested;
    private bool mapMenuVisible;
    private Destination? customToFollow;

    public Plugin()
    {
        config = Pi.GetPluginConfig() as Configuration ?? new();
        restoreQuestId = config.SavedQuestId;
        restoreLevelId = config.SavedLevelId;
        navigationHud = new NavigationHud(Pi.UiBuilder.FontAtlas);
        questLevelFont = Pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 16));
        rewardCard = new RewardCard(new QuestRewards(Data), Textures);
        navReady = Pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        pathfind = Pi.GetIpcSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>>("vnavmesh.Nav.PathfindCancelable");
        flagFloor = Pi.GetIpcSubscriber<Vector3?>("vnavmesh.Query.Mesh.FlagToPoint");
        nearestFloor = Pi.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPointReachable");
        pointOnFloor = Pi.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
        Commands.AddHandler("/ruta", new CommandInfo(OnCommand)
        {
            HelpMessage = "Guía de rutas: /ruta abre ajustes, /ruta mapa lee la misión abierta, /ruta bandera sigue la bandera, /ruta parar borra la ruta."
        });
        Framework.Update += Update;
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenConfigUi += Open;
        Pi.UiBuilder.OpenMainUi += Open;
        Lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_ToDoList", OnDutyEvent);
        Lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "ScenarioTree", OnDutyEvent);
        Lifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "AreaMap", OnMapEvent);
        Log.Information("Ruta Eorzea 0.11: ficha de recompensas de misión cargada.");
    }

    private void Open() => open = true;
    private void Save() => Pi.SavePluginConfig(config);
    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "parar": Clear(); break;
            case "bandera": SetDestination(GameReader.Flag()); break;
            case "mapa": ReadSelectedQuest(); break;
            default: open = !open; break;
        }
    }

    private void OnDutyEvent(AddonEvent type, AddonArgs args)
    {
        // Observe the result of the game's existing click, without intercepting
        // input or changing the selected quest.
        if (config.FollowQuestClicks && args is AddonReceiveEventArgs e &&
            ((int)e.AtkEventType == (int)AtkEventType.MouseClick || (int)e.AtkEventType == (int)AtkEventType.InputReceived))
            readQuestFrame = frame + 3;
    }

    private unsafe void OnMapEvent(AddonEvent type, AddonArgs args)
    {
        if (disposed || !config.Enabled || !config.MapRightClick || mapMenuVisible || !Client.IsLoggedIn ||
            args is not AddonReceiveEventArgs e || e.AtkEventData == 0) return;
        var eventType = (AtkEventType)e.AtkEventType;
        if (eventType != AtkEventType.MouseDown && eventType != AtkEventType.MouseUp && eventType != AtkEventType.MouseClick) return;
        var mouse = ((AtkEventData*)e.AtkEventData)->MouseData;
        if (mouse.ButtonId != 1 || mouse.Modifier != ModifierFlag.None) return;
        var addon = Gui.GetAddonByName<AddonAreaMap>("AreaMap", 1);
        var map = GameReader.ReadAreaMap(addon, 0);
        var position = new Vector2(mouse.PosX, mouse.PosY);
        if (map == null || !map.Contains(position) || position.X < map.Min.X + 28 ||
            !GameReader.MapOwnsPoint(addon, position)) return;
        if (!Data.GetExcelSheet<Lumina.Excel.Sheets.Map>().TryGetRow(map.Map, out var sheetMap) ||
            sheetMap.TerritoryType.RowId != map.Territory) return;
        e.PreventOriginal();
        if (eventType != AtkEventType.MouseDown) return;
        mapMenuDestination = GameReader.MapDestination(map, position);
        mapMenuPosition = position;
        mapMenuRequested = true;
    }

    private void Clear()
    {
        generation++;
        cancellation?.Cancel();
        target = null;
        travelOnly = false;
        trackedQuestId = 0;
        restoreQuestId = 0;
        config.SavedQuestId = 0;
        config.SavedLevelId = 0;
        candidates.Clear();
        arrived = false;
        transitExit = null;
        snappedExit = null;
        waitingForTransition = false;
        exitSearchAttempted = false;
        snappedFlag = null;
        snappedQuest = null;
        path.Clear();
        status = "Ruta detenida. Selecciona otro objetivo para continuar.";
        Save();
    }

    private void SetDestination(Destination? destination)
    {
        if (destination == null) { status = "No hay una bandera de mapa disponible."; return; }
        generation++;
        cancellation?.Cancel();
        target = destination;
        travelOnly = false;
        exitSearchAttempted = false;
        transitExit = null;
        snappedExit = null;
        waitingForTransition = false;
        trackedQuestId = destination.QuestId;
        restoreQuestId = 0;
        config.SavedQuestId = destination.QuestId;
        config.SavedLevelId = destination.QuestId == 0 ? 0 : destination.LevelId;
        candidates.Clear();
        arrived = false;
        snappedFlag = null;
        snappedQuest = null;
        path.Clear();
        nextQuery = DateTime.MinValue;
        status = destination.Territory == Client.TerritoryType
            ? "Preparando ruta…" : "Buscando la salida hacia la zona del objetivo…";
        Save();
    }

    private void ReadSelectedQuest()
    {
        var selection = GameReader.SelectedQuest();
        if (selection.Quest == 0) { status = "Abre el mapa pulsando el texto del objetivo debajo del título de la misión."; return; }
        FollowQuest(selection.Quest, selection.Map);
    }

    private unsafe List<Destination> QuestChoices(uint questId)
    {
        var choices = GameReader.QuestDestinations(questId);
        if (choices.Count != 0) return choices;
        var manager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager.Instance();
        if (manager == null || manager->IsQuestAccepted(questId) ||
            FFXIVClientStructs.FFXIV.Client.Game.QuestManager.IsQuestComplete(questId)) return choices;
        // The next MSQ may not yet be accepted or have a native quest marker.
        // In that case guide to its issuer, never reuse an accepted quest's start.
        if (!Data.GetExcelSheet<Lumina.Excel.Sheets.Quest>().TryGetRow(questId | 0x10000, out var quest) ||
            !quest.IssuerLocation.IsValid) return choices;
        var location = quest.IssuerLocation.Value;
        if (location.Map.RowId == 0 || location.Territory.RowId == 0) return choices;
        var label = trackerRows.FirstOrDefault(x => x.QuestId == questId)?.Label ?? quest.Name.ToString();
        choices.Add(new(questId, location.RowId, location.Territory.RowId, location.Map.RowId,
            new(location.X, location.Y, location.Z), location.Radius, label));
        return choices;
    }

    private void FollowQuest(uint questId, uint preferredMap = 0, uint preferredLevel = 0)
    {
        var available = QuestChoices(questId);
        // Prefer a destination on the map the game opened for the clicked step.
        var selected = available.Where(x => x.Map == preferredMap).ToList();
        if (selected.Count > 0) available = selected;
        var destination = QuestTargets.Choose(available, Client.TerritoryType,
            Objects.LocalPlayer?.Position ?? Vector3.Zero, preferredLevel);
        if (destination != null)
        {
            SetDestination(destination);
            candidates = available;
            nextQuestRefresh = DateTime.UtcNow.AddSeconds(1);
            return;
        }
        Clear();
        candidates = available;
        trackedQuestId = questId;
        config.SavedQuestId = trackedQuestId;
        Save();
        status = "Esta misión aún no muestra un destino compatible. Esperando un objetivo visible en el mapa.";
    }

    private unsafe bool MapVisible() => Gui.GetAddonByName<AtkUnitBase>("AreaMap", 1) is var addon &&
                                            addon != null && addon->IsVisible;

    private unsafe void Update(IFramework framework)
    {
        frame++;
        if (Interlocked.Exchange(ref customToFollow, null) is { } custom) SetDestination(custom);
        var toggle = Interlocked.Exchange(ref questToToggle, 0);
        if (toggle != 0)
        {
            if (trackedQuestId == (uint)toggle) Clear();
            else
            {
                if (!config.Enabled) { config.Enabled = true; Save(); }
                FollowQuest((uint)toggle);
            }
        }
        if ((config.ShowTrackerButtons || config.ShowQuestLevels || config.ShowQuestRewards) && Client.IsLoggedIn)
        {
            trackerRows = TrackerReader.Read(Gui.GetAddonByName<AtkUnitBase>("_ToDoList", 1));
            trackerRows.AddRange(TrackerReader.Read(Gui.GetAddonByName<AtkUnitBase>("ScenarioTree", 1), true));
        }
        else trackerRows = [];
        if (disposed || !config.Enabled || Objects.LocalPlayer is not { } player || !Client.IsLoggedIn)
        {
            path.Clear();
            minimap = null;
            return;
        }
        if (restoreQuestId != 0 && DateTime.UtcNow >= nextRestoreAttempt)
        {
            nextRestoreAttempt = DateTime.UtcNow.AddSeconds(1);
            if (restoreDeadline == default) restoreDeadline = DateTime.UtcNow.AddSeconds(15);
            var quest = restoreQuestId;
            if (QuestChoices(quest).Count > 0) FollowQuest(quest, preferredLevel: restoreLevelId);
            else if (DateTime.UtcNow >= restoreDeadline)
            {
                Clear();
                status = "La misión guardada ya no aparece activa. Marca otra misión para continuar.";
            }
        }
        if (Client.TerritoryType != lastTerritory || Client.MapId != lastMap)
        {
            lastTerritory = Client.TerritoryType;
            lastMap = Client.MapId;
            generation++;
            cancellation?.Cancel();
            path.Clear();
            minimap = null;
            snappedFlag = null;
            snappedQuest = null;
            transitExit = null;
            snappedExit = null;
            waitingForTransition = false;
            exitSearchAttempted = false;
            arrived = false;
            dependencyAvailable = false;
            nextDependencyCheck = DateTime.MinValue;
            nextQuery = DateTime.UtcNow.AddSeconds(1);
            if (target != null) status = "Preparando la ruta en la nueva zona…";
        }
        if (trackedQuestId != 0 && DateTime.UtcNow >= nextQuestRefresh)
        {
            nextQuestRefresh = DateTime.UtcNow.AddSeconds(1);
            var choices = QuestChoices(trackedQuestId);
            if (choices.Count > 0)
            {
                if (target == null || !QuestTargets.StillAvailable(target, choices))
                {
                    var destination = QuestTargets.Choose(choices, Client.TerritoryType, player.Position);
                    if (destination != null) SetDestination(destination);
                }
                candidates = choices;
            }
        }
        if (DateTime.UtcNow >= nextDependencyCheck)
        {
            nextDependencyCheck = DateTime.UtcNow.AddSeconds(2);
            try { dependencyAvailable = navReady.InvokeFunc(); }
            catch { dependencyAvailable = false; }
        }
        var mapVisible = MapVisible();
        var selection = GameReader.SelectedQuest();
        if (config.FollowQuestClicks && mapVisible && selection.Quest != 0 &&
            (!mapWasVisible || selection != lastQuest)) readQuestFrame = frame + 2;
        mapWasVisible = mapVisible;
        lastQuest = selection;
        if (readQuestFrame != 0 && frame >= readQuestFrame)
        {
            readQuestFrame = 0;
            if (mapVisible) ReadSelectedQuest();
        }
        if (config.FollowMapFlag)
        {
            var flag = GameReader.Flag();
            if (mapVisible && flag != null && (lastFlag == null || flag.Map != lastFlag.Map ||
                Vector2.DistanceSquared(RouteMath.Xz(flag.Position), RouteMath.Xz(lastFlag.Position)) > 0.01f))
                SetDestination(flag);
            lastFlag = flag;
        }
        if (pending is { IsCompleted: true })
        {
            try
            {
                var points = pending.GetAwaiter().GetResult();
                if (pendingGeneration == generation)
                {
                    var valid = points.Count >= 2 && points.Count <= 20000 && points.All(RouteMath.Finite);
                    if (valid)
                    {
                        if (Vector3.Distance(points[^1], pendingGoal) > Math.Max(5, transitExit == null ? target!.Radius : 3)) valid = false;
                    }
                    path = valid ? points : [];
                    status = valid ? (transitExit == null ? "Ruta lista." : $"Sigue el camino hasta la salida hacia {transitExit.Name}.")
                        : "No se encontró un camino completo. No se dibujará una línea que atraviese obstáculos.";
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (pendingGeneration == generation) { path.Clear(); status = "No se pudo calcular la ruta. Revisa vnavmesh."; }
                Log.Warning(ex, "Falló el cálculo de ruta");
            }
            pending = null;
        }
        if (target == null || arrived) return;
        if (travelOnly && target.Territory == Client.TerritoryType)
        {
            generation++;
            cancellation?.Cancel();
            path.Clear();
            arrived = true;
            status = $"Llegaste a {target.Label}. Selecciona una misión para continuar.";
            Chat.Print($"[Ruta Eorzea] {status}");
            return;
        }
        if (target.Territory != Client.TerritoryType)
        {
            if (transitExit == null)
            {
                if (exitSearchAttempted) return;
                exitSearchAttempted = true;
                try
                {
                    zoneData ??= new ZoneData(Data);
                    transitExit = zoneData.Graph.NextExit(Client.TerritoryType, Client.MapId, target.Territory,
                        RouteMath.Xz(player.Position));
                }
                catch (Exception ex) { Log.Warning(ex, "No se pudieron leer las conexiones entre zonas"); }
                if (transitExit == null)
                {
                    status = "No hay una conexión a pie conocida hasta esa zona. Puede requerir transporte, una entrada especial o un desbloqueo.";
                    return;
                }
            }
        }
        if (!dependencyAvailable)
        {
            path.Clear();
            status = "Esperando vnavmesh: instálalo o espera a que termine de preparar la zona.";
            return;
        }
        var position = player.Position;
        if (waitingForTransition)
        {
            if (snappedExit.HasValue && Vector3.Distance(position, snappedExit.Value) < 9) return;
            waitingForTransition = false;
        }
        if (path.Count >= 2)
        {
            progress = RouteMath.Progress(path, position);
            if (Vector3.Distance(position, path[^1]) <= (transitExit == null ? Math.Max(2.5f, Math.Min(target.Radius, 8)) : 3))
            {
                if (transitExit != null)
                {
                    status = $"Cruza la salida hacia {transitExit.Name}. La guía continuará al cargar la zona.";
                    waitingForTransition = true;
                }
                else
                {
                    status = target.Radius > 3 ? "Llegaste al área del objetivo." : "Llegaste al destino.";
                    arrived = true;
                }
                path.Clear();
                return;
            }
            minimap = GameReader.ReadMinimap(Gui.GetAddonByName<AddonNaviMap>("_NaviMap", 1), position);
        }
        if (pending != null || DateTime.UtcNow < nextQuery) return;
        if (path.Count >= 2 && Vector3.DistanceSquared(position, queryStart) < 100 && progress.DistanceFromRoute < 4) return;
        nextQuery = DateTime.UtcNow.AddSeconds(2);
        try
        {
            var goal = target.Position;
            if (transitExit != null)
            {
                snappedExit ??= nearestFloor.InvokeFunc(new Vector3(transitExit.Position.X, position.Y, transitExit.Position.Y), 18, 100);
                if (!snappedExit.HasValue || Vector2.Distance(RouteMath.Xz(snappedExit.Value), transitExit.Position) > 18)
                {
                    status = "No se encontró suelo transitable cerca de la salida. Revisa el mapa para cruzar a la siguiente zona.";
                    return;
                }
                goal = snappedExit.Value;
            }
            else if (target.QuestId == 0)
            {
                if (target.Custom)
                {
                    if (target.HasAltitude) snappedFlag = target.Position;
                    else snappedFlag ??= pointOnFloor.InvokeFunc(new(target.Position.X, 1024, target.Position.Z), false, 5)
                        ?? nearestFloor.InvokeFunc(new(target.Position.X, position.Y, target.Position.Z), 10, 200);
                }
                else snappedFlag = flagFloor.InvokeFunc();
                if (!snappedFlag.HasValue) { status = "No se encontró suelo transitable en ese punto del mapa."; return; }
                goal = snappedFlag.Value;
            }
            else
            {
                // Native quest markers can be underground or at the center of
                // an obstacle. Resolve a nearby reachable surface before querying.
                var horizontalRange = target.Radius > 3 ? Math.Clamp(target.Radius, 3, 16) : 3;
                snappedQuest ??= nearestFloor.InvokeFunc(target.Position, horizontalRange, 100)
                    ?? pointOnFloor.InvokeFunc(new(target.Position.X, 1024, target.Position.Z), false, horizontalRange);
                if (!snappedQuest.HasValue)
                {
                    status = "No se encontró suelo transitable cerca del objetivo de la misión.";
                    return;
                }
                goal = snappedQuest.Value;
            }
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            pendingGeneration = generation;
            queryStart = position;
            pendingGoal = goal;
            pending = pathfind.InvokeFunc(position, goal, false, cancellation.Token);
            if (path.Count == 0) status = "Calculando camino transitable…";
        }
        catch (Exception ex)
        {
            status = "vnavmesh no responde o no tiene una versión compatible.";
            Log.Warning(ex, "No se pudo consultar vnavmesh");
        }
    }

    private void Draw()
    {
        if (disposed) return;
        DrawMapMenu();
        if (config.ShowSettingsButton) DrawSettingsButton();
        if (open) DrawSettings();
        if (config.ShowTrackerButtons) DrawTrackerButtons();
        if (config.ShowQuestLevels) DrawQuestLevels();
        if (config.ShowQuestRewards) DrawQuestRewards();
        if (config.Enabled && config.ShowArrow && (target != null || trackedQuestId != 0) && path.Count < 2)
            DrawTravelStatus();
        if (!config.Enabled || path.Count < 2 || Objects.LocalPlayer is not { } player) return;
        if (config.ShowMinimap) DrawMinimap();
        if (config.ShowAreaMap) DrawAreaMap();
        if (config.ShowWorldPath && !MapVisible()) DrawWorldPath();
        if (config.ShowArrow) DrawArrow(player.Position, player.Rotation);
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSize(new Vector2(640, 620), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Ruta Eorzea — experimental###RutaEorzea", ref open)) { ImGui.End(); return; }
        ImGui.TextWrapped(status);
        if (target != null) ImGui.TextWrapped($"Destino: {target.Label}");
        if (path.Count >= 2) ImGui.Text($"Distancia por el camino: {progress.Remaining:0} m");
        ImGui.Separator();
        ImGui.TextWrapped("Marca la casilla a la derecha de una misión para seguirla; desmárcala para detener la guía. También puedes pulsar su objetivo o colocar una bandera con Ctrl + clic derecho en el mapa.");
        if (ImGui.Button("Leer misión del mapa abierto")) ReadSelectedQuest();
        ImGui.SameLine();
        if (ImGui.Button("Seguir bandera")) SetDestination(GameReader.Flag());
        if (ImGui.Button("Detener guía")) Clear();
        if (ImGui.TreeNode("Guiar hasta una zona vecina"))
        {
            zoneData ??= new ZoneData(Data);
            foreach (var exit in zoneData.Graph.Adjacent(Client.TerritoryType))
                if (ImGui.Button($"Ir a {exit.Name}##zone{exit.To}"))
                {
                    SetDestination(new Destination(0, 0, exit.To, exit.ToMap, Vector3.Zero, 0, exit.Name));
                    travelOnly = true;
                }
            ImGui.TreePop();
        }
        if (candidates.Count > 1)
        {
            ImGui.Separator();
            for (var i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                var detail = item.Territory == Client.TerritoryType && Objects.LocalPlayer is { } local
                    ? $"{Vector3.Distance(local.Position, item.Position):0} m de distancia" : "otra zona";
                if (ImGui.Button($"Seguir objetivo {i + 1} · {detail}##target{i}"))
                    SetDestination(item);
            }
        }
        ImGui.Separator();
        var changed = false;
        var enabled = config.Enabled; if (ImGui.Checkbox("Activar guía", ref enabled)) { config.Enabled = enabled; changed = true; }
        var quest = config.FollowQuestClicks; if (ImGui.Checkbox("Seguir al pulsar objetivos de misión", ref quest)) { config.FollowQuestClicks = quest; changed = true; }
        var buttons = config.ShowTrackerButtons; if (ImGui.Checkbox("Casillas a la derecha de cada misión", ref buttons)) { config.ShowTrackerButtons = buttons; changed = true; }
        var levels = config.ShowQuestLevels; if (ImGui.Checkbox("Nivel de misión con color según tu nivel", ref levels)) { config.ShowQuestLevels = levels; changed = true; }
        var rewards = config.ShowQuestRewards; if (ImGui.Checkbox("Ficha de recompensas al pasar sobre una misión", ref rewards)) { config.ShowQuestRewards = rewards; changed = true; }
        if (levels) ImGui.TextWrapped("Verde: desde 5 niveles menos hasta 2 más. Naranja: 3–4 más. Rojo: 5 o más. Gris: 6 o más por debajo. El color compara niveles; no calcula la recompensa de EXP.");
        var rightClick = config.MapRightClick; if (ImGui.Checkbox("Crear rutas con clic derecho en el mapa", ref rightClick)) { config.MapRightClick = rightClick; changed = true; }
        var settingsButton = config.ShowSettingsButton; if (ImGui.Checkbox("Botón flotante de ajustes en el minimapa", ref settingsButton)) { config.ShowSettingsButton = settingsButton; changed = true; }
        var flag = config.FollowMapFlag; if (ImGui.Checkbox("Seguir banderas nuevas del mapa", ref flag)) { config.FollowMapFlag = flag; changed = true; }
        var mini = config.ShowMinimap; if (ImGui.Checkbox("Ruta sobre el minimapa", ref mini)) { config.ShowMinimap = mini; changed = true; }
        var area = config.ShowAreaMap; if (ImGui.Checkbox("Ruta en el mapa grande (M)", ref area)) { config.ShowAreaMap = area; changed = true; }
        var world = config.ShowWorldPath; if (ImGui.Checkbox("Ruta sobre el terreno", ref world)) { config.ShowWorldPath = world; changed = true; }
        var arrow = config.ShowArrow; if (ImGui.Checkbox("Flecha con distancia", ref arrow)) { config.ShowArrow = arrow; changed = true; }
        var followLine = config.AlignArrowWithMinimap; if (ImGui.Checkbox("Flecha en la dirección del rastro del minimapa", ref followLine)) { config.AlignArrowWithMinimap = followLine; changed = true; }
        if (!followLine)
        {
            var camera = config.CameraRelativeArrow; if (ImGui.Checkbox("Flecha según la cámara (desmarcado: según personaje)", ref camera)) { config.CameraRelativeArrow = camera; changed = true; }
        }
        var width = config.LineWidth; if (ImGui.SliderFloat("Grosor", ref width, 1, 7)) { config.LineWidth = width; changed = true; }
        var color = config.RouteColor; if (ImGui.ColorEdit4("Color", ref color)) { config.RouteColor = color; changed = true; }
        if (changed) Save();
        ImGui.Separator();
        ImGui.TextWrapped("Guía por salidas a pie entre zonas. En misiones con varios objetivos se elige el más cercano de tu zona y se actualiza cuando cambian los marcadores. Transportes y entradas especiales pueden requerir intervención.");
        if (!string.IsNullOrEmpty(minimapStatus)) ImGui.TextWrapped(minimapStatus);
        if (ImGui.Button("Guardar diagnóstico local")) WriteDiagnostics();
        ImGui.End();
    }

    private unsafe void DrawSettingsButton()
    {
        var bounds = GameReader.ReadMinimapBounds(Gui.GetAddonByName<AddonNaviMap>("_NaviMap", 1));
        if (bounds == null || !Client.IsLoggedIn) return;
        var viewport = ImGui.GetMainViewport();
        var radians = config.SettingsButtonAngle * MathF.PI / 180;
        var mapCenter = viewport.Pos + bounds.Center;
        var center = mapCenter + new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * (bounds.Radius + 13);
        center = Vector2.Clamp(center, viewport.Pos + new Vector2(17), viewport.Pos + viewport.Size - new Vector2(17));
        ImGui.SetNextWindowPos(center - new Vector2(17), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(34), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;
        if (ImGui.Begin("Ajustes de Ruta Eorzea##RutaEorzeaMinimapButton", flags))
        {
            var pressed = ImGui.InvisibleButton("##ajustes", new Vector2(34));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4))
            {
                var delta = ImGui.GetIO().MousePos - mapCenter;
                if (delta.LengthSquared() > 25)
                {
                    config.SettingsButtonAngle = MathF.Atan2(delta.Y, delta.X) * 180 / MathF.PI;
                    settingsButtonMoved = true;
                }
            }
            if (pressed && !settingsButtonMoved) open = !open;
            if (settingsButtonMoved && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            { Save(); settingsButtonMoved = false; }
            var draw = ImGui.GetWindowDrawList();
            var accent = ImGui.ColorConvertFloat4ToU32(config.RouteColor);
            draw.AddCircleFilled(center, 15, hovered ? 0xFF315E47 : 0xF21A221C, 32);
            draw.AddCircle(center, 15, trackedQuestId != 0 ? accent : 0xFF9FB8C2, 32, 1.5f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * MathF.PI / 4;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                draw.AddLine(center + direction * 8, center + direction * 11, 0xFFE0E6E3, 3);
            }
            draw.AddCircle(center, 7, 0xFFE0E6E3, 24, 3);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Ruta Eorzea — Ajustes");
                ImGui.TextUnformatted("Clic: abrir o cerrar. Arrastra para mover alrededor del minimapa.");
                if (target != null) ImGui.TextUnformatted($"Destino: {target.Label}");
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 330);
                ImGui.TextWrapped(status);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawTrackerButtons()
    {
        if (trackerRows.Count == 0) return;
        var viewport = ImGui.GetMainViewport();
        var secondaryRight = trackerRows.Where(x => !x.MainScenario).Select(x => x.Right).DefaultIfEmpty(0).Max() + 24;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.08f, .09f, .08f, .94f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.15f, .36f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.14f, .45f, .3f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, config.RouteColor);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.7f, .66f, .48f, .9f));
        foreach (var row in trackerRows)
        {
            var right = Math.Clamp(row.MainScenario ? row.Right + 12 : secondaryRight, 2, viewport.Size.X - 28);
            var top = row.Position.Y + (row.Height - ImGui.GetFrameHeight()) / 2;
            if (top < 0 || top + ImGui.GetFrameHeight() > viewport.Size.Y) continue;
            ImGui.SetNextWindowPos(viewport.Pos + new Vector2(right, top), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(ImGui.GetFrameHeight() + 2), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            if (ImGui.Begin($"Seguir misión##RutaEorzeaTrack{row.MainScenario}_{row.QuestId}", flags))
            {
                var selected = trackedQuestId == row.QuestId;
                if (ImGui.Checkbox("##seguir", ref selected)) Interlocked.Exchange(ref questToToggle, (int)row.QuestId);
                if (ImGui.IsItemHovered())
                {
                    if (config.ShowQuestRewards && Objects.LocalPlayer is { } tooltipPlayer)
                    {
                        using var rewardFont = questLevelFont.Push();
                        rewardCard.Draw(row, tooltipPlayer.Level, selected, status);
                    }
                    else
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(row.Label);
                        ImGui.TextUnformatted(selected ? "Siguiendo. Pulsa para quitar la ruta." : "Pulsa para seguir esta misión.");
                        if (selected) ImGui.TextWrapped(status);
                        ImGui.EndTooltip();
                    }
                }
            }
            ImGui.End();
        }
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(5);
    }

    private void DrawQuestLevels()
    {
        if (Objects.LocalPlayer is not { } player || trackerRows.Count == 0) return;
        var viewport = ImGui.GetMainViewport();
        using var font = questLevelFont.Push();
        var draw = ImGui.GetBackgroundDrawList();
        foreach (var row in trackerRows)
        {
            if (!questLevels.TryGetValue(row.QuestId, out var level))
            {
                if (!Data.GetExcelSheet<Lumina.Excel.Sheets.Quest>().TryGetRow(row.QuestId | 0x10000, out var quest)) continue;
                level = quest.ClassJobLevel[0];
                questLevels[row.QuestId] = level;
            }
            if (level <= 0) continue;
            var difficulty = QuestLevels.Compare(level, player.Level);
            var color = difficulty switch
            {
                QuestDifficulty.VeryHigh => new Vector4(1, .12f, .08f, 1),
                QuestDifficulty.Higher => new Vector4(1, .6f, .02f, 1),
                QuestDifficulty.Suitable => new Vector4(.18f, 1, .3f, 1),
                _ => new Vector4(.75f, .77f, .75f, 1)
            };
            var text = $"({level})";
            var size = ImGui.CalcTextSize(text);
            var x = row.MainScenario ? row.TextPosition.X + row.TextWidth + 6 : row.TextPosition.X - size.X - 5;
            var y = row.Position.Y + (row.Height - size.Y) / 2;
            if (y < 0 || y + size.Y > viewport.Size.Y) continue;
            x = Math.Clamp(x, 2, viewport.Size.X - size.X - 2);
            var position = viewport.Pos + new Vector2(x, y);
            // Integrate with the native title's actual glyph bounds. No floating
            // badge or input-catching window; native quest clicks stay available.
            draw.AddText(position + new Vector2(-1, 0), 0xFF000000, text);
            draw.AddText(position + new Vector2(1, 0), 0xFF000000, text);
            draw.AddText(position + new Vector2(0, -1), 0xFF000000, text);
            draw.AddText(position + new Vector2(0, 1), 0xFF000000, text);
            draw.AddText(position, ImGui.ColorConvertFloat4ToU32(color), text);
        }
    }

    private void DrawQuestRewards()
    {
        if (Objects.LocalPlayer is not { } player || ImGui.GetIO().WantCaptureMouse ||
            ImGui.IsMouseDown(ImGuiMouseButton.Left) || mapMenuVisible) return;
        var viewport = ImGui.GetMainViewport().Pos;
        foreach (var row in trackerRows)
        {
            var min = viewport + row.TextPosition - new Vector2(config.ShowQuestLevels && !row.MainScenario ? 32 : 0, 1);
            var max = viewport + new Vector2(row.TextPosition.X + row.TextWidth + (row.MainScenario ? 32 : 0), row.Position.Y + row.Height + 1);
            if (!ImGui.IsMouseHoveringRect(min, max, false)) continue;
            using var rewardFont = questLevelFont.Push();
            rewardCard.Draw(row, player.Level, trackedQuestId == row.QuestId, status);
            break;
        }
    }

    private void DrawMapMenu()
    {
        if (mapMenuRequested)
        {
            mapMenuRequested = false;
            ImGui.OpenPopup("Ruta Eorzea##MapaContextual");
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + mapMenuPosition);
        }
        mapMenuVisible = ImGui.BeginPopup("Ruta Eorzea##MapaContextual");
        if (!mapMenuVisible) return;
        if (mapMenuDestination is { } destination)
        {
            ImGui.TextUnformatted(destination.Label);
            ImGui.Separator();
            if (ImGui.MenuItem("Crear ruta hasta aquí")) Interlocked.Exchange(ref customToFollow, destination);
        }
        if (ImGui.MenuItem("Cancelar")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawMinimap()
    {
        if (minimap is not { Calibrated: true } map)
        {
            minimapStatus = "Minimapa: esperando a que el juego muestre el mapa y su escala.";
            return;
        }
        minimapStatus = "Minimapa: escala y giro leídos directamente del juego; no necesita iconos cercanos.";
        var draw = ImGui.GetBackgroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(config.RouteColor);
        var origin = map.WorldOrigin;
        var viewport = ImGui.GetMainViewport().Pos;
        var mapCenter = map.Center + viewport;
        Vector2 Project(Vector3 point) => viewport + map.PlayerPin + RouteMath.Project(RouteMath.Xz(point) - origin, map.Projection);
        if (progress.DistanceFromRoute > 2)
        {
            var a = map.PlayerPin + viewport;
            var b = Project(progress.Foot);
            if (RouteMath.ClipCircle(ref a, ref b, mapCenter, map.Radius))
            {
                draw.AddLine(a, b, 0xDD000000, config.LineWidth + 2);
                draw.AddLine(a, b, color, config.LineWidth);
            }
        }
        for (var i = progress.Segment; i < path.Count - 1; ++i)
        {
            var a = Project(i == progress.Segment ? progress.Foot : path[i]);
            var b = Project(path[i + 1]);
            if (!RouteMath.ClipCircle(ref a, ref b, mapCenter, map.Radius)) continue;
            draw.AddLine(a, b, 0xDD000000, config.LineWidth + 2);
            draw.AddLine(a, b, color, config.LineWidth);
        }
        var end = Project(path[^1]);
        if (Vector2.Distance(end, mapCenter) < map.Radius) draw.AddCircleFilled(end, 4, color);
    }

    private unsafe void DrawAreaMap()
    {
        var map = GameReader.ReadAreaMap(Gui.GetAddonByName<AddonAreaMap>("AreaMap", 1), Client.MapId);
        if (map == null) return;
        var viewport = ImGui.GetMainViewport().Pos;
        var draw = ImGui.GetBackgroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(config.RouteColor);
        var width = Math.Max(2, config.LineWidth);
        draw.PushClipRect(viewport + map.Min, viewport + map.Max, true);
        for (var i = progress.Segment; i < path.Count - 1; i++)
        {
            var a = viewport + map.Project(i == progress.Segment ? progress.Foot : path[i]);
            var b = viewport + map.Project(path[i + 1]);
            draw.AddLine(a, b, 0xDD182019, width + 2);
            draw.AddLine(a, b, color, width);
        }
        var end = viewport + map.Project(path[^1]);
        draw.AddCircleFilled(end, 5, 0xDD182019);
        draw.AddCircleFilled(end, 3, color);
        draw.PopClipRect();
    }

    private void DrawWorldPath()
    {
        var draw = ImGui.GetBackgroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(config.RouteColor);
        // Subdivide to avoid drawing a huge chord across the screen when only one
        // end of a long segment is visible. Points behind the camera are omitted.
        for (var i = progress.Segment; i < path.Count - 1; ++i)
        {
            var from = i == progress.Segment ? progress.Foot : path[i];
            var to = path[i + 1];
            var parts = Math.Clamp((int)(Vector3.Distance(from, to) / 2) + 1, 1, 200);
            for (var j = 0; j < parts; j++)
            {
                var a = Vector3.Lerp(from, to, j / (float)parts) + new Vector3(0, .15f, 0);
                var b = Vector3.Lerp(from, to, (j + 1) / (float)parts) + new Vector3(0, .15f, 0);
                if (!Gui.WorldToScreen(a, out var sa) || !Gui.WorldToScreen(b, out var sb)) continue;
                draw.AddLine(sa, sb, 0xAA000000, config.LineWidth + 2);
                draw.AddLine(sa, sb, color, config.LineWidth);
            }
        }
    }

    private void DrawArrow(Vector3 player, float heading)
    {
        var origin = progress.DistanceFromRoute > 2 ? player : progress.Foot;
        var next = RouteMath.SteeringPoint(path, progress, origin);
        var delta = next - origin;
        Vector2? Project(Vector3 point) => Gui.WorldToScreen(point, out var screen, out _) ? screen : null;
        var direction = config.CameraRelativeArrow
            ? RouteMath.ProjectedDirection(origin, next, Project) ?? GameReader.CameraDirection(delta)
            : RouteMath.ScreenDirection(RouteMath.Xz(delta), MathF.PI - heading);
        if (config.AlignArrowWithMinimap && minimap is { Calibrated: true } nativeMap)
            direction = RouteMath.MinimapRouteDirection(path, progress, player, nativeMap.Projection);
        var caption = transitExit != null ? $"Hacia {transitExit.Name}" : target?.Radius > 3 ? "Área de búsqueda"
            : candidates.Count > 1 ? "Objetivo más cercano" : "Hasta el objetivo";
        var distance = progress.Remaining >= 1000 ? $"{progress.Remaining / 1000:0.0} km" : $"{progress.Remaining:0} m";
        navigationHud.Draw(target?.Label ?? "Guía de ruta", caption, distance, direction, false, status);
    }

    private void DrawTravelStatus()
    {
        var title = target?.Label ?? trackerRows.FirstOrDefault(x => x.QuestId == trackedQuestId)?.Label ?? "Guía de ruta";
        var unavailable = status.StartsWith("No se encontró", StringComparison.Ordinal) || status.StartsWith("No hay una conexión", StringComparison.Ordinal);
        var caption = arrived ? "Destino alcanzado" : unavailable ? "Sin camino disponible" : waitingForTransition ? $"Cruza hacia {transitExit?.Name}"
            : target == null ? "Esperando un objetivo" : !dependencyAvailable ? "Preparando la zona" : "Preparando el camino";
        var value = arrived ? "LISTO" : unavailable ? "SIN RUTA" : waitingForTransition ? "CRUZAR" : "ESPERA";
        navigationHud.Draw(title, caption, value, waitingForTransition ? new Vector2(0, -1) : null, arrived, status);
    }

    private unsafe void WriteDiagnostics()
    {
        try
        {
            var data = new { Version = "0.11.0", Territory = Client.TerritoryType, Map = Client.MapId,
                TrackedQuest = trackedQuestId, TrackerRows = trackerRows,
                TransitExit = transitExit, SnappedExit = snappedExit, WaitingForTransition = waitingForTransition,
                Status = status, Minimap = minimap, Target = target, RoutePoints = path.Count,
                DependencyReady = dependencyAvailable, SelectedQuest = GameReader.SelectedQuest(), Native = GameReader.Inspect(),
                Player = Objects.LocalPlayer?.Position,
                NativeMinimap = GameReader.InspectMinimap(Gui.GetAddonByName<AddonNaviMap>("_NaviMap", 1)),
                NativeAreaMap = GameReader.InspectAreaMap(Gui.GetAddonByName<AddonAreaMap>("AreaMap", 1)),
                MainScenario = TrackerReader.Inspect(Gui.GetAddonByName<AtkUnitBase>("ScenarioTree", 1)),
                Camera = GameReader.InspectCamera() };
            var file = Path.Combine(Pi.GetPluginConfigDirectory(), "diagnostico.json");
            File.WriteAllText(file, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            Chat.Print("[Ruta Eorzea] Diagnóstico guardado en la carpeta local del plugin.");
        }
        catch (Exception ex) { Log.Warning(ex, "No se pudo guardar el diagnóstico"); }
    }

    public void Dispose()
    {
        disposed = true;
        navigationHud.Dispose();
        questLevelFont.Dispose();
        generation++;
        cancellation?.Cancel();
        if (pending != null) _ = pending.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        cancellation?.Dispose();
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Pi.UiBuilder.OpenMainUi -= Open;
        Lifecycle.UnregisterListener(OnDutyEvent);
        Lifecycle.UnregisterListener(OnMapEvent);
        Commands.RemoveHandler("/ruta");
    }
}
