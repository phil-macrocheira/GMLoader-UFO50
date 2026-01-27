mkDir(prependGMLPath);
string[] directories = Directory.GetDirectories(prependGMLPath);

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
            Log.Information($"Prepending {Path.GetFileName(file)} from {Path.GetRelativePath(prependGMLPath, directory)} Folder");
            importGroup.QueuePrepend(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        } 
    }
    importGroup.Import(true);
}