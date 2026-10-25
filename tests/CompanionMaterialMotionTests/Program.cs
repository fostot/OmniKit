using System.Numerics;
using Dalamud.Bindings.ImGui;

using var f = new Fixture();
if (args.Contains("--reactions-only"))
{
    NativeCases.RunReactions(f, args.FirstOrDefault(argument => argument.StartsWith("--reaction-look=", StringComparison.Ordinal))?[16..]);
    Console.WriteLine($"Companion maximum-control reactions: {f.Checks} focused checks passed;156looks,96/192,bothfacings,fourstridephases,positive/negativeextremes. No game/framework/provider calls.");
    return;
}
var origin = new Vector2(180); var size = new Vector2(192);
var avatars = f.Looks.Select(look => f.Text(look, "Avatar")).Distinct().ToArray();
f.Check(avatars.Length == 13, "All thirteen species participate in the costume matrix.");
var mapped = ((System.Collections.IEnumerable)f.Type("CompanionMotionProfiles").GetProperty("MappedLooks")!.GetValue(null)!)
    .Cast<object>().Select(key => f.Get(key, "Item1") + "/" + f.Get(key, "Item2")).ToHashSet(StringComparer.Ordinal);
var authored = f.Looks.Select(look => f.Text(look, "Avatar") + "/" + f.Text(look, "Id")).ToHashSet(StringComparer.Ordinal);
f.Check(mapped.Count == 156 && mapped.SetEquals(authored), "Profile keys cover the exact authored156 look IDs without label guesses or omissions.");
string Material(object state) => f.Get(f.Get(state, "Profile"), "Material").ToString()!;
foreach (var avatar in avatars)
{
    var original = f.Motion(avatar, null); var reset = f.Motion(avatar, null);
    var alternate = f.Looks.First(look => f.Text(look, "Avatar") == avatar);
    f.Call(reset, "SetLook", avatar, f.Text(alternate, "Id")); f.Call(reset, "SetLook", avatar, null);
    f.Check(Material(original) == "Species" && Material(reset) == "Species", "Default and reset-to-default retain the species profile: " + avatar);
    f.Check(new[] { "Tempo", "Strength", "Lag" }.All(name => (float)f.Get(f.Get(original, "Profile"), name) == 1), "Species defaults retain unit motion parameters: " + avatar);
    f.Check((bool)f.Call(reset, "UsesRoll", avatar)! == (avatar == "Moonblob"), "Default rolling remains exclusive to Moonblob.");
    var stable = true;
    foreach (var point in new[] { new Vector2(.25f, .25f), new Vector2(.75f, .5f), new Vector2(.5f, .85f) })
    foreach (var phase in new[] { 0f, .25f, .75f })
        stable &= f.Warp(original, avatar, origin + point * size, origin, size, phase, false)
            == f.Warp(reset, avatar, origin + point * size, origin, size, phase, false);
    f.Check(stable, "Choosing and clearing a look does not contaminate default motion state: " + avatar);
}

(string Avatar, string Id, string Material, bool Rolls)[] fixtures = [
    ("Moonblob", "OceanOpal", "Fluid", true), ("Moonblob", "LavenderGeode", "Crystal", false),
    ("Wink", "TideGlass", "Crystal", false), ("Wink", "TwilightVelvet", "Silk", false),
    ("Puff", "PeachParfait", "Cream", false), ("Puff", "RainPearl", "Bubble", false),
    ("Ember", "Moonfire", "Flame", false), ("Ember", "VioletWisp", "Wisp", false)];
