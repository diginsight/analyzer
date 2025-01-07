using Microsoft.AspNetCore.Http.Extensions;

namespace Diginsight.Analyzer.API.Models;

internal sealed record PayloadLink(string Label, bool IsOutput, Uri Uri)
{
    public static PayloadLink From(Repositories.Models.PayloadDescriptor descriptor, HttpRequest request)
    {
        string label = descriptor.Label;
        return new PayloadLink(
            label, descriptor.IsOutput, new Uri(UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path + $"/{label}"))
        );
    }
}
