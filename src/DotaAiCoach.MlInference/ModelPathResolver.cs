namespace DotaAiCoach.MlInference;

/// <summary>
/// Resolves bundled model artifact paths relative to the running
/// assembly's base directory. Model files are shipped as
/// CopyToOutputDirectory content under Models/&lt;model-name&gt;/ (see
/// docs/adr/0023) — no network access is required at inference time.
/// </summary>
internal static class ModelPathResolver
{
    public static string ResolvePath(string modelFolderName, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Models", modelFolderName, fileName);
}
