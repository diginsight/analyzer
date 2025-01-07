using System.Text;

namespace Diginsight.Analyzer.Repositories.Models;

public record NamedEncodedStream(Stream Stream, Encoding Encoding, string Name, string ContentType) : EncodedStream(Stream, Encoding);
