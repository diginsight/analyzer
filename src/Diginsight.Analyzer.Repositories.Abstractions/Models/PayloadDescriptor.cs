using System.Text;

namespace Diginsight.Analyzer.Repositories.Models;

public sealed record PayloadDescriptor(string Label, bool IsOutput, Encoding Encoding, string Name, string ContentType);
