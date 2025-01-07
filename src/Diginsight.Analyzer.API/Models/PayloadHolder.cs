namespace Diginsight.Analyzer.API.Models;

internal sealed record PayloadHolder(string? RawContentType, string? FileName, Func<CancellationToken, ValueTask<Stream>> OpenStreamAsync);
