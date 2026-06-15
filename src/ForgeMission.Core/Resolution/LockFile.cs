using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Resolution;

public class LockFile
{
    public int Version { get; set; } = 1;
    public Dictionary<string, LockFileExpert> Experts { get; set; } = new(StringComparer.Ordinal);
}

public class LockFileExpert
{
    public string Source { get; set; } = "";
    public string Path   { get; set; } = "";
}

public static class LockFileIO
{
    // LockFile/LockFileExpert are public POCOs directly instantiated here, so the trimmer
    // preserves them. The IL3050 on DeserializerBuilder is conservative — reflection works
    // in AOT for preserved types.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFile))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFileExpert))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Types preserved via DynamicDependency")]
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFile))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LockFileExpert))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Types preserved via DynamicDependency")]
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static void Write(string path, LockFile lockFile)
        => File.WriteAllText(path, Serializer.Serialize(lockFile));

    public static LockFile Read(string path)
        => Deserializer.Deserialize<LockFile>(File.ReadAllText(path));

    public static LockFile Build(
        Dictionary<string, ResolvedExpert> catalog,
        string missionDirectory)
    {
        var lf = new LockFile();
        foreach (var (name, expert) in catalog.OrderBy(k => k.Key))
        {
            var relativePath = Path.GetRelativePath(missionDirectory, expert.ExpertMdPath);
            lf.Experts[name] = new LockFileExpert { Source = expert.Source, Path = relativePath };
        }
        return lf;
    }
}
