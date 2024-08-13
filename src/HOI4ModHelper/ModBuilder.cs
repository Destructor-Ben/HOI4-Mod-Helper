using System.Runtime.InteropServices;

namespace HOI4ModHelper;

internal class ModBuilder(string modPath, string outputPath)
{
    public string ModPath { get; } = modPath;
    public string ModName => Path.GetFileName(ModPath.TrimEnd(Path.DirectorySeparatorChar).TrimEnd(Path.AltDirectorySeparatorChar));
    public string OutputPath => Path.Join(outputPath, ModName);

    public static string DocumentsFolder { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : "~/.local/share";
    public static string Hoi4ModFolder { get; } = Path.Join(DocumentsFolder, "Paradox Interactive/Hearts of Iron IV/mod");

    private readonly List<string> ignoredExtensions = [];
    private readonly List<string> ignoredFolders = [];
    private readonly List<string> ignoredFiles = [];

    public void Build()
    {
        // Print info
        Console.WriteLine("Mod Name: " + ModName);
        Console.WriteLine("Mod Path: " + ModPath);
        Console.WriteLine("Output Path: " + OutputPath);

        // Parse ignored_files.mod
        ParseIgnoredFilesInfo();

        // Delete old stuff
        // TODO: it might pay to verify that this isn't deleting everything on someones PC
        Console.WriteLine("Deleting old code...");
        if (Directory.Exists(OutputPath))
            Directory.Delete(OutputPath, true);

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

    private void ParseIgnoredFilesInfo()
    {
        string ignoredFilesInfoPath = Path.Join(ModPath, "ignored_files.mod");
        if (!File.Exists(ignoredFilesInfoPath))
            return;

        Console.WriteLine("Parsing ignored files...");

        // Loop over each line, ignore comments and empty lines
        foreach (string line in File.ReadLines(ignoredFilesInfoPath)
                                    .Where(l => !string.IsNullOrWhiteSpace(l))
                                    .Where(l => !l.StartsWith('#')))
        {
            // Either ignore a top-level dir, all files with a certain extension, or a single file
            // TODO: make this more like a .gitignore and use regex, also this is a terrible way of handling this
            if (line.StartsWith('*'))
            {
                // Extension
                ignoredExtensions.Add(Path.GetExtension(line));
            }
            else if (line.EndsWith(Path.DirectorySeparatorChar) || line.EndsWith(Path.AltDirectorySeparatorChar))
            {
                // Top level dir
                ignoredFolders.Add(line);
            }
            else
            {
                // Single file
                ignoredFiles.Add(line);
            }
        }
    }

    // TODO: do the flags and image stuff
    private void TransformFile(string file)
    {
        string relativeFileName = Path.GetRelativePath(ModPath, file) // (file.StartsWith(ModPath, StringComparison.Ordinal) ? file[ModPath.Length..] : file)
                                      .TrimStart(Path.DirectorySeparatorChar)
                                      .TrimStart(Path.AltDirectorySeparatorChar)
                                      .TrimEnd(Path.DirectorySeparatorChar)
                                      .TrimEnd(Path.AltDirectorySeparatorChar);

        if (ShouldIgnoreFile(relativeFileName))
            return;

        Console.WriteLine("Transforming file: " + file);

        string destinationFile = Path.Join(OutputPath, relativeFileName);
        CopyFile(file, destinationFile);

        Console.WriteLine("Transformed to: " + destinationFile);
    }

    private bool ShouldIgnoreFile(string fileName)
    {
        if (fileName == "ignored_files.mod")
            return true;

        // TODO: this is a mess
        if (ignoredFolders.Any(folder => fileName.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0].StartsWith(Path.TrimEndingDirectorySeparator(folder), StringComparison.Ordinal)))
            return true;

        if (ignoredExtensions.Any(extension => extension == Path.GetExtension(fileName)))
            return true;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (ignoredFiles.Any(file => file == fileName))
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
