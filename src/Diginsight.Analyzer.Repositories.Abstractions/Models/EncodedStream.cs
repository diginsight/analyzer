using System.Text;

namespace Diginsight.Analyzer.Repositories.Models;

public record EncodedStream(Stream Stream, Encoding Encoding);
