using System.Numerics;
using RutaEorzea;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
bool Near(float a, float b) => MathF.Abs(a - b) < .001f;

// A route around a corner must retain the corner, not point through the wall.
Vector3[] corner = [new(0,0,0), new(10,0,0), new(10,0,10)];
var progress = RouteMath.Progress(corner, new(8,0,0));
Check(progress.Segment == 0 && Near(progress.Remaining, 12), "Distance around corner");
Check(RouteMath.Ahead(corner, progress, 6) == new Vector3(10,0,4), "Lookahead follows corner");
Check(Near(RouteMath.Progress(corner, new(10,0,7)).Remaining, 3), "Progress after corner");

// Coincident X/Z on two floors: choose the correct height.
Vector3[] floors = [new(0,0,0), new(10,0,0), new(10,12,0), new(0,12,0)];
Check(RouteMath.Progress(floors, new(2,12,0)).Segment == 2, "Bridge/floor separation");
Vector3[] duplicate = [new(0,0,0),new(0,0,0),new(4,0,0)];
Check(float.IsFinite(RouteMath.Progress(duplicate, new(1,0,0)).Remaining), "Zero-length segment");

var a = new Vector2(-20,0); var b = new Vector2(20,0);
Check(RouteMath.ClipCircle(ref a, ref b, Vector2.Zero, 10) && Near(a.X,-10) && Near(b.X,10), "Circle crossing with both ends outside");
a = new(-20,12); b = new(20,12);
Check(!RouteMath.ClipCircle(ref a, ref b, Vector2.Zero, 10), "Outside route is hidden");
a = new(0,0); b = new(20,0);
Check(RouteMath.ClipCircle(ref a, ref b, Vector2.Zero, 10) && a == Vector2.Zero && Near(b.X,10), "Clip exiting circle");
a = new(3,4); b = a;
Check(RouteMath.ClipCircle(ref a, ref b, Vector2.Zero,10), "Zero-length inside circle");
a = new(30,40); b = a;
Check(!RouteMath.ClipCircle(ref a, ref b, Vector2.Zero,10), "Zero-length outside circle");

// Rotate 90 degrees and scale 2: independent of the minimap's zoom and placement.
Vector2 origin = new(100,200), pin = new(1700,100), rotation = new(0,2);
List<(Vector2 World,Vector2 Screen)> references = [
    (origin + new Vector2(10,0),pin + new Vector2(0,20)),
    (origin + new Vector2(0,10),pin + new Vector2(-20,0)),
    (origin + new Vector2(-8,0),pin + new Vector2(0,-16))];
Check(RouteMath.FitProjection(origin,pin,references,out var fit) && Vector2.Distance(fit,rotation)<.001, "Fit rotating scaled minimap");
Check(RouteMath.Project(new Vector2(3,4),fit)==new Vector2(-8,6), "Project world waypoint");
references[2] = (origin + new Vector2(-8,0),pin + new Vector2(40,50));
Check(!RouteMath.FitProjection(origin,pin,references,out _), "Reject inconsistent/clamped markers");
Check(!RouteMath.FitProjection(origin,pin,[],out _), "No guessed minimap without references");
Check(!RouteMath.Finite(new(float.NaN,0,0)), "Reject invalid route coordinate");

// Regression: measured native marker locations in New Gridania. The rotating
// player's node bounds are offset from the actual center of the minimap.
origin = new(-77.77362f,49.91398f); pin = new(1792,165);
references = [
    (new(-38,53),new(1831.77f,168.09f)),
    (new(-69,-7),new(1800.77f,108.09f)),
    (new(-79,-2),new(1790.77f,113.09f)),
    (new(-114,88),new(1755.77f,203.09f)),
    (new(-77.75f,38.25f),new(1792.02f,153.34f))];
Check(RouteMath.FitProjection(origin,pin,references,out fit) && Vector2.Distance(fit,Vector2.UnitX)<.01f,
    "Native minimap uses map center as player origin");
Check(!RouteMath.FitProjection(origin,new(1799.8911f,159.87549f),references,out _),
    "Rotated player node bounds cannot anchor minimap");
references = [
    (new(-38,53),new(1851.655f,169.63501f)),
    (new(-77.75f,38.25f),new(1792.03f,147.51001f))];
