using System.Numerics;
using OmniKit.Ui;

var checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
var viewportMin = new Vector2(100, 50);
var viewportMax = new Vector2(900, 650);
var size = new Vector2(96);
var surface = new CompanionSurface(7, new Vector2(250, 250), new Vector2(600, 550), 28, 10);
var surfaces = new[] { surface };
var options = new CompanionPhysicsOptions(true, true, true);
var physics = new CompanionPhysics();
void Tick(float dt = .05f, bool animate = true) => physics.Update(surfaces, viewportMin, viewportMax, size, dt, animate, false, options);
void PickAndDrop(Vector2 pointer)
{
    physics.PickUp(pointer - size * .5f, pointer, false);
    physics.Carry(pointer, viewportMin, viewportMax, size, .016f);
    physics.Drop(pointer, surfaces, viewportMin, viewportMax, size, options);
    Tick(0);
}
PickAndDrop(new Vector2(400, 260));
Check(physics.Placement == CompanionPlacement.Perched, "Dropping on a header perches on its top.");
Check(Math.Abs(physics.Position.Y + size.Y * .9f - surface.Minimum.Y) < .001f, "Feet rest exactly on the ledge.");
var oldPosition = physics.Position;
surfaces[0] = surface with { Minimum = surface.Minimum + new Vector2(25, 15), Maximum = surface.Maximum + new Vector2(25, 15) };
Tick(0);
Check(physics.Position == oldPosition + new Vector2(25, 15), "A perched companion follows moved surfaces even without elapsed time.");
physics.Update([], viewportMin, viewportMax, size, .016f, true, false, options);
Check(physics.Placement == CompanionPlacement.Falling && physics.Position.Y >= oldPosition.Y,
    "Closing the attached frame starts a fall without snapping to the saved area.");
surfaces[0] = surface;
PickAndDrop(new Vector2(400, 400));
Check(physics.Placement == CompanionPlacement.Contained, "Dropping into the frame body contains the companion.");
var sawPush = false;
for (var i = 0; i < 900; i++)
{
    Tick();
    sawPush |= physics.Pose == CompanionPose.Push;
    Check(physics.Position.X >= physics.ClipMinimum.X && physics.Position.Y >= physics.ClipMinimum.Y
        && physics.Position.X + size.X <= physics.ClipMaximum.X && physics.Position.Y + size.Y <= physics.ClipMaximum.Y,
        "Contained movement never clips outside the observed interior.");
}
Check(sawPush, "Reaching a contained edge produces a pushing reaction.");
surfaces[0] = surface with { Maximum = surface.Minimum + new Vector2(50) };
Tick();
Check(physics.Placement == CompanionPlacement.Falling, "Resizing a container below the sprite size safely detaches into a fall.");
surfaces[0] = surface;
PickAndDrop(new Vector2(590, 260));
var sawFall = false;
var sawLand = false;
for (var i = 0; i < 700; i++) { Tick(); sawFall |= physics.Pose == CompanionPose.Fall; sawLand |= physics.Pose == CompanionPose.Land; }
Check(sawFall && sawLand, "Walking past an enabled ledge edge falls and lands.");
Check(physics.Placement == CompanionPlacement.Floor, "A fall without a lower ledge lands on the viewport floor.");
var reducedViewport = new Vector2(650, 400);
physics.Update([], viewportMin, reducedViewport, size, .016f, false, false, options);
Check(physics.Position.X + size.X <= reducedViewport.X && physics.Position.Y + size.Y <= reducedViewport.Y,
    "A viewport resize still bounds a frozen companion on the floor.");
physics.PickUp(new Vector2(300, 100), new Vector2(330, 130), false);
physics.Carry(new Vector2(-1000, 5000), viewportMin, viewportMax, size, .01f);
Check(physics.Position == new Vector2(viewportMin.X, viewportMax.Y - size.Y), "Dragging remains inside the game viewport.");
physics.Drop(new Vector2(800, 100), surfaces, viewportMin, viewportMax, size, new CompanionPhysicsOptions(false, false, false));
Check(physics.Placement == CompanionPlacement.Returning, "An unsupported drop with falling disabled begins a continuous walk home.");
physics.PickUp(new Vector2(330, 60), new Vector2(360, 90), false);
physics.Drop(new Vector2(360, 90), surfaces, viewportMin, viewportMax, size, options);
var before = physics.Position;
Tick(1000);
Check(physics.Position.Y - before.Y <= 3, "A long frame stall is clamped and cannot tunnel through surfaces.");
before = physics.Position;
Tick(.05f, false);
Check(physics.Position == before && physics.Pose == CompanionPose.Idle, "Animations disabled freezes physics and reaction playback.");
physics.Update(surfaces, viewportMin, viewportMax, size, .05f, true, true, options);
Check(physics.Position.Y > before.Y, "Hovering cannot suspend a falling companion in midair.");
for (var i = 0; i < 200 && physics.Placement == CompanionPlacement.Falling; i++) Tick();
Check(physics.Placement == CompanionPlacement.Floor, "A fall passes lower frames and reaches the actual viewport floor.");

