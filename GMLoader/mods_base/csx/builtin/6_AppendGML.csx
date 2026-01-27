mkDir(appendGMLPath);
string[] directories = Directory.GetDirectories(appendGMLPath)
                               .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                               .ToArray();

UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data, null, defaultDecompSettings);

if (directories.Length != 0)
{
    foreach (string directory in directories)
    {
        string[] dirFiles = Directory.GetFiles(directory, "*.gml")
                                   .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                   .ToArray();
        
        foreach (string file in dirFiles)
        {
            Log.Information($"Appending {Path.GetFileName(file)} from {Path.GetRelativePath(appendGMLPath, directory)} Folder");
            importGroup.QueueAppend(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        }
    }
    importGroup.Import(true);
}