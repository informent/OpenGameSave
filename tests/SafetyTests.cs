using OpenGameSave;
using System.Text.Json;
var root = Path.Combine(Path.GetTempPath(), "opengamesave-safety-" + Guid.NewGuid().ToString("N"));
var snapshot = Path.Combine(root, "snapshot"); Directory.CreateDirectory(snapshot);
var manifest = new SaveSnapshot("safe", DateTime.Now, "Test", root, new[] { new SaveFile("..\\outside.txt", 0, "") });
File.WriteAllText(Path.Combine(snapshot, "manifest.json"), JsonSerializer.Serialize(manifest));
try { SaveEngine.Verify(snapshot); throw new Exception("Unsafe manifest was accepted."); } catch (InvalidDataException) { }
Directory.Delete(root, true); Console.WriteLine("PASS: unsafe snapshot paths are rejected");