var home = new Vector2(720, 180);
var sawReturn = false;
var reachedFloor = false;
PickAndDrop(new Vector2(590, 260));
for (var i = 0; i < 2200 && physics.Active; i++)
{
    before = physics.Position;
    var wasReturning = physics.Placement == CompanionPlacement.Returning;
    physics.Update(surfaces, viewportMin, viewportMax, size, 1f / 30, true, false, options, home);
    reachedFloor |= physics.Placement == CompanionPlacement.Floor && Math.Abs(physics.Position.Y + size.Y - viewportMax.Y) < .001f;
    sawReturn |= physics.Placement == CompanionPlacement.Returning;
    if (wasReturning)
        Check(Vector2.Distance(before, physics.Position) <= size.Y * .34f / 30 + .001f, "Returning uses bounded continuous movement, including its handoff.");
    Check(physics.Position.X >= viewportMin.X && physics.Position.Y >= viewportMin.Y
        && physics.Position.X + size.X <= viewportMax.X + .001f && physics.Position.Y + size.Y <= viewportMax.Y + .001f,
        "Pickup, fall and return keep the complete sprite cell inside the viewport.");
}
Check(reachedFloor && sawReturn && !physics.Active && Vector2.Distance(physics.Position, home) < .001f,
    "Walking off a titlebar lands at the bottom, walks home, and hands back at the exact roaming position.");

PickAndDrop(new Vector2(400, 260));
var sawTurn = false; var oldFacing = physics.FacingLeft;
for (var i = 0; i < 70; i++) { Tick(); sawTurn |= physics.FacingLeft != oldFacing; }
Check(sawTurn, "A companion can pause and turn while exploring a titlebar.");

// The foremost complete frame owns a drop; a small overlay must not cause a
// companion to attach to a larger window hidden beneath it.
var overlapping = new[] { surface, surface with { Id = 8, Order = 99 } };
physics.PickUp(new Vector2(350, 350), new Vector2(400, 400), false);
physics.Drop(new Vector2(400, 400), overlapping, viewportMin, viewportMax, size, options);
Check(physics.AttachedSurfaceId == 8, "Drop selection honors observed frame stacking order.");
before = physics.Position;
physics.Update(overlapping, viewportMin, viewportMax, size, .05f, false, false, options, home);
Check(physics.Position == before, "Disabling animations preserves contained placement and freezes movement.");

foreach (var box in new[] { new CompanionLayout(viewportMin, viewportMax, size), new CompanionLayout(viewportMin, viewportMin + size, size) })
{
    var nearby = CompanionPhysics.HomeArea(box, true);
    Check(nearby.SpriteSize == box.SpriteSize && nearby.Minimum.X >= box.Minimum.X && nearby.Minimum.Y >= box.Minimum.Y
        && nearby.Maximum.X <= box.Maximum.X && nearby.Maximum.Y <= box.Maximum.Y
        && nearby.Maximum.X - nearby.Minimum.X >= size.X && nearby.Maximum.Y - nearby.Minimum.Y >= size.Y,
        "The home area preserves the approved sprite size and fits inside even a one-cell saved box.");
    Check(CompanionPhysics.HomeArea(box, false) == box, "Disabling home preserves the original roaming box.");
    var (houseMin, houseMax) = CompanionPhysics.HomeBounds(box);
    Check(houseMin.X >= box.Minimum.X && houseMin.Y >= box.Minimum.Y && houseMax.X <= box.Maximum.X
        && houseMax.Y <= box.Maximum.Y && houseMax.X > houseMin.X && houseMax.Y > houseMin.Y,
        "The whole house image stays inside a large or one-cell saved area.");
}
foreach (var delta in new[] { new Vector2(-5), new Vector2(5), new Vector2(.1f, -.1f), Vector2.Zero })
{
    var box = new Vector4(.72f, .55f, .95f, .92f);
    var moved = CompanionPhysics.TranslateHomeBox(box, delta);
    Check(moved.X >= 0 && moved.Y >= 0 && moved.Z <= 1 && moved.W <= 1
        && Math.Abs((moved.Z - moved.X) - (box.Z - box.X)) < .00001f
        && Math.Abs((moved.W - moved.Y) - (box.W - box.Y)) < .00001f,
        "Dragging home translates and clamps the whole saved box without resizing it.");
}
Check(CompanionPhysics.HomeFacesLeft(800, 100, 800, false), "A right-side house faces inward to the left.");
Check(!CompanionPhysics.HomeFacesLeft(200, 100, 800, true), "A left-side house faces inward to the right.");
foreach (var x in new[] { 490f, 500f, 510f })
    Check(CompanionPhysics.HomeFacesLeft(x, 100, 800, true) && !CompanionPhysics.HomeFacesLeft(x, 100, 800, false),
        "A center dead band preserves house orientation without flip jitter.");
Console.WriteLine($"Companion frame physics: {checks} checks passed.");