Check(RouteMath.FitProjection(origin,pin,references,out fit) && Vector2.Distance(fit,new(1.5f,0))<.01f,
    "Native enlarged minimap retains correct route scale");
Console.WriteLine($"PASS: {checks} navigation/projection checks");

ZoneExit west = new(132, 2, 133, 3, new(-104.5f,10), "Vieja Gridania");
ZoneExit east = new(132, 2, 133, 3, new(100.5f,15), "Vieja Gridania");
ZoneExit central = new(132, 2, 148, 4, new(170,162), "Bosque Central");
ZoneExit north = new(133, 3, 154, 7, new(-217,-96), "Bosque del Norte");
var graph = new ZoneGraph([west, east, central, north,
    new(148,4,154,7,new(0,0),"Bosque del Norte"),
    new(132,99,154,7,new(0,0),"Otro nivel de mapa")]);
Check(graph.NextExit(132,2,133,new(-77,49))==west, "Choose nearest walking exit to Old Gridania");
Check(graph.NextExit(132,2,133,new(90,40))==east, "Choose another valid exit when closer");
Check(graph.NextExit(132,2,154,new(-77,49))==west, "Continue through intermediate zone");
Check(graph.NextExit(133,3,154,new(-128,-36))==north, "Recalculate next leg after crossing into intermediate zone");
Check(graph.NextExit(132,2,148,new(-77,49))==central, "Prefer fewer zone transitions");
Check(graph.NextExit(132,2,132,new(-77,49))==null, "Stop zone routing in destination territory");
Check(graph.NextExit(154,7,132,Vector2.Zero)==null, "Do not invent reverse or inaccessible connections");
Check(graph.NextExit(132,50,133,Vector2.Zero)==null, "Do not guide through an exit on another map floor");
Check(graph.Adjacent(132).Count()==3, "Group duplicate exits by destination zone");
Console.WriteLine($"PASS: {checks} total navigation/projection/zone checks");

Destination incenseFar = new(20, 1999920, 133, 3, new(20,0,0), 1, "Incienso y sensibilidad");
Destination incenseNear = new(20, 1999921, 133, 3, new(3,0,0), 1, "Incienso y sensibilidad");
Check(QuestTargets.Choose([incenseFar, incenseNear],133,Vector3.Zero)==incenseNear,
    "Multi-objective incense quest selects nearby destination without settings picker");
var otherZone = incenseNear with { Territory = 132, Position = Vector3.Zero };
Check(QuestTargets.Choose([otherZone,incenseFar],133,Vector3.Zero)==incenseFar,
    "Prefer objective in current zone over coordinates from another zone");
Check(QuestTargets.Choose([incenseFar,incenseNear],133,Vector3.Zero,1999920)==incenseFar,
    "Restore explicitly chosen objective");
Check(!QuestTargets.StillAvailable(incenseNear,[incenseFar]), "Completed objective triggers selection of remaining one");
Check(QuestTargets.StillAvailable(incenseFar,[incenseFar]), "Keep active objective while it remains available");
Check(QuestTargets.Choose([],133,Vector3.Zero)==null, "Missing objective waits without opening settings");
Check(Vector2.Distance(RouteMath.ScreenDirection(new(0,-10),0),new(0,-1))<.001,
    "Camera facing north: north objective points upward");
Check(Vector2.Distance(RouteMath.ScreenDirection(new(0,-10),MathF.PI/2),new(-1,0))<.001,
    "Camera facing east: north objective points left");
Check(Vector2.Distance(RouteMath.ScreenDirection(new(0,10),0),new(0,1))<.001,
    "Objective behind camera points downward");
Console.WriteLine($"PASS: {checks} total checks including quest selection and camera-relative arrow");

