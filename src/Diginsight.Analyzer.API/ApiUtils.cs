using Diginsight.Analyzer.API.Models;
using Diginsight.Analyzer.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using ContentDispositionHeaderValue = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue;
using MediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;

namespace Diginsight.Analyzer.API;

internal static class ApiUtils
{
    public static async Task<PayloadHolder> FollowLocationAsync(
        HttpClient httpClient, HttpRequest request, IFormFile? ff, bool skipFileName, CancellationToken cancellationToken
    )
    {
        string? rawContentLocation;
        string? rawContentType;
        string? fileName;
        Func<CancellationToken, ValueTask<Stream>> readStreamAsync;
        if (ff is not null)
        {
            rawContentLocation = ff.Headers.ContentLocation.LastOrDefault();
            rawContentType = ff.ContentType.HardTrim();
            fileName = skipFileName ? null : ff.FileName.HardTrim();
            readStreamAsync = _ => ValueTask.FromResult(ff.OpenReadStream());
        }
        else
        {
            rawContentLocation = request.Headers.ContentLocation.LastOrDefault();
            rawContentType = request.ContentType;
            fileName = null;
            readStreamAsync = _ => ValueTask.FromResult(request.Body);
        }

        if (rawContentLocation is null)
        {
            return new PayloadHolder(rawContentType, fileName, readStreamAsync);
        }

        string? ffName = ff?.Name;
        if (!Uri.TryCreate(rawContentLocation, UriKind.Absolute, out Uri? contentLocation))
        {
            (string messageFormat, IReadOnlyList<object?> parameters) = ffName is null
                ? ("Malformed content location", Array.Empty<object?>())
                : ("Malformed content location for form file {0}", [ ffName ]);
            throw new AnalysisException(messageFormat, parameters, HttpStatusCode.BadRequest, "MalformedContentLocation");
        }

        return await FollowLocationAsync(
            httpClient,
            contentLocation,
            ffName,
            ff is null || skipFileName ? null : new StrongBox<string?>(fileName),
            cancellationToken
        );
    }

    private static async Task<PayloadHolder> FollowLocationAsync(
        HttpClient httpClient, [DisallowNull] Uri? contentLocation, string? ffName, StrongBox<string?>? fileNameBox, CancellationToken cancellationToken
    )
    {
        while (true)
        {
            string contentLocationLeaf = contentLocation.Segments[^1];
            HttpResponseMessage response = await httpClient.GetAsync(contentLocation, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();

                HttpStatusCode statusCode = response.StatusCode;
                (string messageFormat, IReadOnlyList<object?> parameters) = ffName is null
                    ? ("Cannot follow content location due to {0} status code", new object?[] { statusCode })
                    : ("Cannot follow content location for form file '{1}' due to {0} status code", [ statusCode, ffName ]);

                throw new AnalysisException(messageFormat, parameters, HttpStatusCode.BadGateway, "CannotFollowContentLocation");
            }

            HttpContentHeaders headers = response.Content.Headers;

            if (fileNameBox is { Value: null } &&
                headers.ContentDisposition is { } cdhv &&
                (cdhv.FileNameStar ?? cdhv.FileName) is { } tempFileName)
            {
                fileNameBox.Value = tempFileName;
            }

            contentLocation = headers.ContentLocation;
            if (contentLocation is not null)
            {
                response.Dispose();
                continue;
            }

            return new PayloadHolder(
                headers.ContentType?.ToString(),
                fileNameBox is null ? null : fileNameBox.Value ?? contentLocationLeaf,
                ct => new ValueTask<Stream>(response.Content.ReadAsStreamAsync(ct))
            );
        }
    }

    internal static async Task<IFormFileCollection> ReadFormFilesAsync(this HttpRequest request, CancellationToken cancellationToken)
    {
        MediaTypeHeaderValue mthv = MediaTypeHeaderValue.Parse(request.Headers.ContentType.Last()!);
        MultipartReader reader = new (mthv.Boundary.ToString(), request.Body);

        FormFileCollection collection = [ ];
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            Stream sectionBody = section.Body;
            Stream seekableSectionBody = new MemoryStream();
            await sectionBody.CopyToAsync(seekableSectionBody, cancellationToken);
            seekableSectionBody.Position = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static string? HardTrim(StringSegment str) => str.Value.HardTrim();

            ContentDispositionHeaderValue cdhv = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
            IFormFile formFile = new FormFile(
                seekableSectionBody, 0,
                seekableSectionBody.Length,
                HardTrim(cdhv.Name) ?? "",
                HardTrim(cdhv.FileNameStar) ?? HardTrim(cdhv.FileName) ?? ""
            )
            {
                Headers = new HeaderDictionary(section.Headers),
            };

            collection.Add(formFile);
        }

        return collection;
    }
}
