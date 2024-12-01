using System.Runtime.InteropServices;
using HOI4ModHelper.Dds;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace HOI4ModHelper;

internal class ModBuilder(string modPath, string outputPath, bool isDevBuild)
{
    public string ModPath { get; } = modPath.Clean();

    public string ModName
    {
        get
        {
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(ModPath));
            if (IsDevBuild)
                return name + "_Dev";

            return name;
        }
    }

    public string OutputPath => Path.Join(outputPath, ModName).Clean();
    public bool IsDevBuild { get; } = isDevBuild;

    public static string DocumentsFolder { get; } = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : "~/.local/share").Clean();
    public static string Hoi4ModFolder { get; } = Path.Join(DocumentsFolder, "Paradox Interactive/Hearts of Iron IV/mod").Clean();

    private readonly Ignore.Ignore ignoredFiles = new();

    // HOI4 needs specific file formats
    private static readonly TgaEncoder TgaEncoder = new() { BitsPerPixel = TgaBitsPerPixel.Pixel32, Compression = TgaCompression.None };

    private static readonly List<string> SupportedImageFormats =
    [
        ".gif",
        ".webp",
        ".pbm",
        ".jpeg",
        ".qoi",
        ".tga",
        ".tiff",
        ".bmp",
        ".png",
    ];

    public void Build()
    {
        // Print info
        Console.WriteLine("Mod Name: " + ModName);
        Console.WriteLine("Mod Path: " + ModPath);
        Console.WriteLine("Output Path: " + OutputPath);
        Console.WriteLine("Dev Build: " + IsDevBuild);
        Console.WriteLine();

        // Parse ignored_files.mod
        ParseIgnoredFilesInfo();

        // Delete old stuff
        // TODO: it might pay to verify that this isn't deleting everything on someones PC
        Console.WriteLine("Deleting old code...");
        if (Directory.Exists(OutputPath))
            Directory.Delete(OutputPath, true);

        Directory.CreateDirectory(OutputPath);

        // Create the mod descriptor in the mods folder
        Console.WriteLine("Creating descriptor...");
        string descriptorFilePath = Path.Join(ModPath, "descriptor.mod");

        string descriptorContents = "";
        if (File.Exists(descriptorFilePath))
            descriptorContents = File.ReadAllText(descriptorFilePath);

        descriptorContents += $"\npath=\"{OutputPath}\"";
        descriptorContents = TransformDescriptorContents(descriptorContents);

        // The descriptor goes in the mods folder (one dir up)
        string outputDescriptorFilePath = string.Join('/', OutputPath.Split('/')[..^1]);
        outputDescriptorFilePath = Path.Join(outputDescriptorFilePath, ModName + ".mod");

        PrintCopyMessage(descriptorFilePath, outputDescriptorFilePath);
        CreateFolderForFile(outputDescriptorFilePath);
        File.WriteAllText(outputDescriptorFilePath, descriptorContents);

        // Copy new stuff
        Console.WriteLine();
        Console.WriteLine("Transforming files...");
        foreach (string file in Directory.GetFiles(ModPath, "*", SearchOption.AllDirectories))
        {
            TransformFile(file);
        }

        Console.WriteLine();
        Console.WriteLine("Build completed successfully.");
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
        Console.WriteLine("File watcher running, press Enter to exit");
        fileWatcher.EnableRaisingEvents = true;
        Console.ReadKey();

        return;

        void UpdateFile(string file)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("File changed: " + file);
                TransformFile(file);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured: {e}");
            }
        }
    }

    private void ParseIgnoredFilesInfo()
    {
        // Check if ignored_files.mod exists
        string ignoredFilesInfoPath = Path.Join(ModPath, "ignored_files.mod");
        if (!File.Exists(ignoredFilesInfoPath))
            return;

        // Parse the file
        Console.WriteLine("Parsing ignored files...");

        // Loop over each line and add it to the ignored list
        foreach (string? line in File.ReadLines(ignoredFilesInfoPath))
        {
            ignoredFiles.Add(line);
        }
    }

    private void TransformFile(string file)
    {
        string relativeFileName = Path.GetRelativePath(ModPath, file)
                                      .Clean()
                                      .TrimStart('/')
                                      .TrimEnd('/');

        string destinationFile = Path.Join(OutputPath, relativeFileName);

        // Ignore the file if we need to
        if (relativeFileName == "ignored_files.mod" || ignoredFiles.IsIgnored(relativeFileName))
            return;

        // Images
        string fileExtension = Path.GetExtension(relativeFileName);
        if (SupportedImageFormats.Contains(fileExtension))
        {
            TransformImage(file, fileExtension, relativeFileName);
            return;
        }

        // Mod descriptor
        if (relativeFileName == "descriptor.mod")
        {
            string descriptorContents = File.ReadAllText(file);
            descriptorContents = TransformDescriptorContents(descriptorContents);

            PrintCopyMessage(file, destinationFile);
            CreateFolderForFile(destinationFile);
            File.WriteAllText(destinationFile, descriptorContents);
            return;
        }

        // Normal files
        PrintCopyMessage(file, destinationFile);
        CreateFolderForFile(destinationFile);
        File.Copy(file, destinationFile, true);
    }

    private void TransformImage(string file, string extension, string relativeFileName)
    {
        // Load image
        using var image = Image.Load<Rgba32>(file);
        string outputType = "dds";

        // Make thumbnail.png output type a PNG
        if (Path.ChangeExtension(relativeFileName, null) == "thumbnail")
            outputType = "png";

        // Make flags have an output of a TGA and copy over differently
        if (relativeFileName.StartsWith("gfx/flags", StringComparison.Ordinal))
        {
            outputType = "tga";
            CopyFlag(82, 52, "");
            CopyFlag(41, 26, "medium");
            CopyFlag(10, 7, "small");
            return;
        }

        // Copy the file over
        CopyImage(image, relativeFileName);

        return;

        void CopyImage(Image<Rgba32> imageToCopy, string outputFile, bool isTga = false)
        {
            string destinationFile = Path.Join(OutputPath, Path.ChangeExtension(outputFile, outputType));
            PrintCopyMessage(file, destinationFile);
            CreateFolderForFile(destinationFile);

            if (!isTga)
                imageToCopy.EncodeAsDds(destinationFile);
            else
                imageToCopy.Save(destinationFile, TgaEncoder);
        }

        void CopyFlag(int width, int height, string flagsFolder)
        {
            // Add the flags subdirectory if needed
            var path = relativeFileName.Split('/').ToList();
            if (!string.IsNullOrEmpty(flagsFolder))
                path.Insert(2, flagsFolder);

            // Copy
            using var newImage = image.Clone(i => i.Resize(width, height));
            CopyImage(newImage, string.Join('/', path), true);
        }
    }

    private string TransformDescriptorContents(string contents)
    {
        // Add " - Dev Version" onto the end of a dev builds name
        if (!IsDevBuild)
            return contents;

        var lines = contents.Split('\n').ToList();
        int nameLineIndex = lines.FindIndex(l => l.Trim().StartsWith("name", StringComparison.OrdinalIgnoreCase));
        if (nameLineIndex == -1)
            return contents;

        string nameLine = lines[nameLineIndex];
        int endQuoteIndex = nameLine.LastIndexOf('"');
        nameLine = nameLine.Insert(endQuoteIndex, " - Dev Version");
        lines[nameLineIndex] = nameLine;

        return string.Join('\n', lines);
    }

    // For some reason, the folder has to be created if it doesn't exist for stuff like copying
    private static void CreateFolderForFile(string filePath)
    {
        string destinationFolder = Path.GetDirectoryName(filePath) ?? throw new NullReferenceException("The given file isn't in a folder");
        if (!Directory.Exists(destinationFolder))
            Directory.CreateDirectory(destinationFolder);
    }

    private static void PrintCopyMessage(string sourcePath, string destinationPath)
    {
        Console.WriteLine($"Copying '{sourcePath.Clean()}'\n     to '{destinationPath.Clean()}'");
    }
}
