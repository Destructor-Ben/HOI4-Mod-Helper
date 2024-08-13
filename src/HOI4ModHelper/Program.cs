using System.CommandLine;

namespace HOI4ModHelper;

internal static class Program
{
    public static int Main(string[] args)
    {
        // Setting up CLI
        var rootCommand = new RootCommand("A tool for HOI4 mods that makes mod development easier.");

        var modPathOption = new Option<string>(["--mod-path", "-m"], Directory.GetCurrentDirectory, "The path to the folder containing the mods code. Defaults to the CWD.");
        rootCommand.AddOption(modPathOption);

        var outputPathOption = new Option<string>(["--output", "-o"], () => ModBuilder.Hoi4ModFolder, "The path to the folder where the mod should be outputted. Defaults to the HOI4 mods folder.");
        rootCommand.AddOption(outputPathOption);

        var shouldWatchOption = new Option<bool>(["--watch", "-w"], () => false, "Whether the mods code should be watched and rebuilt if it changes. Defaults to false.");
        rootCommand.AddOption(shouldWatchOption);

        rootCommand.SetHandler(Run, modPathOption, outputPathOption, shouldWatchOption);

        return rootCommand.Invoke(args);
    }

    private static void Run(string modPath, string outputPath, bool shouldWatch)
    {
        // Build the mod and set up a file watcher if needed
        var modBuilder = new ModBuilder(modPath, outputPath);
        modBuilder.Build();

        if (!shouldWatch)
            return;

        modBuilder.Watch();
    }
}