Func<Vector3,Vector2?> Projection(Matrix4x4 view)
{
    var perspective = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,16f/9,.05f,1000);
    return point => {
        var clip = Vector4.Transform(new Vector4(point,1),view*perspective);
        if (clip.W <= .0001f) return null;
        return new Vector2((clip.X/clip.W+1)*960,(1-clip.Y/clip.W)*540);
    };
}
var southCamera = Matrix4x4.CreateLookAt(new(0,5,8),Vector3.Zero,Vector3.UnitY);
var southProjection = Projection(southCamera);
var rightDirection = RouteMath.ProjectedDirection(Vector3.Zero,new(10,0,0),southProjection);
Check(rightDirection.HasValue && rightDirection.Value.X>.9f, "Projected east route points right when camera is south");
var aheadDirection = RouteMath.ProjectedDirection(Vector3.Zero,new(0,0,-10),southProjection);
Check(aheadDirection.HasValue && aheadDirection.Value.Y<-.9f, "Projected forward route points up");
Check(!southProjection(new(0,0,30)).HasValue, "Regression fixture places waypoint behind camera");
var behindDirection = RouteMath.ProjectedDirection(Vector3.Zero,new(0,0,30),southProjection);
Check(behindDirection.HasValue && behindDirection.Value.Y>.9f, "Short route probe keeps correct downward direction for waypoint behind camera");
var eastCamera = Matrix4x4.CreateLookAt(new(8,5,0),Vector3.Zero,Vector3.UnitY);
var rotatedDirection = RouteMath.ProjectedDirection(Vector3.Zero,new(0,0,-10),Projection(eastCamera));
Check(rotatedDirection.HasValue && rotatedDirection.Value.X>.9f, "Changing camera yaw rotates route direction without an assumed yaw offset");
var highCamera = Matrix4x4.CreateLookAt(new(.1f,15,.1f),Vector3.Zero,Vector3.UnitY);
var highDirection = RouteMath.ProjectedDirection(Vector3.Zero,new(-10,0,0),Projection(highCamera));
Check(highDirection.HasValue && float.IsFinite(highDirection.Value.X) && float.IsFinite(highDirection.Value.Y),
    "Near top-down camera remains valid");
Check(RouteMath.ProjectedDirection(Vector3.Zero,new(0,0,-10),_=>null)==null,
    "Unavailable projection never fabricates a direction");
Check(RouteMath.ViewDirection(new(0,0,-10),Matrix4x4.Identity,-1) is { } rh && rh.Y<-.9f,
    "First-person fallback respects right-handed projection");
Check(RouteMath.ViewDirection(new(0,0,10),Matrix4x4.Identity,1) is { } lh && lh.Y<-.9f,
    "First-person fallback respects left-handed projection");
progress = RouteMath.Progress(corner,new(8,0,0));
Check(RouteMath.SteeringPoint(corner,progress,new(8,0,0))==new Vector3(10,0,0),
    "Arrow points to next corner rather than cutting through it");
progress = RouteMath.Progress(corner,new(6,0,-5));
Check(RouteMath.SteeringPoint(corner,progress,new(6,0,-5))==progress.Foot,
    "Off-route arrow points back to the nearest route segment");
Console.WriteLine($"PASS: {checks} total checks including actual camera projection and corner steering");
var drawnFoot = new Vector3(2,0,1);
var drawnCorner = new Vector3(-8,0,-14);
var projectedFoot = southProjection(drawnFoot+Vector3.UnitY*.15f)!.Value;
var projectedCorner = southProjection(drawnCorner+Vector3.UnitY*.15f)!.Value;
var drawnLineDirection = Vector2.Normalize(projectedCorner-projectedFoot);
var needleDirection = RouteMath.ProjectedDirection(drawnFoot,drawnCorner,southProjection);
Check(needleDirection.HasValue && Vector2.Dot(needleDirection.Value,drawnLineDirection)>.9999f,
    "Needle aligns with the exact screen segment drawn from route foot to next corner");
Console.WriteLine($"PASS: {checks} checks; projected needle aligns with drawn route");

