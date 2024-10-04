using System.CommandLine;

namespace HOI4ModHelper;

internal static class Program
{
    public static int Main(string[] args)
    {
        // Setting up CLI
        var rootCommand = new RootCommand("A tool for HOI4 mods that makes mod development easier.");

        var modPathOption = new Option<string>(["--mod-path", "-m"], Directory.GetCurrentDirectory, "The path to the folder containing the mods code.");
        rootCommand.AddOption(modPathOption);

        var outputPathOption = new Option<string>(["--output", "-o"], () => ModBuilder.Hoi4ModFolder, "The path to the folder where the mod should be outputted.");
        rootCommand.AddOption(outputPathOption);

        var shouldWatchOption = new Option<bool>(["--watch", "-w"], () => false, "Whether the mods code should be watched and rebuilt if it changes.");
        rootCommand.AddOption(shouldWatchOption);

        var isDevBuildOption = new Option<bool>(["--dev", "-d"], () => true, "Whether the mod will be built in developer mode.");
        rootCommand.AddOption(isDevBuildOption);

        rootCommand.SetHandler(Run, modPathOption, outputPathOption, shouldWatchOption, isDevBuildOption);

        return rootCommand.Invoke(args);
    }

    private static void Run(string modPath, string outputPath, bool shouldWatch, bool isDevBuild)
    {
        // Build the mod and set up a file watcher if needed
        var modBuilder = new ModBuilder(modPath, outputPath, isDevBuild);
        modBuilder.Build();

        if (!shouldWatch)
            return;

        modBuilder.Watch();
    }
}
