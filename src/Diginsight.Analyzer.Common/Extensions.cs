using Newtonsoft.Json;
using System.ComponentModel;
using System.Text;

namespace Diginsight.Analyzer.Common;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class Extensions
{
    public static void Serialize(
        this JsonSerializer serializer, Stream stream, object? obj, Type? objectType = null, Encoding? encoding = null
    )
    {
        using TextWriter tw = new StreamWriter(stream, encoding ?? CommonUtils.DefaultEncoding, leaveOpen: true);
        using JsonWriter jw = new JsonTextWriter(tw);
        serializer.Serialize(jw, obj, objectType);
        tw.Flush();
    }

    public static async Task SerializeAsync(
        this JsonSerializer serializer, Stream stream, object? obj, Type? objectType = null, Encoding? encoding = null
    )
    {
        await using TextWriter tw = new StreamWriter(stream, encoding ?? CommonUtils.DefaultEncoding, leaveOpen: true);
        await using JsonWriter jw = new JsonTextWriter(tw);
        serializer.Serialize(jw, obj, objectType);
        await tw.FlushAsync();
    }

    public static T Deserialize<T>(
        this JsonSerializer serializer, Stream stream, Encoding? encoding = null
    )
    {
        using TextReader tr = new StreamReader(stream, encoding ?? CommonUtils.DefaultEncoding, leaveOpen: true);
        using JsonReader jr = new JsonTextReader(tr);
        return serializer.Deserialize<T>(jr)!;
    }

    public static async Task<T> DeserializeAsync<T>(
        this JsonSerializer serializer, Stream stream, Encoding? encoding = null
    )
    {
        using TextReader tr = new StreamReader(stream, encoding ?? CommonUtils.DefaultEncoding, leaveOpen: true);
        await using JsonReader jr = new JsonTextReader(tr);
        return serializer.Deserialize<T>(jr)!;
    }
}
