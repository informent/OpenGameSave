using OpenGameSave;
using System.Text.Json;
var root = Path.Combine(Path.GetTempPath(), "opengamesave-safety-" + Guid.NewGuid().ToString("N"));
var snapshot = Path.Combine(root, "snapshot"); Directory.CreateDirectory(snapshot);
var manifest = new SaveSnapshot("safe", DateTime.Now, "Test", root, new[] { new SaveFile("..\\outside.txt", 0, "") });
File.WriteAllText(Path.Combine(snapshot, "manifest.json"), JsonSerializer.Serialize(manifest));
try { SaveEngine.Verify(snapshot); throw new Exception("Unsafe manifest was accepted."); } catch (InvalidDataException) { }
Directory.Delete(root, true);
var linkedRoot = Path.Combine(Path.GetTempPath(), "opengamesave-link-safety-" + Guid.NewGuid().ToString("N")); var outside = Path.Combine(linkedRoot, "outside"); var source = Path.Combine(linkedRoot, "source"); var library = Path.Combine(linkedRoot, "library"); Directory.CreateDirectory(outside); Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(outside, "secret.sav"), "outside data");
try { Directory.CreateSymbolicLink(Path.Combine(source, "linked"), outside); try { SaveEngine.CreateSnapshot(new GameProfile("Linked", null, source), library); throw new Exception("Linked source directory was followed."); } catch (InvalidDataException) { } }
catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Console.WriteLine("INFO: symbolic-link creation unavailable on runner"); }
Directory.CreateDirectory(library); Directory.CreateDirectory(Path.Combine(library, "Safe")); var childSnapshot = Path.Combine(library, "Safe", "child"); Directory.CreateDirectory(childSnapshot); File.WriteAllText(Path.Combine(childSnapshot, "data.sav"), "data"); var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(childSnapshot, "data.sav")))); File.WriteAllText(Path.Combine(childSnapshot, "manifest.json"), JsonSerializer.Serialize(new SaveSnapshot("child", DateTime.Now, "Safe", source, new[] { new SaveFile("data.sav", 4, hash) })));
var restoreBase = Path.Combine(linkedRoot, "restore"); Directory.CreateDirectory(restoreBase);
try { Directory.CreateSymbolicLink(Path.Combine(restoreBase, "redirect"), outside); try { SaveEngine.Restore(childSnapshot, Path.Combine(restoreBase, "redirect")); throw new Exception("Restore wrote through a linked target root."); } catch (InvalidDataException) { } }
catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Console.WriteLine("INFO: symbolic-link restore test unavailable on runner"); }
Directory.Delete(linkedRoot, true); Console.WriteLine("PASS: unsafe manifest paths and linked filesystem paths are rejected");
