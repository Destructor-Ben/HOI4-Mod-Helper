using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Svg;
using Image = SixLabors.ImageSharp.Image;

namespace HOI4ModHelper;

// TODO: make all backslashes forward slashes in paths
internal class ModBuilder(string modPath, string outputPath)
{
    public string ModPath { get; } = modPath.Replace('\\', '/');
    public string ModName => Path.GetFileName(Path.TrimEndingDirectorySeparator(ModPath));
    public string OutputPath => Path.Join(outputPath.Replace('\\', '/'), ModName);

    public static string DocumentsFolder { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : "~/.local/share";
    public static string Hoi4ModFolder { get; } = Path.Join(DocumentsFolder, "Paradox Interactive/Hearts of Iron IV/mod");

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
        ".dds",
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

        // Transform images
        // TODO: handle all ImageSharp supported file types, probably try and add SVG support too
        // TODO: make all images handled properly
        string fileExtension = Path.GetExtension(relativeFileName);
        if (SupportedImageFormats.Contains(fileExtension))
        {
            TransformImage(file, fileExtension, relativeFileName);
            return;
        }

        if (fileExtension is ".png" or ".svg" && relativeFileName != "thumbnail.png")
        {
            // Render SVGs
            if (fileExtension == ".svg")
            {
                var svg = SvgDocument.Open(file);

                // Not all svgs will have width and height attributes (automatically handled), sometimes a view box instead
                // TODO: handle viewbox attribute, also supply default width and height
                // TODO: not all svgs will include a width and height
                //svg.Width = 100;
                //svg.Height = 100;
                var bitmap = svg.Draw();
                var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Bmp);
                stream.Seek(0, SeekOrigin.Begin);
                var image = Image.Load<Rgba32>(stream);

                string dest = Path.Join(OutputPath, relativeFileName);
                PrintCopyMessage(file, dest);
                CreateFolderForFile(dest);
                image.SaveAsPng(dest);
                return;
            }

            // Pretty much only flags use TGA, the rest of the images use DDS
            if (relativeFileName.StartsWith("gfx/flags", StringComparison.Ordinal))
                TransformFlag(file, relativeFileName);
            else
                TransformImage(file,, extension relativeFileName);

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
        // Load image
        Image image = extension is ".svg" ? ConvertSvgToBitmap(file) : Image.Load(file);

        // Default output is a DDS
        string outputType = "dds";

        // Make thumbnail.png output type a PNG
        if (Path.ChangeExtension(file, null) == "thumbnail")
            outputType = "png";

        // Make flags have an output of a TGA

        // Turn png into DDS

        string destinationFile = Path.Join(OutputPath, Path.ChangeExtension(relativeFileName, "dds"));

        PrintCopyMessage(file, destinationFile);
        CreateFolderForFile(destinationFile);

        image.SaveAsTga(destinationFile); // TODO: save as DDS - probably make my own library for ImageSharp or improve ImageSharp.Textures
    }

    private static Image ConvertSvgToBitmap(string file)
    {
        var svg = SvgDocument.Open(file);

        // Not all svgs will have width and height attributes (automatically handled), sometimes a view box instead
        // TODO: handle view box attribute, also supply default width and height since not all SVGs will have a width and height - maybe we just don't care
        var bitmap = svg.Draw();
        var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Bmp);
        stream.Seek(0, SeekOrigin.Begin);
        return Image.Load(stream);
    }

    private void TransformFlag(string file, string relativeFileName)
    {
        // TODO: what if this overwrites other files?
        var image = Image.Load(file);

        // Large, medium, and small flags
        SaveFlag(82, 52, "");
        SaveFlag(41, 26, "medium");
        SaveFlag(10, 7, "small");

        return;

        void SaveFlag(int width, int height, string flagFolder)
        {
            // TODO: wtf is this wizardry
            string destinationFile = Path.Join(OutputPath, "gfx/flags", flagFolder, Path.ChangeExtension(string.Join('/', relativeFileName.Split('/')[2..]), "tga"));
            var newImage = image.Clone(i => i.Resize(width, height));

            PrintCopyMessage(file, destinationFile);
            CreateFolderForFile(destinationFile);
            newImage.SaveAsTga(destinationFile);
        }
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
        Console.WriteLine($"Copying '{sourcePath}'\n     to '{destinationPath}'");
    }
}
