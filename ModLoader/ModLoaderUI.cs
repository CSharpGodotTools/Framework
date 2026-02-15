using Godot;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Framework.UI;

public class ModLoaderUI
{
    private readonly Dictionary<string, ModInfo> _mods = [];

    public Dictionary<string, ModInfo> GetMods()
    {
        return _mods;
    }

    public void LoadMods(Node node)
    {
        _mods.Clear();

        string modsPath = ProjectSettings.GlobalizePath("res://Mods");

        // Ensure "Mods" directory always exists
        Directory.CreateDirectory(modsPath);

        DirAccess dir = DirAccess.Open(modsPath);

        if (dir == null)
        {
            GameFramework.Logger.LogWarning("Failed to open Mods directory because it does not exist");
            return;
        }

        dir.ListDirBegin();

        string filename = dir.GetNext();

        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        while (filename != "")
        {
            if (!dir.CurrentIsDir())
            {
                goto Next;
            }

            string modRoot = $@"{modsPath}/{filename}";
            string modJson = $@"{modRoot}/mod.json";

            if (!File.Exists(modJson))
            {
                GameFramework.Logger.LogWarning($"The mod folder '{filename}' does not have a mod.json so it will not be loaded");
                goto Next;
            }

            string jsonFileContents = File.ReadAllText(modJson);

            jsonFileContents = jsonFileContents.Replace("*", "Any");

            if (!TryDeserializeModInfo(modJson, jsonFileContents, options, out ModInfo modInfo))
            {
                goto Next;
            }

            modInfo.Normalize();

            if (string.IsNullOrWhiteSpace(modInfo.Id))
            {
                GameFramework.Logger.LogWarning($"The mod folder '{filename}' has an invalid or empty id and will be skipped");
                goto Next;
            }

            if (_mods.ContainsKey(modInfo.Id))
            {
                GameFramework.Logger.LogWarning($"Duplicate mod id '{modInfo.Id}' was skipped");
                goto Next;
            }

            _mods.Add(modInfo.Id, modInfo);

            // Load dll
            string dllPath = $@"{modRoot}/Mod.dll";

            if (File.Exists(dllPath))
            {
                AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(typeof(Godot.Bridge.ScriptManagerBridge).Assembly);
                Assembly assembly = context.LoadFromAssemblyPath(dllPath);
                Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);
            }

            // Load pck
            string pckPath = $@"{modRoot}/mod.pck";

            if (File.Exists(pckPath))
            {
                bool success = ProjectSettings.LoadResourcePack(pckPath, replaceFiles: true);

                if (!success)
                {
                    GameFramework.Logger.LogWarning($"Failed to load pck file for mod '{modInfo.Name}'");
                    goto Next;
                }

                string modScenePath = $"res://{modInfo.Author}/{modInfo.Id}/mod.tscn";

                PackedScene importedScene = ResourceLoader.Load<PackedScene>(modScenePath);

                if (importedScene == null)
                {
                    GameFramework.Logger.LogWarning($"Failed to load mod.tscn for mod '{modInfo.Name}'");
                    goto Next;
                }

                Node mod = importedScene.Instantiate<Node>();
                node.GetTree().Root.CallDeferred(Node.MethodName.AddChild, mod);
            }

        Next:
            filename = dir.GetNext();
        }

        dir.ListDirEnd();
        dir.Dispose();
    }

    private static bool TryDeserializeModInfo(
        string modJsonPath,
        string jsonFileContents,
        JsonSerializerOptions options,
        out ModInfo modInfo)
    {
        try
        {
            modInfo = JsonSerializer.Deserialize<ModInfo>(jsonFileContents, options);
        }
        catch (JsonException exception)
        {
            GameFramework.Logger.LogWarning($"Failed to parse '{modJsonPath}': {exception.Message}");
            modInfo = new ModInfo();
            return false;
        }

        if (modInfo != null)
        {
            return true;
        }

        GameFramework.Logger.LogWarning($"The file '{modJsonPath}' is empty or malformed and was skipped");
        modInfo = new ModInfo();
        return false;
    }
}

public class ModInfo
{
    public string Name        { get; set; } = string.Empty;
    public string Id          { get; set; } = string.Empty;
    public string ModVersion  { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author      { get; set; } = string.Empty;

    public Dictionary<string, string> Dependencies      { get; set; } = [];
    public Dictionary<string, string> Incompatibilities { get; set; } = [];

    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? Id : Name;
        Author = string.IsNullOrWhiteSpace(Author) ? "Unknown" : Author;
        ModVersion = string.IsNullOrWhiteSpace(ModVersion) ? "Unknown" : ModVersion;
        GameVersion = string.IsNullOrWhiteSpace(GameVersion) ? "Unknown" : GameVersion;
        Description ??= string.Empty;
        Dependencies ??= [];
        Incompatibilities ??= [];
    }
}
