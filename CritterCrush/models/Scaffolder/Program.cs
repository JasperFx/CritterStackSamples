using Bobcat.EventModel;
using Bobcat.EventModel.Scaffolding;

// Regenerates CritterCrush from models/CritterCrush.emodel.yaml. Model in, files out, nothing in
// between — the patch step that used to sit here is gone, and every gap it stood in for closed
// upstream (see models/README.md).
//
// ⚠️ --arrangements is NOT optional for THIS chapter, whatever its name suggests. The committed
// BookingAppointments.feature was generated with it (bobcat#259): ten of thirteen scenarios shared
// the same arranged history, and it is now three named @arrangement scenarios they reference.
// Regenerating without the flag silently inlines that history back into every scenario — a green
// but much worse file. It stays a flag because upstream makes it one, and because a chapter with
// no repeated history gains nothing from it.

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run --project models/Scaffolder -- <model.yaml> <out-dir> [--arrangements] [--plan <plan.yaml>] [--manifest <spec-ownership.yaml>]");
    return 2;
}

var (modelPath, outDir) = (args[0], args[1]);
var arrangements = args.Contains("--arrangements");
var planIndex = Array.IndexOf(args, "--plan");
var planPath = planIndex >= 0 && planIndex + 1 < args.Length ? args[planIndex + 1] : null;
var manifestIndex = Array.IndexOf(args, "--manifest");
var manifestPath = manifestIndex >= 0 && manifestIndex + 1 < args.Length ? args[manifestIndex + 1] : null;

var reading = CuratedModelReader.Read(File.ReadAllText(modelPath));
foreach (var warning in reading.Warnings) Console.Error.WriteLine($"model warning: {warning}");
foreach (var problem in reading.Problems) Console.Error.WriteLine($"problem: {problem}");
if (reading.File is null) return 1;

// The spec-ownership manifest: which slices are specified somewhere other than a .feature. Its
// problems are fatal for the same reason the model's are — a manifest that does not validate would
// otherwise change what gets scaffolded while pointing at slices that may not exist. In particular
// an integration + projected slice that never states `scaffold:` is reported here rather than
// silently falling back to writing nothing, which is how eighteen slices lost their skeletons
// before bobcat#334.
var ownership = SpecOwnershipPlan.None;
if (manifestPath is not null)
{
    var manifest = SpecOwnershipReader.Read(File.ReadAllText(manifestPath), reading.File);
    foreach (var warning in manifest.Warnings) Console.Error.WriteLine($"manifest warning: {warning}");
    foreach (var problem in manifest.Problems) Console.Error.WriteLine($"manifest problem: {problem}");
    if (!manifest.Succeeded) return 1;

    ownership = SpecOwnershipPlan.For(manifest.File);
}

// ScaffoldAll is the single door: the pieces are not independent, and skipping one leaves a
// dangling type that fails the whole project. The features are then re-emitted over the top when
// arrangements are asked for, because that overload is the only way to ask.
var files = new Dictionary<string, string>(SliceScaffolder.ScaffoldAll(reading.File, ownership));
if (arrangements)
{
    foreach (var (path, content) in SliceScaffolder.ScaffoldFeatures(reading.File, arrangements: true, ownership))
    {
        files[path] = content;
    }
}

foreach (var (path, content) in files.OrderBy(x => x.Key, StringComparer.Ordinal))
{
    var target = Path.Combine(outDir, path);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, content);
    Console.WriteLine(path);
}

// The Stoat plan is derived too, for the reason the last hand-written one rotted: every scenario
// name is a spec identity, and a re-scaffold that renames a scenario leaves a gate nothing can ever
// open. Deriving it means the plan cannot disagree with the model it came from.
if (planPath is not null)
{
    File.WriteAllText(planPath, StoatPlan.From(reading.File));
    Console.WriteLine($"plan -> {planPath}");
}

Console.WriteLine();
Console.WriteLine($"{files.Count} file(s) into {outDir}. Copy the .feature over the committed one;");
Console.WriteLine("copy a slice's .cs ONLY if that slice is still unimplemented — regenerating a");
Console.WriteLine("filled-in slice overwrites the work.");
return 0;