foreach (var item in fixtures)
{
    var state = f.Motion(item.Avatar, item.Id);
    f.Check(Material(state) == item.Material && (bool)f.Call(state, "UsesRoll", item.Avatar)! == item.Rolls,
        "Reviewed look resolves its intended material and rolling policy: " + item.Avatar + "/" + item.Id);
}
foreach (var pair in new[] { ("Moonblob", "OceanOpal", "LavenderGeode"), ("Wink", "TideGlass", "TwilightVelvet"),
    ("Puff", "PeachParfait", "RainPearl"), ("Ember", "Moonfire", "VioletWisp") })
{
    var first = f.Motion(pair.Item1, pair.Item2); var second = f.Motion(pair.Item1, pair.Item3); var difference = 0f;
    foreach (var phase in new[] { 0f, .25f, .5f, .75f })
    foreach (var point in new[] { new Vector2(.2f, .2f), new Vector2(.8f, .2f), new Vector2(.3f, .65f), new Vector2(.7f, .85f) })
        difference += Vector2.Distance(f.Warp(first, pair.Item1, origin + point * size, origin, size, phase, false),
            f.Warp(second, pair.Item1, origin + point * size, origin, size, phase, false));
    f.Check(difference > 4, pair.Item2 + "/" + pair.Item3 + " produce measurably different material motion, not merely different profile labels.");
}
foreach (var pair in new[] { ("Moonblob", "OceanOpal"), ("Wink", "TwilightVelvet"), ("Puff", "PeachParfait"), ("Ember", "Moonfire") })
{
    var soft = f.Motion(pair.Item1, pair.Item2);
    var widths = new[] { 0f, .25f, .5f, .75f }.Select(phase => Vector2.Distance(
        f.Warp(soft, pair.Item1, origin + size * new Vector2(.25f, .5f), origin, size, phase, false),
        f.Warp(soft, pair.Item1, origin + size * new Vector2(.75f, .5f), origin, size, phase, false))).ToArray();
    f.Check(widths.Max() - widths.Min() > size.X * .01f, pair.Item2 + " changes its soft body's width during travel while native mesh-area checks preserve mass.");
}

