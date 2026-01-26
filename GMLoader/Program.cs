#region Using Directives
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using UndertaleModLib.Util;
using Config.Net;
using ImageMagick;
using Underanalyzer.Compiler;
using Underanalyzer.Decompiler;
using System.Text;
using VYaml.Serialization;
using VYaml.Annotations;
using UndertaleModLib.Compiler;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.IO;
#endregion

namespace GMLoader;

#region Events and Logging
public enum GMLogLevel { Debug, Info, Warning, Error }

public delegate void GMLoaderLogHandler(GMLogLevel level, string message);

/// <summary>
/// Custom Serilog sink that routes to the event
/// </summary>
internal class CallbackSink : ILogEventSink
{
    private readonly GMLoaderLogHandler _callback;

    public CallbackSink(GMLoaderLogHandler callback) => _callback = callback;

    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Debug or LogEventLevel.Verbose => GMLogLevel.Debug,
            LogEventLevel.Information => GMLogLevel.Info,
            LogEventLevel.Warning => GMLogLevel.Warning,
            _ => GMLogLevel.Error
        };

        _callback?.Invoke(level, logEvent.RenderMessage());

        if (logEvent.Exception != null)
            _callback?.Invoke(GMLogLevel.Error, logEvent.Exception.ToString());
    }
}

public class GMLoaderResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }

    public static GMLoaderResult Ok() => new() { Success = true };
    public static GMLoaderResult Fail(string message, Exception? ex = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        Exception = ex
    };
}
#endregion

public interface IConfig
{
    public bool AutoGameStart { get; }
    public string TextureExclusion { get; }
    public string GameExecutable { get; }
    public string ImportPreCSX { get; }
    public string ImportBuiltinCSX { get; }
    public string ImportPostCSX { get; }
    public string ImportAfterCSX { get; }
    public string GameData { get; }
    public string ModsDirectory { get; }
    public string TexturesDirectory { get; }
    public string BackgroundTextureDirectory { get; }
    public string NoStripTexturesDirectory { get; }
    public string TexturesConfigDirectory { get; }
    public string BackgroundsConfigDirectory { get; }
    public string AudioDirectory { get; }
    public string AudioConfigDirectory { get; }
    public string ShaderDirectory { get; }
    public string ConfigDirectory { get; }
    public string GMLCodeDirectory { get; }
    public string GMLCodePatchDirectory { get; }
    public string CollisionDirectory { get; }
    public string ASMDirectory { get; }
    public string PrependGMLDirectory { get; }
    public string AppendGMLDirectory { get; }
    public string AppendGMLCollisionDirectory { get; }
    public string NewObjectDirectory { get; }
    public string ExistingObjectDirectory { get; }
    public string RoomDirectory { get; }
    public int DefaultSpriteX { get; }
    public int DefaultSpriteY { get; }
    public uint DefaultSpriteSpeedType { get; }
    public float DefaultSpriteFrameSpeed { get; }
    public uint DefaultSpriteBoundingBoxType { get; }
    public int DefaultSpriteBoundingBoxLeft { get; }
    public int DefaultSpriteBoundingBoxRight { get; }
    public int DefaultSpriteBoundingBoxBottom { get; }
    public int DefaultSpriteBoundingBoxTop { get; }
    public uint DefaultSpriteSepMasksType { get; }
    public bool DefaultSpriteTransparent { get; }
    public bool DefaultSpriteSmooth { get; }
    public bool DefaultSpritePreload { get; }
    public uint DefaultSpriteSpecialVer { get; }
    public bool DefaultBGTransparent { get; }
    public bool DefaultBGSmooth { get; }
    public bool DefaultBGPreload { get; }
    public uint DefaultBGTileWidth { get; }
    public uint DefaultBGTileHeight { get; }
    public uint DefaultBGBorderX { get; }
    public uint DefaultBGBorderY { get; }
    public uint DefaultBGTileColumn { get; }
    public uint DefaultBGItemOrFramePerTile { get; }
    public uint DefaultBGTileCount { get; }
    public int DefaultBGFrameTime { get; }
    public string DefaultAudioType { get; }
    public bool DefaultAudioEmbedded { get; }
    public bool DefaultAudioCompressed { get; }
    public uint DefaultAudioEffects { get; }
    public float DefaultAudioVolume { get; }
    public float DefaultAudioPitch { get; }
    public int DefaultAudioGroupIndex { get; }
    public int DefaultAudioFileID { get; }
    public bool DefaultAudioPreload { get; }
}

[YamlObject]
public partial class CodeData
{
    [YamlMember("type")]
    public string? yml_type { get; set; }
    [YamlMember("find")]
    public string? yml_find { get; set; }
    [YamlMember("code")]
    public string? yml_code { get; set; }
    [YamlMember("case_sensitive")]
    public bool? yml_casesensitive { get; set; }
}