// Native AreaMap measurements in Old Gridania, including pan, zoom,
// nonzero sheet offsets and scaled UI. No rotating marker bounds involved.
var mapCenter = new Vector2(992,674);
var mapOffset = new Vector2(340.68f,-176.94f);
var mapPlayer = new Vector2(170.34102f,-88.4735f);
var mapPin = RouteMath.AreaMapPoint(mapPlayer,mapCenter,mapOffset,Vector2.Zero,2,new(1.5357143f));
Check(Vector2.Distance(mapPin,mapCenter)<.02f, "Large map route starts at native player pin");
var eastPoint = RouteMath.AreaMapPoint(mapPlayer+new Vector2(10,0),mapCenter,mapOffset,Vector2.Zero,2,new(2));
Check(Vector2.Distance(eastPoint,mapCenter+new Vector2(40,0))<.02f, "Large map combines sheet scale and zoom");
var pan = new Vector2(80,-25);
var panned = RouteMath.AreaMapPoint(mapPlayer,mapCenter,mapOffset+pan,Vector2.Zero,2,new(2));
Check(Vector2.Distance(panned,mapCenter-pan*2)<.02f, "Map panning follows native offset");
var uiScaled = RouteMath.AreaMapPoint(mapPlayer+new Vector2(10,0),mapCenter,mapOffset,Vector2.Zero,2,new(3));
Check(Vector2.Distance(uiScaled,mapCenter+new Vector2(60,0))<.03f, "Large map route follows UI scaling");
var shifted = RouteMath.AreaMapPoint(new(5,7),new(100,200),new(20,30),new(10,-20),2,new(.5f));
Check(Vector2.Distance(shifted,new(105,172))<.001f, "Large map respects sheet coordinate offsets");
Console.WriteLine($"PASS: {checks} checks including large map projection");
foreach (var fixture in new[] {
    (World:new Vector2(170.34f,-88.47f),Offset:new Vector2(340.68f,-176.94f),Sheet:Vector2.Zero,Factor:2f,Scale:new Vector2(1.5357143f)),
    (World:new Vector2(-700,310),Offset:new Vector2(-400,120),Sheet:new Vector2(100,-200),Factor:.5f,Scale:new Vector2(.2857143f)),
    (World:new Vector2(25,-30),Offset:new Vector2(80,90),Sheet:new Vector2(15,42),Factor:4f,Scale:new Vector2(3,2)),
    (World:new Vector2(328.2f,290.1f),Offset:new Vector2(386.81f,258.87f),Sheet:Vector2.Zero,Factor:1f,Scale:new Vector2(1.5357143f)) })
{
    var screen = RouteMath.AreaMapPoint(fixture.World,mapCenter,fixture.Offset,fixture.Sheet,fixture.Factor,fixture.Scale);
    var restored = RouteMath.AreaMapWorld(screen,mapCenter,fixture.Offset,fixture.Sheet,fixture.Factor,fixture.Scale);
    Check(Vector2.Distance(restored,fixture.World)<.002f,"Map clicks invert zoom, pan, sheet offsets and UI scaling");
}
Console.WriteLine($"PASS: {checks} checks including custom map destinations");

// Regression: live rotating minimap in North Shroud. Match native marker
// positions without requiring any markers to compute the projection itself.
var rotatingMap = RouteMath.MinimapProjection(.75f,1,1,-1.3439035f)!.Value;
Check(Vector2.Distance(rotatingMap,new(.16866198f,-.7307914f))<.0002f,
    "Native compass rotation matches the previously measured marker fit");
var nativePlayer = new Vector2(266.07f,210.41f);
var measuredMarker = new Vector2(1800.8842f,191.83249f);
var nativeMarker = new Vector2(3742/16f,3680/16f);
Check(Vector2.Distance(new Vector2(1792,165)+RouteMath.Project(nativeMarker-nativePlayer,rotatingMap),measuredMarker)<.05f,
    "Native projection lands on a measured rotating minimap icon");
var northMap = RouteMath.MinimapProjection(.5f,2,1,0)!.Value;
Check(Vector2.Distance(northMap,Vector2.UnitX)<.0001f,
    "Sheet-scaled minimap retains world scale with north locked");
var zoomedMap = RouteMath.MinimapProjection(.75f,2,1.5f,MathF.PI/2)!.Value;
Check(Vector2.Distance(RouteMath.Project(new(10,0),zoomedMap),new(0,22.5f))<.001f,
    "Zoom, UI scale and quarter-turn rotation update together");
Check(RouteMath.MinimapProjection(0,1,1,0)==null && RouteMath.MinimapProjection(.75f,1,1,float.NaN)==null,
    "Loading or invalid native minimap data is rejected");
Check(Vector2.Distance(RouteMath.Project(new(0,-20),RouteMath.MinimapProjection(.75f,1,1,MathF.PI)!.Value),new(0,15))<.001f,
    "Turning around updates a rotating map even with no reference icons");
