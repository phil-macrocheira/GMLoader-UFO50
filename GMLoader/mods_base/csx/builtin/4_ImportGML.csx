mkDir(gmlCodePath);
string[] dirFiles = Directory.GetFiles(gmlCodePath, "*.gml")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();
string[] codeConfigDirFiles = Directory.GetFiles(gmlCodePatchPath, "*.yaml")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

if (dirFiles.Length == 0 && codeConfigDirFiles.Length == 0)
    return;
else if (!dirFiles.Any(x => x.EndsWith(".gml", StringComparison.OrdinalIgnoreCase)) && !codeConfigDirFiles.Any(x => x.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
    return;

UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data, null, defaultDecompSettings)
{
    ThrowOnNoOpFindReplace = true
};

foreach (string file in dirFiles)
{
    Log.Information($"Importing {Path.GetFileName(file)}");
    string code = File.ReadAllText(file);
    string codeName = Path.GetFileNameWithoutExtension(file);
    importGroup.QueueReplace(codeName, code);
}
importGroup.Import(true);

bool success = importConfigDefinedCode(importGroup);
if (!success)
    throw new Exception();