[YamlObject]
public partial class SpriteData
{
    [YamlMember("frames")] public int? yml_frame { get; set; }
    [YamlMember("x")] public int? yml_x { get; set; }
    [YamlMember("y")] public int? yml_y { get; set; }
    [YamlMember("transparent")] public bool? yml_transparent { get; set; }
    [YamlMember("smooth")] public bool? yml_smooth { get; set; }
    [YamlMember("preload")] public bool? yml_preload { get; set; }
    [YamlMember("speed_type")] public uint? yml_speedtype { get; set; }
    [YamlMember("frame_speed")] public float? yml_framespeed { get; set; }
    [YamlMember("bounding_box_type")] public uint? yml_boundingboxtype { get; set; }
    [YamlMember("bbox_left")] public int? yml_bboxleft { get; set; }
    [YamlMember("bbox_right")] public int? yml_bboxright { get; set; }
    [YamlMember("bbox_bottom")] public int? yml_bboxbottom { get; set; }
    [YamlMember("bbox_top")] public int? yml_bboxtop { get; set; }
    [YamlMember("sepmasks")] public uint? yml_sepmask { get; set; }
}

[YamlObject]
public partial class BackgroundData
{
    [YamlMember("tile_count")] public uint? yml_tile_count { get; set; }
    [YamlMember("tile_width")] public uint? yml_tile_width { get; set; }
    [YamlMember("tile_height")] public uint? yml_tile_height { get; set; }
    [YamlMember("border_x")] public uint? yml_border_x { get; set; }
    [YamlMember("border_y")] public uint? yml_border_y { get; set; }
    [YamlMember("tile_column")] public uint? yml_tile_column { get; set; }
    [YamlMember("item_per_tile")] public uint? yml_item_per_tile { get; set; }
    [YamlMember("transparent")] public bool? yml_transparent { get; set; }
    [YamlMember("smooth")] public bool? yml_smooth { get; set; }
    [YamlMember("preload")] public bool? yml_preload { get; set; }
    [YamlMember("frametime")] public long? yml_frametime { get; set; }
}

[YamlObject]
public partial class AudioData
{
    [YamlMember("type")] public string? yml_type { get; set; }
    [YamlMember("embedded")] public bool? yml_embedded { get; set; }
    [YamlMember("compressed")] public bool? yml_compressed { get; set; }
    [YamlMember("effects")] public uint? yml_effects { get; set; }
    [YamlMember("volume")] public float? yml_volume { get; set; }
    [YamlMember("pitch")] public float? yml_pitch { get; set; }
    [YamlMember("audiogroup_index")] public int? yml_audiogroup_index { get; set; }
    [YamlMember("audiofile_id")] public int? yml_audiofile_id { get; set; }
    [YamlMember("preload")] public bool? yml_preload { get; set; }
}

public class GMLoaderProgram
{
    #region Properties
    public static UndertaleData Data { get; set; } = null!;
    private static ScriptOptions CliScriptOptions { get; set; } = null!;
    public static string gameDataPath { get; set; } = string.Empty;
    public static string modsPath { get; set; } = string.Empty;
    public static string textureExclusion { get; set; } = string.Empty;
    public static List<string> textureExclusionList { get; set; } = new List<string>();
    public static string importPreCSXPath { get; set; } = string.Empty;
    public static string importBuiltInCSXPath { get; set; } = string.Empty;
    public static string importPostCSXPath { get; set; } = string.Empty;
    public static string importAfterCSXPath { get; set; } = string.Empty;
    public static List<string> invalidCodeNames { get; set; } = new List<string>();
    public static int invalidCode { get; set; }
    public static List<string> invalidSpriteNames { get; set; } = new List<string>();
    public static int invalidSprite { get; set; }
    public static List<string> invalidSpriteSizeNames { get; set; } = new List<string>();
    public static int invalidSpriteSize { get; set; }
    public static string texturesPath { get; set; } = string.Empty;
    public static string backgroundsTexturePath { get; set; } = string.Empty;
    public static string texturesConfigPath { get; set; } = string.Empty;
    public static string noStripTexturesPath { get; set; } = string.Empty;
    public static string backgroundsConfigPath { get; set; } = string.Empty;
    public static string audioPath { get; set; } = string.Empty;
    public static string audioConfigPath { get; set; } = string.Empty;
    public static string shaderPath { get; set; } = string.Empty;
    public static string configPath { get; set; } = string.Empty;
    public static DecompileSettings defaultDecompSettings { get; set; } = null!;
    public static string gmlCodePath { get; set; } = string.Empty;
    public static string gmlCodePatchPath { get; set; } = string.Empty;
    public static string collisionPath { get; set; } = string.Empty;
    public static string asmPath { get; set; } = string.Empty;
    public static string prependGMLPath { get; set; } = string.Empty;
    public static string appendGMLPath { get; set; } = string.Empty;
    public static string appendGMLCollisionPath { get; set; } = string.Empty;
    public static string newObjectPath { get; set; } = string.Empty;
    public static string existingObjectPath { get; set; } = string.Empty;
    public static string roomPath { get; set; } = string.Empty;
    public static int defaultSpriteX { get; set; }
    public static int defaultSpriteY { get; set; }
    public static float defaultSpriteFrameSpeed { get; set; }
    public static int defaultSpriteBoundingBoxLeft { get; set; }
    public static int defaultSpriteBoundingBoxRight { get; set; }
    public static int defaultSpriteBoundingBoxBottom { get; set; }
    public static int defaultSpriteBoundingBoxTop { get; set; }
    public static bool defaultSpriteTransparent { get; set; }
    public static bool defaultSpriteSmooth { get; set; }
    public static bool defaultSpritePreload { get; set; }
    public static uint defaultSpriteSpecialVer { get; set; }
    public static uint defaultSpriteSpeedType { get; set; }
    public static uint defaultSpriteBoundingBoxType { get; set; }
    public static uint defaultSpriteSepMasksType { get; set; }
    public static bool defaultBGTransparent { get; set; }
    public static bool defaultBGSmooth { get; set; }
    public static bool defaultBGPreload { get; set; }
    public static uint defaultBGTileWidth { get; set; }
    public static uint defaultBGTileHeight { get; set; }
    public static uint defaultBGBorderX { get; set; }
    public static uint defaultBGBorderY { get; set; }
    public static uint defaultBGTileColumn { get; set; }
    public static uint defaultBGItemOrFramePerTile { get; set; }
    public static uint defaultBGTileCount { get; set; }
    public static int defaultBGFrameTime { get; set; }
    public static string defaultAudioType { get; set; } = string.Empty;
    public static bool defaultAudioEmbedded { get; set; }
    public static bool defaultAudioCompressed { get; set; }
    public static uint defaultAudioEffects { get; set; }
    public static float defaultAudioVolume { get; set; }
    public static float defaultAudioPitch { get; set; }
    public static int defaultAudioGroupIndex { get; set; }
    public static int defaultAudioFileID { get; set; }
    public static bool defaultAudioPreload { get; set; }

