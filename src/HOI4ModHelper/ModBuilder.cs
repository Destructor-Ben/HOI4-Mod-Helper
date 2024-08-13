using System.Runtime.InteropServices;

namespace HOI4ModHelper;

internal class ModBuilder(string modPath, string outputPath)
{
    public string ModPath { get; } = modPath;
    public string OutputPath { get; } = outputPath;
    public string ModName => Path.GetFileName(ModPath.TrimEnd(Path.DirectorySeparatorChar).TrimEnd(Path.AltDirectorySeparatorChar));

    public static string DocumentsFolder { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : "~/.local/share";
    public static string Hoi4ModFolder { get; } = Path.Join(DocumentsFolder, "Paradox Interactive/Hearts of Iron IV/mod");

    public void Build()
    {
        // Print info
        Console.WriteLine("Mod Name: " + ModName);
        Console.WriteLine("Mod Path: " + ModPath);
        Console.WriteLine("Output Path: " + OutputPath);
        Console.WriteLine("Output Path (with Mod Name): " + Path.Join(OutputPath, ModName));

        // Delete old stuff
        // TODO: it might pay to verify that this isn't deleting everything on someones PC
        string oldOutputPath = Path.Join(OutputPath, ModName);
        Console.WriteLine("Deleting old code: " + oldOutputPath);

        if (Directory.Exists(oldOutputPath))
            Directory.Delete(oldOutputPath, true);

        // Copy new stuff
        foreach (string file in Directory.GetFiles(ModPath, "*", SearchOption.AllDirectories))
        {
            TransformFile(file);
        }
    }

    public void Watch()
    {
        Console.WriteLine();
        Console.WriteLine("Setting up file watcher...");

        var fileWatcher = new FileSystemWatcher();
        fileWatcher.Path = ModPath;
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
        fileWatcher.IncludeSubdirectories = true;

        fileWatcher.Changed += (_, e) => UpdateFile(e.FullPath);
        fileWatcher.Renamed += (_, e) => UpdateFile(e.FullPath);

        // Begin file watching
        Console.WriteLine("File watcher running");
        fileWatcher.EnableRaisingEvents = true;

        // Wait for the user to exit
        Console.WriteLine("Press any key to quit");
        Console.ReadKey();

        return;

        void UpdateFile(string file)
        {
            Console.WriteLine();
            Console.WriteLine("File Changed: " + file);
            TransformFile(file);
        }
    }

    // Replace the mod path with the output path
    private string ReplaceModPathWithOutputPath(string path)
    {
        return Path.Join(OutputPath, ModName, RemoveModPathFromFileName(path));
    }

    private string RemoveModPathFromFileName(string fileName)
    {
        return fileName.StartsWith(ModPath, StringComparison.Ordinal) ? fileName[ModPath.Length..] : fileName;
    }

    // TODO: make flags good, add file ignoring
    private void TransformFile(string file)
    {
        string niceFileName = RemoveModPathFromFileName(file)
                              .TrimStart(Path.DirectorySeparatorChar)
                              .TrimStart(Path.AltDirectorySeparatorChar)
                              .TrimEnd(Path.DirectorySeparatorChar)
                              .TrimEnd(Path.AltDirectorySeparatorChar);

        if (ShouldIgnoreFile(niceFileName))
            return;

        Console.WriteLine("Transforming file: " + file);

        string destinationFile = ReplaceModPathWithOutputPath(file);
        CopyFile(file, destinationFile);

        Console.WriteLine("Transformed to: " + destinationFile);
    }

    private bool ShouldIgnoreFile(string file)
    {
        if (file.StartsWith(".git", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static void CopyFile(string srcPath, string destPath)
    {
        // For some reason, the folder has to be created if it doesn't exist
        string destinationFolder = Path.GetDirectoryName(destPath) ?? throw new NullReferenceException("Copy destination path isn't in a folder!");
        if (!Directory.Exists(destinationFolder))
            Directory.CreateDirectory(destinationFolder);

        // Copy the file
        File.Copy(srcPath, destPath, true);
    }
}
