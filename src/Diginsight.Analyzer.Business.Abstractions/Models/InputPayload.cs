using Diginsight.Analyzer.Repositories.Models;
using System.Text;

namespace Diginsight.Analyzer.Business.Models;

public sealed record InputPayload(string Label, Stream Stream, Encoding Encoding, string Name, string ContentType)
    : NamedEncodedStream(Stream, Encoding, Name, ContentType);
