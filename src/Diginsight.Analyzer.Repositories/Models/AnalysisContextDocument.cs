using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Repositories.Models;

internal sealed class AnalysisContextDocument
{
    [JsonProperty("id")]
    public string Id { get; }

    public ExecutionKind ExecutionKind { get; }

    public Guid AnalysisId { get; }

    public int Attempt { get; }

    [JsonExtensionData]
    public JObject ExtensionData { get; } = new ();

    [JsonConstructor]
    private AnalysisContextDocument(ExecutionKind executionKind, string id, Guid analysisId, int attempt)
    {
        ExecutionKind = executionKind;
        Id = id;
        AnalysisId = analysisId;
        Attempt = attempt;
    }

    public static AnalysisContextDocument Create(IAnalysisContext analysisContext)
    {
        (ExecutionKind executionKind, Guid executionId) = analysisContext.ExecutionCoord;
        (Guid analysisId, int attempt) = analysisContext.AnalysisCoord;

        AnalysisContextDocument document = new (executionKind, executionId.ToString("D"), analysisId, attempt);

        JsonSerializer serializer = JsonSerializer.CreateDefault();

        JObject rawSource = JObject.FromObject(analysisContext, serializer);
        rawSource.Property(nameof(IAnalysisContext.ExecutionCoord), StringComparison.OrdinalIgnoreCase)!.Remove();
        rawSource.Property(nameof(IAnalysisContext.AnalysisCoord), StringComparison.OrdinalIgnoreCase)!.Remove();

        using (JsonReader reader = rawSource.CreateReader())
        {
            serializer.Populate(reader, document);
        }

        return document;
    }
}
