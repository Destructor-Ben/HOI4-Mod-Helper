using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Svg;
using Image = SixLabors.ImageSharp.Image;

namespace HOI4ModHelper;

internal class ModBuilder(string modPath, string outputPath)
{
    public string ModPath { get; } = modPath.Replace('\\', '/');
    public string ModName => Path.GetFileName(Path.TrimEndingDirectorySeparator(ModPath));
    public string OutputPath => Path.Join(outputPath, ModName).Replace('\\', '/');

    public static string DocumentsFolder { get; } = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : "~/.local/share").Replace('\\', '/');
    public static string Hoi4ModFolder { get; } = Path.Join(DocumentsFolder, "Paradox Interactive/Hearts of Iron IV/mod").Replace('\\', '/');

    private readonly Ignore.Ignore ignoredFiles = new();

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
        // TODO: ".dds",
        ".svg",
    ];

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
        Console.WriteLine("File watcher running, press Enter to exit");
        fileWatcher.EnableRaisingEvents = true;
        Console.ReadKey();

        return;

        void UpdateFile(string file)
        {
            Console.WriteLine();
            Console.WriteLine("File changed: " + file);
            TransformFile(file);
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
                                      .Replace('\\', '/')
                                      .TrimStart('/')
                                      .TrimEnd('/');

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

        // Normal files
        string destinationFile = Path.Join(OutputPath, relativeFileName);
        PrintCopyMessage(file, destinationFile);
        CreateFolderForFile(destinationFile);
        File.Copy(file, destinationFile, true);
    }

    private void TransformImage(string file, string extension, string relativeFileName)
    {
        // TODO: dds saving and loading
        // Load image
        using var image = extension is ".svg" ? ConvertSvgToBitmap(file) : Image.Load(file);

        // Default output is a DDS
        string outputType = "png"; // TODO: "dds";

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

        void CopyImage(Image imageToCopy, string outputFile)
        {
            string destinationFile = Path.Join(OutputPath, Path.ChangeExtension(outputFile, outputType));
            PrintCopyMessage(file, destinationFile);
            CreateFolderForFile(destinationFile);
            imageToCopy.Save(destinationFile); // TODO: allow saving as DDS
        }

        void CopyFlag(int width, int height, string flagsFolder)
        {
            // Add the flags subdirectory if needed
            var path = relativeFileName.Split('/').ToList();
            if (!string.IsNullOrEmpty(flagsFolder))
                path.Insert(2, flagsFolder);

            // Copy
            using var newImage = image.Clone(i => i.Resize(width, height));
            CopyImage(newImage, string.Join('/', path));
        }
    }

    private static Image ConvertSvgToBitmap(string file)
    {
        var svg = SvgDocument.Open(file);

        // Not all svgs will have width and height attributes (automatically handled), sometimes a view box instead
        // TODO: handle view box attribute, also supply default width and height since not all SVGs will have a width and height - maybe we just don't care
        var bitmap = svg.Draw();
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Bmp);
        stream.Seek(0, SeekOrigin.Begin);
        return Image.Load(stream);
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
        Console.WriteLine($"Copying '{sourcePath.Replace('\\', '/')}'\n     to '{destinationPath.Replace('\\', '/')}'");
    }
}