    public static Dictionary<string, int> moddedTextureCounts = new();
    public static List<string> vanillaSpriteList = new();
    public static List<string> spriteList = new();
    public static List<string> backgroundList = new();
    public static Dictionary<string, SpriteData> spriteDictionary = new();
    public static Dictionary<string, BackgroundData> backgroundDictionary = new();
    public static string[] spritesToImport = Array.Empty<string>();
    public static string[] noStripStyleSpritesToImport = Array.Empty<string>();
    public static string[] backgroundsToImport = Array.Empty<string>();
    public static List<string> audioList = new();
    public static string[] audioToImport = Array.Empty<string>();
    public static Dictionary<string, AudioData> audioDictionary = new();
    #endregion

    /// <summary>
    /// Initialize logging with a callback. Call this before Run().
    /// </summary>
    /// <param name="logCallback">Your callback to receive log messages</param>
    public static void Initialize(GMLoaderLogHandler? logCallback = null)
    {
        var config = new LoggerConfiguration().MinimumLevel.Debug();

        if (logCallback != null)
        {
            config.WriteTo.Sink(new CallbackSink(logCallback));
        }

        Log.Logger = config.CreateLogger();
    }

    /// <summary>
    /// Run GMLoader with the specified config file
    /// </summary>
    /// <param name="configFilePath">Path to GMLoader.ini</param>
    /// <returns>Result indicating success or failure with error details</returns>
    public static GMLoaderResult Run(string configFilePath)
    {
        try
        {
            if (!File.Exists(configFilePath))
            {
                return GMLoaderResult.Fail($"Config file not found: {configFilePath}");
            }

            IConfig config = new ConfigurationBuilder<IConfig>()
               .UseIniFile(configFilePath)
               .Build();

            return RunWithConfig(config);
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred: {e.Message}");
            return GMLoaderResult.Fail(e.Message, e);
        }
    }