foreach (var look in f.Looks)
{
    var avatar = f.Text(look, "Avatar"); var id = f.Text(look, "Id"); var state = f.Motion(avatar, id);
    var label = avatar + "/" + id;
    var profile = f.Get(state, "Profile");
    // Deliberately install another species/material before the runtime paints.
    // This catches omitted SetLook calls or passing Choice rather than the authored Id.
    var seeded = f.Motion("Moonblob", "OceanOpal"); f.Call(seeded, "SetLook", "Wink", "TwilightVelvet");
    ImGui.NewFrame(); f.Draw(look, seeded, ImGui.GetForegroundDrawList(), origin, 96, false, .375f); ImGui.Render();
    f.Check(profile.Equals(f.Get(seeded, "Profile")), label + " actual runtime selects the authored look Id before drawing.");
    f.Check(new[] { "Tempo", "Strength", "Lag" }.All(name => float.IsFinite((float)f.Get(profile, name)) && (float)f.Get(profile, name) > 0), label + " has finite positive material parameters.");
    if (avatar == "Orbit")
    {
        Matrix3x2 Matrix(float phase, bool mirror, bool animate = true) => (Matrix3x2)f.Call(state, "OrbitMovement", origin, 72f, phase, mirror, animate)!;
        var matrix = Matrix(.375f, false); var x = Vector2.TransformNormal(Vector2.UnitX, matrix); var y = Vector2.TransformNormal(Vector2.UnitY, matrix);
        f.Check(Math.Abs(x.Length() - 1) < .00001f && Math.Abs(y.Length() - 1) < .00001f && Math.Abs(Vector2.Dot(x, y)) < .00001f,
            label + " keeps a rigid planet/hat/ring matrix, never a body mesh squash.");
        f.Check(Matrix(.375f, false, false) == Matrix3x2.Identity && Matrix(.375f, false) == matrix, label + " disabled/static matrix is deterministic.");
        var idle = f.Motion(avatar, id, active: false); var idleBefore = (Matrix3x2)f.Call(idle, "OrbitMovement", origin, 72f, .375f, false, true)!;
        for (var tick = 0; tick < 12; tick++) f.Call(idle, "Update", 1f / 60, true, false);
        var idleAfter = (Matrix3x2)f.Call(idle, "OrbitMovement", origin, 72f, .375f, false, true)!;
        if (Material(state) != "Species") f.Check(idleBefore != idleAfter, label + " material-specific idle breath advances without travel.");
        continue;
    }
    if (Material(state) is "Crystal" or "Stone" or "Metal")
    {
        var center = origin + size * .5f; var at = f.Warp(state, avatar, center, origin, size, .375f, false);
        var x = f.Warp(state, avatar, center + new Vector2(20, 0), origin, size, .375f, false) - at;
        var y = f.Warp(state, avatar, center + new Vector2(0, 20), origin, size, .375f, false) - at;
        f.Check(Math.Abs(x.Length() - y.Length()) < .001f && Math.Abs(Vector2.Dot(x, y)) < .02f,
            label + " rigid material keeps perpendicular equal-scale axes instead of a jelly squeeze.");
        f.Check(Math.Abs(x.Length() / 20 - 1) < .001f && Math.Abs(y.Length() / 20 - 1) < .001f,
            label + " rigid point-pair distances stay within0.1% of their source length.");
    }
    if (Material(state) != "Species")
    {
        var idle = f.Motion(avatar, id, active: false); var sample = origin + size * new Vector2(.25f, .25f);
        var idleBefore = f.Warp(idle, avatar, sample, origin, size, .375f, false);
        for (var tick = 0; tick < 12; tick++) f.Call(idle, "Update", 1f / 60, true, false);
        f.Check(Vector2.Distance(idleBefore, f.Warp(idle, avatar, sample, origin, size, .375f, false)) > .001f,
            label + " material-specific idle expression advances instead of freezing the body.");
    }
    var seam = true; var disabled = true; var finite = true;
    foreach (var mirror in new[] { false, true })
    foreach (var point in new[] { new Vector2(.15f, .12f), new Vector2(.5f, .4f), new Vector2(.8f, .75f), new Vector2(.5f, .9f) })
    {
        var p = origin + point * size;
        seam &= Vector2.Distance(f.Warp(state, avatar, p, origin, size, 0, mirror), f.Warp(state, avatar, p, origin, size, 1, mirror)) < .001f;
        disabled &= f.Warp(state, avatar, p, origin, size, .375f, mirror, false) == p;
        for (var step = 0; step < 8; step++)
        { var warped = f.Warp(state, avatar, p, origin, size, step / 8f, mirror); finite &= float.IsFinite(warped.X) && float.IsFinite(warped.Y); }
    }
    f.Check(seam && disabled && finite, label + " is finite, phase-periodic and exactly still when animation is disabled.");
    var previous = f.Warp(state, avatar, origin + size * new Vector2(.25f, .25f), origin, size, .375f, false); var continuous = true;
    // One bounded stopping interval, including the idle expression after travel decays.
    for (var tick = 0; tick < 90; tick++)
    {
        f.Call(state, "Update", 1f / 60, true, false);
        var current = f.Warp(state, avatar, origin + size * new Vector2(.25f, .25f), origin, size, .375f, false);
        continuous &= Vector2.Distance(previous, current) < size.X * .035f; previous = current;
    }
    f.Check(continuous && (float)f.Get(state, "Activity") < .00001f, label + " settles continuously after travel without freezing mid-pulse.");
    // Fractional material tempo must not jump when the old species clock wraps at2pi seconds.
    var boundary = f.Motion(avatar, id, seconds: 6.25f); var p0 = origin + size * new Vector2(.25f, .2f);
    var before = f.Warp(boundary, avatar, p0, origin, size, .375f, false); var wrapContinuous = true;
    for (var tick = 0; tick < 6; tick++)
    {
        f.Call(boundary, "Update", 1f / 60, true, true); var after = f.Warp(boundary, avatar, p0, origin, size, .375f, false);
        wrapContinuous &= Vector2.Distance(before, after) < size.X * .035f; before = after;
    }
    f.Check(wrapContinuous, label + " has no fractional-tempo clock seam.");
}

NativeCases.Run(f);
Console.WriteLine($"Companion material motion: {f.Checks} checks passed;156 looks,13 default resets, native96/192 both facings, material settling and eye/body integration. No game/framework/provider calls.");
