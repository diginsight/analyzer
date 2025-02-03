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

    public IReadOnlyList<object?> Parameters { get; }

    public AnalysisException(string message, HttpStatusCode statusCode, string label, Exception? innerException = null)
        : this(message, innerException, statusCode, label, [ ]) { }

    public AnalysisException(ref InterpolatedStringHandler handler, HttpStatusCode statusCode, string label, Exception? innerException = null)
        : this(handler.ToString(), innerException, statusCode, label, handler.Parameters) { }

    public AnalysisException(
        string messageFormat, IReadOnlyList<object?> parameters, HttpStatusCode statusCode, string label, Exception? innerException = null
    )
        : this(string.Format(messageFormat, parameters as object?[] ?? parameters.ToArray()), innerException, statusCode, label, parameters) { }

    [JsonConstructor]
    private AnalysisException(string message, Exception? innerException, HttpStatusCode statusCode, string label, IReadOnlyList<object?> parameters)
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
        private readonly IList<object?> parameters;

        public InterpolatedStringHandler(int literalLength, int formattedCount)
        {
            sb = new StringBuilder(literalLength);
            parameters = new List<object?>(formattedCount);
        }

        public IReadOnlyList<object?> Parameters => parameters.ToArray();

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
    public void Deconstruct(out string message, out Exception? innerException, out string label, out IReadOnlyList<object?> parameters)
    {
        message = Message;
        innerException = InnerException;
        label = Label;
        parameters = Parameters;
    }
}
