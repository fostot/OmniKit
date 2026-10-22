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
Check(!physics.Active, "Closing the attached frame returns control to the safe roaming box.");
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
Check(!physics.Active, "Resizing a container below the sprite size safely detaches.");
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
Check(!physics.Active, "An unsupported drop with falling disabled returns to the saved roaming box.");
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
Check(physics.Placement == CompanionPlacement.Perched, "A falling companion catches a lower visible ledge.");
Console.WriteLine($"Companion frame physics: {checks} checks passed.");