    /// <summary>
    /// Run GMLoader with an already-loaded config
    /// </summary>
    public static GMLoaderResult RunWithConfig(IConfig config)
    {
        try
        {
            #region Config
            textureExclusion = config.TextureExclusion;
            importPreCSXPath = config.ImportPreCSX;
            importBuiltInCSXPath = config.ImportBuiltinCSX;
            importPostCSXPath = config.ImportPostCSX;
            importAfterCSXPath = config.ImportAfterCSX;
            gameDataPath = config.GameData;
            modsPath = config.ModsDirectory;
            texturesPath = config.TexturesDirectory;
            backgroundsTexturePath = config.BackgroundTextureDirectory;
            noStripTexturesPath = config.NoStripTexturesDirectory;
            texturesConfigPath = config.TexturesConfigDirectory;
            backgroundsConfigPath = config.BackgroundsConfigDirectory;
            audioPath = config.AudioDirectory;
            audioConfigPath = config.AudioConfigDirectory;
            shaderPath = config.ShaderDirectory;
            configPath = config.ConfigDirectory;
            gmlCodePath = config.GMLCodeDirectory;
            gmlCodePatchPath = config.GMLCodePatchDirectory;
            collisionPath = config.CollisionDirectory;
            asmPath = config.ASMDirectory;
            prependGMLPath = config.PrependGMLDirectory;
            appendGMLPath = config.AppendGMLDirectory;
            appendGMLCollisionPath = config.AppendGMLCollisionDirectory;
            newObjectPath = config.NewObjectDirectory;
            existingObjectPath = config.ExistingObjectDirectory;
            roomPath = config.RoomDirectory;
            defaultSpriteX = config.DefaultSpriteX;
            defaultSpriteY = config.DefaultSpriteY;
            defaultSpriteSpeedType = config.DefaultSpriteSpeedType;
            defaultSpriteFrameSpeed = config.DefaultSpriteFrameSpeed;
            defaultSpriteBoundingBoxType = config.DefaultSpriteBoundingBoxType;
            defaultSpriteBoundingBoxLeft = config.DefaultSpriteBoundingBoxLeft;
            defaultSpriteBoundingBoxRight = config.DefaultSpriteBoundingBoxRight;
            defaultSpriteBoundingBoxBottom = config.DefaultSpriteBoundingBoxBottom;
            defaultSpriteBoundingBoxTop = config.DefaultSpriteBoundingBoxTop;
            defaultSpriteSepMasksType = config.DefaultSpriteSepMasksType;
            defaultSpriteTransparent = config.DefaultSpriteTransparent;
            defaultSpriteSmooth = config.DefaultSpriteSmooth;
            defaultSpritePreload = config.DefaultSpritePreload;
            defaultSpriteSpecialVer = config.DefaultSpriteSpecialVer;
            defaultBGTransparent = config.DefaultBGTransparent;
            defaultBGSmooth = config.DefaultBGSmooth;
            defaultBGPreload = config.DefaultBGPreload;
            defaultBGTileWidth = config.DefaultBGTileWidth;
            defaultBGTileHeight = config.DefaultBGTileHeight;
            defaultBGBorderX = config.DefaultBGBorderX;
            defaultBGBorderY = config.DefaultBGBorderY;
            defaultBGTileColumn = config.DefaultBGTileColumn;
            defaultBGItemOrFramePerTile = config.DefaultBGItemOrFramePerTile;
            defaultBGTileCount = config.DefaultBGTileCount;
            defaultBGFrameTime = config.DefaultBGFrameTime;
            defaultAudioType = config.DefaultAudioType;
            defaultAudioEmbedded = config.DefaultAudioEmbedded;
            defaultAudioCompressed = config.DefaultAudioCompressed;
            defaultAudioEffects = config.DefaultAudioEffects;
            defaultAudioVolume = config.DefaultAudioVolume;
            defaultAudioPitch = config.DefaultAudioPitch;
            defaultAudioGroupIndex = config.DefaultAudioGroupIndex;
            defaultAudioFileID = config.DefaultAudioFileID;
            defaultAudioPreload = config.DefaultAudioPreload;
            #endregion

            textureExclusionList = textureExclusion.Split(',').ToList();

            mkDir(modsPath);
            mkDir(texturesPath);
            mkDir(texturesConfigPath);
            mkDir(audioPath);
            mkDir(audioConfigPath);
            mkDir(gmlCodePath);
            mkDir(gmlCodePatchPath);
            mkDir(noStripTexturesPath);
            mkDir(backgroundsConfigPath);
            mkDir(roomPath);
            mkDir(importPreCSXPath);
            mkDir(importBuiltInCSXPath);
            mkDir(importPostCSXPath);
            mkDir(importAfterCSXPath);

            string modsPathAbsoluteDir = Path.GetFullPath(modsPath);
            if (Directory.Exists(modsPath))
            {
                Log.Debug($"Scanning the filetree of {modsPathAbsoluteDir}");
                Log.Debug($"{Path.GetFileName(modsPathAbsoluteDir)}");
                PrintFileTree(modsPath, "", true);
            }

            string[] dirPreCSXFiles = Directory.GetFiles(importPreCSXPath, "*.csx")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] dirBuiltInCSXFiles = Directory.GetFiles(importBuiltInCSXPath, "*.csx")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] dirPostCSXFiles = Directory.GetFiles(importPostCSXPath, "*.csx")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] dirAfterCSXFiles = Directory.GetFiles(importAfterCSXPath, "*.csx")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

            if (!File.Exists(gameDataPath))
            {
                return GMLoaderResult.Fail($"Game data file not found: {gameDataPath}");
            }

            Data = new UndertaleData();
            using (var stream = new FileStream(gameDataPath, FileMode.Open, FileAccess.ReadWrite))
            {
                Data = UndertaleIO.Read(stream);
            }

            defaultDecompSettings = new DecompileSettings()
            {
                RemoveSingleLineBlockBraces = true,
                EmptyLineAroundBranchStatements = true,
                EmptyLineBeforeSwitchCases = true,
            };

            ScriptOptionsInitialize();
            processCSXScripts(dirPreCSXFiles, dirBuiltInCSXFiles, dirPostCSXFiles, dirAfterCSXFiles);

            Log.Information("Recompiling data.win...");
            using (var stream = new FileStream(gameDataPath, FileMode.Create, FileAccess.ReadWrite))
                UndertaleIO.Write(stream, Data);

            if (dirAfterCSXFiles.Length != 0)
            {
                Log.Information("Running CSX Scripts after recompilation...");
                foreach (string file in dirAfterCSXFiles)
                {
                    RunCSharpFile(file).GetAwaiter().GetResult();
                }
            }

            Log.Information("Successfully recompiled data.win");
            return GMLoaderResult.Ok();
        }
        catch (Exception e)
        {
            Log.Error($"An error occurred: {e.Message}");
            return GMLoaderResult.Fail(e.Message, e);
        }
    }

    public static void importConfigDefinedCode(CodeImportGroup importGroup)
    {
        string[] configFiles = Directory.GetFiles(gmlCodePatchPath, "*.yaml*", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

        if (configFiles.Length == 0) return;

        Log.Information("Executing built-in ImportCodePatch...");
        var modificationHistory = new Dictionary<string, List<string>>();

        foreach (string file in configFiles)
        {
            try
            {
                byte[] yamlBytes = File.ReadAllBytes(file);
                string fileName = Path.GetFileName(file);
                Log.Information($"Deserializing {fileName}");

                var yamlContent = YamlSerializer.Deserialize<Dictionary<string, List<CodeData>>>(yamlBytes);

                foreach (var scriptEntry in yamlContent)
                {
                    string scriptName = scriptEntry.Key;
                    List<CodeData> patches = scriptEntry.Value;

                    if (!modificationHistory.ContainsKey(scriptName))
                        modificationHistory[scriptName] = new List<string>();
                    modificationHistory[scriptName].Add(fileName);

                    foreach (CodeData patch in patches)
                        ProcessCodePatch(importGroup, fileName, scriptName, patch, modificationHistory);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to process {Path.GetFileName(file)}: {e}");
            }
        }
    }

    public static void ProcessCodePatch(CodeImportGroup importGroup, string fileName, string scriptName, CodeData patch, Dictionary<string, List<string>> modificationHistory)
    {
        string type = patch.yml_type ?? "";
        string? find = patch.yml_find;
        string code = patch.yml_code ?? "";
        bool caseSensitive = patch.yml_casesensitive ?? true;

        switch (type.ToLowerInvariant())
        {
            case "findreplace":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueFindReplace(scriptName, find, code, caseSensitive);
                break;
            case "findreplacetrim":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueTrimmedLinesFindReplace(scriptName, find, code, caseSensitive);
                break;
            case "append":
                importGroup.QueueAppend(scriptName, code);
                break;
            case "prepend":
                importGroup.QueuePrepend(scriptName, code);
                break;
            case "findappend":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueFindReplace(scriptName, find, find + Environment.NewLine + code, caseSensitive);
                break;
            case "findprepend":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueFindReplace(scriptName, find, code + Environment.NewLine + find, caseSensitive);
                break;
            case "findappendtrim":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueTrimmedLinesFindReplace(scriptName, find, find + Environment.NewLine + code, caseSensitive);
                break;
            case "findprependtrim":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Find pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueTrimmedLinesFindReplace(scriptName, find, code + Environment.NewLine + find, caseSensitive);
                break;
            case "findreplaceregex":
                if (string.IsNullOrEmpty(find)) { Log.Error($"Regex pattern is empty for {scriptName}, skipping"); return; }
                importGroup.QueueRegexFindReplace(scriptName, find, code, caseSensitive);
                break;
            default:
                Log.Error($"Unknown patch type '{type}' for {scriptName}, skipping");
                break;
        }

        try { importGroup.Import(); }
        catch (Exception e)
        {
            string history = modificationHistory.ContainsKey(scriptName)
                ? string.Join(", ", modificationHistory[scriptName]) : "no modifications recorded";
            Log.Warning($"Error on {fileName} processing {scriptName}\n'{scriptName}' modified by: {history}\nFind: {find}\nCode: {code}\nException: {e}\n");
        }
    }

    private static async Task importGraphic()
    {
        spriteDictionary.Clear();
        spriteList.Clear();
        backgroundDictionary.Clear();
        backgroundList.Clear();
        string pngExt = ".png";

        var spriteConfigFilesTask = Task.Run(() => Directory.GetFiles(texturesConfigPath, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());
        var spriteStripStyleConfigFilesTask = Task.Run(() => Directory.GetFiles(noStripTexturesPath, "*.yaml", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());
        var backgroundConfigFilesTask = Task.Run(() => Directory.GetFiles(backgroundsConfigPath, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());

        await Task.WhenAll(spriteConfigFilesTask, spriteStripStyleConfigFilesTask, backgroundConfigFilesTask);

        string[] spriteConfigFiles = await spriteConfigFilesTask;
        string[] spriteStripStyleConfigFiles = await spriteStripStyleConfigFilesTask;
        string[] backgroundConfigFiles = await backgroundConfigFilesTask;

        if (spriteConfigFiles.Length == 0 && backgroundConfigFiles.Length == 0 && spriteStripStyleConfigFiles.Length == 0)
        {
            Log.Debug($"No sprite/background config files found at {texturesConfigPath}, skipping texture import");
            return;
        }

        Log.Information("Executing built-in ImportGraphic");
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        if (spriteConfigFiles.Length != 0)
        {
            var spriteFilenames = new ConcurrentBag<string>();
            var localSpriteList = new ConcurrentBag<string>();
            var localModdedTextureCounts = new ConcurrentDictionary<string, int>();
            var localSpriteDictionary = new ConcurrentDictionary<string, SpriteData>();

            Log.Information("Deserializing sprite configuration files...");

            await Parallel.ForEachAsync(spriteConfigFiles, parallelOptions, async (file, ct) =>
            {
                byte[] yamlBytes = await File.ReadAllBytesAsync(file, ct);
                Log.Information($"Deserializing {Path.GetFileName(file)}");
                var deserialized = YamlSerializer.Deserialize<Dictionary<string, SpriteData>>(yamlBytes);

                foreach (var (spritename, configs) in deserialized)
                {
                    spriteFilenames.Add(spritename + pngExt);
                    localSpriteList.Add(spritename);
                    localModdedTextureCounts[spritename] = configs.yml_frame ?? 1;
                    localSpriteDictionary[spritename] = new SpriteData
                    {
                        yml_frame = configs.yml_frame ?? 1,
                        yml_x = configs.yml_x ?? defaultSpriteX,
                        yml_y = configs.yml_y ?? defaultSpriteY,
                        yml_transparent = configs.yml_transparent ?? defaultSpriteTransparent,
                        yml_smooth = configs.yml_smooth ?? defaultSpriteSmooth,
                        yml_preload = configs.yml_preload ?? defaultSpritePreload,
                        yml_speedtype = configs.yml_speedtype ?? defaultSpriteSpeedType,
                        yml_framespeed = configs.yml_framespeed ?? defaultSpriteFrameSpeed,
                        yml_boundingboxtype = configs.yml_boundingboxtype ?? defaultSpriteBoundingBoxType,
                        yml_bboxleft = configs.yml_bboxleft ?? defaultSpriteBoundingBoxLeft,
                        yml_bboxright = configs.yml_bboxright ?? defaultSpriteBoundingBoxRight,
                        yml_bboxbottom = configs.yml_bboxbottom ?? defaultSpriteBoundingBoxBottom,
                        yml_bboxtop = configs.yml_bboxtop ?? defaultSpriteBoundingBoxTop,
                        yml_sepmask = configs.yml_sepmask ?? defaultSpriteSepMasksType
                    };
                }
            });

            spriteList.AddRange(localSpriteList);
            spritesToImport = spriteFilenames.ToArray();
            foreach (var kvp in localModdedTextureCounts) moddedTextureCounts[kvp.Key] = kvp.Value;
            foreach (var kvp in localSpriteDictionary) spriteDictionary[kvp.Key] = kvp.Value;
        }

        if (backgroundConfigFiles.Length != 0)
        {
            var backgroundFilenames = new ConcurrentBag<string>();
            var localBackgroundList = new ConcurrentBag<string>();
            var localBackgroundDictionary = new ConcurrentDictionary<string, BackgroundData>();

            Log.Information("Deserializing backgrounds configuration files...");

            await Parallel.ForEachAsync(backgroundConfigFiles, parallelOptions, async (file, ct) =>
            {
                byte[] yamlBytes = await File.ReadAllBytesAsync(file, ct);
                Log.Information($"Deserializing {Path.GetFileName(file)}");
                var deserialized = YamlSerializer.Deserialize<Dictionary<string, BackgroundData>>(yamlBytes);

                foreach (var (backgroundname, configs) in deserialized)
                {
                    backgroundFilenames.Add(backgroundname + pngExt);
                    localBackgroundList.Add(backgroundname);
                    localBackgroundDictionary[backgroundname] = new BackgroundData
                    {
                        yml_tile_count = configs.yml_tile_count ?? defaultBGTileCount,
                        yml_tile_width = configs.yml_tile_width ?? defaultBGTileWidth,
                        yml_tile_height = configs.yml_tile_height ?? defaultBGTileHeight,
                        yml_border_x = configs.yml_border_x ?? defaultBGBorderX,
                        yml_border_y = configs.yml_border_y ?? defaultBGBorderY,
                        yml_tile_column = configs.yml_tile_column ?? defaultBGTileColumn,
                        yml_item_per_tile = configs.yml_item_per_tile ?? defaultBGItemOrFramePerTile,
                        yml_transparent = configs.yml_transparent ?? defaultBGTransparent,
                        yml_smooth = configs.yml_smooth ?? defaultBGSmooth,
                        yml_preload = configs.yml_preload ?? defaultBGPreload,
                    };
                }
            });

            backgroundList.AddRange(localBackgroundList);
            backgroundsToImport = backgroundFilenames.ToArray();
            foreach (var kvp in localBackgroundDictionary) backgroundDictionary[kvp.Key] = kvp.Value;
        }

        if (spriteStripStyleConfigFiles.Length != 0)
        {
            var localSpriteList = new ConcurrentBag<string>();
            var localSpriteDictionary = new ConcurrentDictionary<string, SpriteData>();

            Log.Information("Deserializing nostrip style sprite configuration files...");

            await Parallel.ForEachAsync(spriteStripStyleConfigFiles, parallelOptions, async (file, ct) =>
            {
                byte[] yamlBytes = await File.ReadAllBytesAsync(file, ct);
                Log.Information($"Deserializing {Path.GetFileName(file)} ({Path.GetFileName(Path.GetDirectoryName(file))})");
                var deserialized = YamlSerializer.Deserialize<SpriteData>(yamlBytes);
                string? spriteName = Path.GetFileName(Path.GetDirectoryName(file));

                if (spriteName != null)
                {
                    localSpriteList.Add(spriteName);
                    localSpriteDictionary[spriteName] = new SpriteData
                    {
                        yml_x = deserialized.yml_x ?? defaultSpriteX,
                        yml_y = deserialized.yml_y ?? defaultSpriteY,
                        yml_transparent = deserialized.yml_transparent ?? defaultSpriteTransparent,
                        yml_smooth = deserialized.yml_smooth ?? defaultSpriteSmooth,
                        yml_preload = deserialized.yml_preload ?? defaultSpritePreload,
                        yml_speedtype = deserialized.yml_speedtype ?? defaultSpriteSpeedType,
                        yml_framespeed = deserialized.yml_framespeed ?? defaultSpriteFrameSpeed,
                        yml_boundingboxtype = deserialized.yml_boundingboxtype ?? defaultSpriteBoundingBoxType,
                        yml_bboxleft = deserialized.yml_bboxleft ?? defaultSpriteBoundingBoxLeft,
                        yml_bboxright = deserialized.yml_bboxright ?? defaultSpriteBoundingBoxRight,
                        yml_bboxbottom = deserialized.yml_bboxbottom ?? defaultSpriteBoundingBoxBottom,
                        yml_bboxtop = deserialized.yml_bboxtop ?? defaultSpriteBoundingBoxTop,
                        yml_sepmask = deserialized.yml_sepmask ?? defaultSpriteSepMasksType
                    };
                }
            });

            spriteList.AddRange(localSpriteList);
            foreach (var kvp in localSpriteDictionary) spriteDictionary[kvp.Key] = kvp.Value;
        }
    }

    private static async Task importAudio()
    {
        audioDictionary.Clear();
        audioList.Clear();

        string[] audioConfigFiles = await Task.Run(() => Directory.GetFiles(audioConfigPath, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());

        if (audioConfigFiles.Length == 0) return;

        Log.Information("Executing built-in ImportAudio");
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        var audioFilenames = new ConcurrentBag<string>();
        var localAudioList = new ConcurrentBag<string>();
        var localAudioDictionary = new ConcurrentDictionary<string, AudioData>();

        Log.Information("Deserializing audio configuration files...");

        await Parallel.ForEachAsync(audioConfigFiles, parallelOptions, async (file, ct) =>
        {
            byte[] yamlBytes = await File.ReadAllBytesAsync(file, ct);
            Log.Information($"Deserializing {Path.GetFileName(file)}");
            var deserialized = YamlSerializer.Deserialize<Dictionary<string, AudioData>>(yamlBytes);

            foreach (var (audioname, configs) in deserialized)
            {
                audioFilenames.Add(audioname);
                localAudioList.Add(audioname);
                localAudioDictionary[audioname] = new AudioData
                {
                    yml_type = configs.yml_type ?? defaultAudioType,
                    yml_embedded = configs.yml_embedded ?? defaultAudioEmbedded,
                    yml_compressed = configs.yml_compressed ?? defaultAudioCompressed,
                    yml_effects = configs.yml_effects ?? defaultAudioEffects,
                    yml_volume = configs.yml_volume ?? defaultAudioVolume,
                    yml_pitch = configs.yml_pitch ?? defaultAudioPitch,
                    yml_audiogroup_index = configs.yml_audiogroup_index ?? defaultAudioGroupIndex,
                    yml_audiofile_id = configs.yml_audiofile_id ?? defaultAudioFileID,
                    yml_preload = configs.yml_preload ?? defaultAudioPreload
                };
            }
        });

        audioList.AddRange(localAudioList);
        audioToImport = audioFilenames.ToArray();
        foreach (var kvp in localAudioDictionary) audioDictionary[kvp.Key] = kvp.Value;
    }

    #region Helper Methods

    public static void mkDir(string? path)
    {
        if (path != null && !Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static void PrintFileTree(string path, string indent, bool isLast)
    {
        string[] files = Directory.GetFiles(path).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] directories = Directory.GetDirectories(path)
            .Where(d => !Path.GetFileName(d).Equals("lib", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray();

        for (int i = 0; i < files.Length; i++)
        {
            bool lastFile = (i == files.Length - 1 && directories.Length == 0);
            Log.Debug($"{indent}{(lastFile ? "└── " : "├── ")}{Path.GetFileName(files[i])}");
        }

        for (int i = 0; i < directories.Length; i++)
        {
            bool lastDir = i == directories.Length - 1;
            Log.Debug($"{indent}{(lastDir ? "└── " : "├── ")}{Path.GetFileName(directories[i])}");
            PrintFileTree(directories[i], indent + (lastDir ? "    " : "|   "), lastDir);
        }
    }

    public static void processCSXScripts(string[] preCSXFiles, string[] builtInCSXFiles, string[] postCSXFiles, string[] afterCSXFiles)
    {
        if (preCSXFiles.Length != 0)
        {
            Log.Information("Running pre-CSX Scripts...");
            foreach (string file in preCSXFiles)
                RunCSharpFile(file).GetAwaiter().GetResult();
        }

        if (builtInCSXFiles.Length != 0)
        {
            Log.Information("Running builtin-CSX scripts...");
            importGraphic().GetAwaiter().GetResult();
            importAudio().GetAwaiter().GetResult();
            foreach (string file in builtInCSXFiles)
                RunCSharpFile(file).GetAwaiter().GetResult();
        }

        if (postCSXFiles.Length != 0)
        {
            Log.Information("Running post-CSX Scripts...");
            foreach (string file in postCSXFiles)
                RunCSharpFile(file).GetAwaiter().GetResult();
        }
    }
    #endregion

    #region Script Handling

    private static async Task RunCSharpFile(string path)
    {
        string lines = $"#line 1 \"{path}\"\n" + File.ReadAllText(path);
        await RunCSharpCode(lines, path);
    }

    public class Program
    {
        public string FilePath { get; set; } = string.Empty;
    }

    public static void ScriptMessage(string message) => Log.Information(message);
    public static void SetProgressBar(string message, string status, double currentValue, double maxValue) { }
    public static void IncrementProgressParallel() { }
    public static void SyncBinding(string resourceType, bool enable) { }
    public static void DisableAllSyncBindings() { }
    public static void EnsureDataLoaded() { }

    private static async Task RunCSharpCode(string code, string? scriptFile = null)
    {
        Log.Information($"Executing '{Path.GetFileName(scriptFile)}'");
        try
        {
            await CSharpScript.EvaluateAsync(code, 
                CliScriptOptions.WithFilePath(Path.GetFullPath(scriptFile ?? "")).WithFileEncoding(Encoding.UTF8), 
                new Program() { FilePath = gameDataPath }, typeof(Program));
        }
        catch (Exception exc)
        {
            Log.Error(exc.ToString());
            throw;
        }
        Log.Information($"Finished '{Path.GetFileName(scriptFile)}'");
    }

    private static void ScriptOptionsInitialize()
    {
        var references = new[]
        {
            typeof(UndertaleObject).GetTypeInfo().Assembly,
            typeof(GMLoaderProgram).GetTypeInfo().Assembly,
            typeof(ImageMagick.Drawing.DrawableAlpha).GetTypeInfo().Assembly,
            typeof(PixelFormat).GetTypeInfo().Assembly,
            typeof(YamlSerializer).GetTypeInfo().Assembly,
            typeof(Log).Assembly,
        };

        CliScriptOptions = ScriptOptions.Default
            .WithReferences(references)
            .AddImports(
                "UndertaleModLib", "UndertaleModLib.Models", "UndertaleModLib.Decompiler",
                "UndertaleModLib.Scripting", "UndertaleModLib.Compiler", "UndertaleModLib.Util",
                "GMLoader", "GMLoader.GMLoaderProgram", "ImageMagick", "Serilog",
                "System", "System.Text", "System.Linq", "System.IO", "System.Collections.Generic",
                "System.Drawing", "System.Drawing.Imaging", "System.Collections",
                "System.Text.RegularExpressions", "System.Diagnostics",
                "System.Threading", "System.Threading.Tasks", 
                "VYaml", "VYaml.Serialization", "VYaml.Annotations")
            .WithEmitDebugInformation(true);
    }
    #endregion
}