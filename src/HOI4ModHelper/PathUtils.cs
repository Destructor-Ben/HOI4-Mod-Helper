namespace HOI4ModHelper;

public static class PathUtils
{
    /// <summary>
    /// Replace all backslashes in this path with forwards lashes.
    /// </summary>
    /// <param name="path">The path to clean.</param>
    /// <returns>The cleaned path.</returns>
    public static string Clean(this string path)
    {
        return path.Replace('\\', '/');
    }
}
