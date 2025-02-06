using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Business.Models;

[JsonObject(MissingMemberHandling = MissingMemberHandling.Error, ItemNullValueHandling = NullValueHandling.Ignore)]
public sealed class ExceptionView
{
    [JsonProperty]
    // ReSharper disable once ReplaceWithFieldKeyword
    private IReadOnlyList<object?>? parameters;

    public string Message { get; }

    public ExceptionView? InnerException { get; }

    public string? Label { get; }

    [JsonIgnore]
    public IReadOnlyList<object?> Parameters => parameters ?? [ ];

    [JsonConstructor]
    private ExceptionView(string message, ExceptionView? innerException, string? label, IReadOnlyList<object?>? parameters)
    {
        Message = message;
        InnerException = innerException;
        Label = label;
        this.parameters = parameters is [ ] ? null : parameters;
    }

    [return: NotNullIfNotNull(nameof(exception))]
    public static ExceptionView? From(Exception? exception) => CoreFrom(exception is AggregateException ae ? ae.Flatten() : exception);

    [return: NotNullIfNotNull(nameof(exception))]
    private static ExceptionView? CoreFrom(Exception? exception) => exception switch
    {
        null => null,
        AnalysisException (var message, var innerException, var label, var parameters) =>
            new ExceptionView(message, From(innerException), label, parameters),
        _ => new ExceptionView(exception.Message, From(exception.InnerException), null, null),
    };
}
