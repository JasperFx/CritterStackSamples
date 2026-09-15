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
    Console.Error.WriteLine("usage: dotnet run --project models/Scaffolder -- <model.yaml> <out-dir> [--arrangements]");
    return 2;
}

var (modelPath, outDir) = (args[0], args[1]);
var arrangements = args.Contains("--arrangements");

var reading = CuratedModelReader.Read(File.ReadAllText(modelPath));
foreach (var problem in reading.Problems) Console.Error.WriteLine($"problem: {problem}");
if (reading.File is null) return 1;

// ScaffoldAll is the single door: the pieces are not independent, and skipping one leaves a
// dangling type that fails the whole project. The features are then re-emitted over the top when
// arrangements are asked for, because that overload is the only way to ask.
var files = new Dictionary<string, string>(SliceScaffolder.ScaffoldAll(reading.File));
if (arrangements)
{
    foreach (var (path, content) in SliceScaffolder.ScaffoldFeatures(reading.File, arrangements: true))
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

Console.WriteLine();
Console.WriteLine($"{files.Count} file(s) into {outDir}. Copy the .feature over the committed one;");
Console.WriteLine("copy a slice's .cs ONLY if that slice is still unimplemented — regenerating a");
Console.WriteLine("filled-in slice overwrites the work.");
return 0;