Console.WriteLine($"PASS: {checks} checks including stable native minimap projection");
Check(QuestLevels.Compare(12,7)==QuestDifficulty.VeryHigh,"Five levels above is red");
Check(QuestLevels.Compare(11,7)==QuestDifficulty.Higher,"Four levels above is orange");
Check(QuestLevels.Compare(10,7)==QuestDifficulty.Higher,"Three levels above is orange");
Check(QuestLevels.Compare(9,7)==QuestDifficulty.Suitable,"Two levels above remains suitable");
Check(QuestLevels.Compare(7,7)==QuestDifficulty.Suitable,"Matching character level is green");
Check(QuestLevels.Compare(5,7)==QuestDifficulty.Suitable,"Current level-five quest is suitable at level seven");
Check(QuestLevels.Compare(5,10)==QuestDifficulty.Suitable && QuestLevels.Compare(5,11)==QuestDifficulty.Low,
    "Level badge changes from green to gray as character levels up");
Check(QuestLevels.Compare(0,7)==QuestDifficulty.Unknown,"Missing quest level is not classified as easy");
Console.WriteLine($"PASS: {checks} checks including quest level difficulty");

var trail = new List<Vector3> { new(0,0,0), new(10,4,0), new(10,4,20) };
var trailPlayer = new Vector3(8,3.2f,0);
var trailProgress = RouteMath.Progress(trail,trailPlayer);
foreach(var trailRotation in new[] {0f,.7f,MathF.PI/2,MathF.PI,-1.3439035f})
{
    var mapProjection = RouteMath.MinimapProjection(.75f,1,1,trailRotation)!.Value;
    var expectedSegment = Vector2.Normalize(RouteMath.Project(RouteMath.Xz(trail[1]-trailProgress.Foot),mapProjection));
    var arrowSegment = RouteMath.MinimapRouteDirection(trail,trailProgress,trailPlayer,mapProjection);
    Check(arrowSegment.HasValue && Vector2.Dot(arrowSegment.Value,expectedSegment)>.99999f,
        "Arrow follows the next drawn minimap segment at every map rotation");
}
var steepTrail = new List<Vector3> { Vector3.Zero,new(10,100,0) };
var steepProgress = RouteMath.Progress(steepTrail,Vector3.Zero);
Check(RouteMath.MinimapRouteDirection(steepTrail,steepProgress,Vector3.Zero,Vector2.UnitX)==Vector2.UnitX,
    "Steep slopes cannot reverse an arrow aligned with the minimap trace");
var offTrail = new Vector3(4,0,-7);
var offTrailProgress = RouteMath.Progress(trail,offTrail);
var rejoinExpected = Vector2.Normalize(RouteMath.Xz(offTrailProgress.Foot-offTrail));
var rejoinArrow = RouteMath.MinimapRouteDirection(trail,offTrailProgress,offTrail,Vector2.UnitX);
Check(rejoinArrow.HasValue && Vector2.Dot(rejoinArrow.Value,rejoinExpected)>.99999f,
    "Leaving the trace points along the return connector to the path");
Check(RouteMath.MinimapRouteDirection([],default,Vector3.Zero,Vector2.UnitX)==null,
    "No fabricated trace direction without a route");
Console.WriteLine($"PASS: {checks} checks including arrow aligned with minimap trail");
Check(RewardMath.BaseExperience(400,65,4)==1040,"Base EXP for To the Bannock matches local Quest/ParamGrow data");
Check(RewardMath.BaseExperience(400,70,5)==1400,"Base EXP for Trial by Earth matches local Quest/ParamGrow data");
Check(RewardMath.BaseExperience(0,70,5)==0,"Quest without EXP keeps zero reward");
Check(RewardMath.BaseExperience(100,0,5)==0,"Zero scaling yields no base EXP");
Check(RewardMath.BaseExperience(65535,65535,255)==10951832373UL,"Base EXP uses wide arithmetic without overflow");
Check(RewardMath.BaseExperience(10,3,5)==1,"Base EXP follows integer truncation");
Check(RewardMath.LevelPercentage(1400,5600)==25,"Reward comparison uses the character's full level requirement");
Check(RewardMath.LevelPercentage(1400,0)==null,"No invented percentage at level cap or with missing growth data");
Check(RewardMath.LevelPercentage(2800,1400)==200,"Large rewards are not misleadingly capped at one level");
Console.WriteLine($"PASS: {checks} checks including quest reward calculations");
