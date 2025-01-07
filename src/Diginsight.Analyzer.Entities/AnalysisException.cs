using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Entities;

public sealed class AnalysisException : ApplicationException
{
    public HttpStatusCode StatusCode { get; }

    public string Label { get; }

    public object?[] Parameters { get; }

    public AnalysisException(string message, HttpStatusCode statusCode, string label)
        : this(message, statusCode, label, (Exception?)null) { }

    public AnalysisException(string message, HttpStatusCode statusCode, string label, Exception? innerException)
        : this(message, innerException, statusCode, label, [ ]) { }

    public AnalysisException(
        ref InterpolatedStringHandler handler,
        HttpStatusCode statusCode,
        string label
    )
        : this(ref handler, statusCode, label, null) { }

    public AnalysisException(
        ref InterpolatedStringHandler handler,
        HttpStatusCode statusCode,
        string label,
        Exception? innerException
    )
        : this(handler.ToString(), innerException, statusCode, label, handler.Parameters.ToArray()) { }

    public AnalysisException(string messageFormat, HttpStatusCode statusCode, string label, object?[] parameters)
        : this(messageFormat, statusCode, label, null, parameters) { }

    public AnalysisException(string messageFormat, HttpStatusCode statusCode, string label, Exception? innerException, object?[] parameters)
        : this(string.Format(messageFormat, parameters), innerException, statusCode, label, parameters) { }

    [JsonConstructor]
    private AnalysisException(string message, Exception? innerException, HttpStatusCode statusCode, string label, object?[] parameters)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Label = label;
        Parameters = parameters;
    }

    [InterpolatedStringHandler]
    public readonly ref struct InterpolatedStringHandler
    {
        private readonly StringBuilder sb;
        private readonly ICollection<object?> parameters;

        public InterpolatedStringHandler(int literalLength, int formattedCount)
        {
            sb = new StringBuilder(literalLength);
            parameters = new List<object?>(formattedCount);
        }

        public IEnumerable<object?> Parameters => parameters;

        public void AppendLiteral(string str)
        {
            sb.Append(str);
        }

        public void AppendFormatted<T>(T obj)
        {
            sb.Append(obj);
            parameters.Add(obj);
        }

        public void AppendFormatted<T>(T obj, string? format)
            where T : IFormattable
        {
            sb.Append(obj.ToString(format, CultureInfo.InvariantCulture));
            parameters.Add(obj);
        }

        public override string ToString() => sb.ToString();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out string message, out Exception? innerException, out string label, out object?[] parameters)
    {
        message = Message;
        innerException = InnerException;
        label = Label;
        parameters = Parameters;
    }
}
