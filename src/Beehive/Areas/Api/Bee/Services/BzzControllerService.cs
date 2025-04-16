// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Areas.Api.Bee.DtoModels;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Manifest;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BzzControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IChunkService beeNetChunkService,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        IFeedService feedService)
        : IBzzControllerService
    {
        // Methods.
        public async Task<IActionResult> DownloadBzzAsync(
            string strAddress,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Normalize address and redirect to it.
            // - append '/' if rawAddress is only a hash, and final slash is missing
            var address = SwarmAddress.FromString(strAddress);
            if (address.ToString() != strAddress)
                return NewPermanentRedirectResult(address, httpContext.Request.QueryString);
            
            // Decode manifest.
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
            var manifest = new ReferencedMantarayManifest(chunkStore, address.Hash);
            
            // Try to dereference feed manifest first.
            var feedManifest = await feedService.TryDecodeFeedManifestAsync(manifest);
            if (feedManifest != null)
            {
                //dereference feed
                var feedChunk = await feedManifest.TryFindFeedChunkAtAsync(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    null,
                    chunkStore,
                    new Hasher());
                if (feedChunk == null)
                    throw new KeyNotFoundException("Can't find feed updates");

                var wrappedChunk = await feedChunk.UnwrapDataChunkAsync(false, new SwarmChunkBmt());
                address = new SwarmAddress(wrappedChunk.Hash, address.Path);
                manifest = new ReferencedMantarayManifest(
                    chunkStore,
                    wrappedChunk.Hash);
                
                //report feed index header
                var feedIndex = feedChunk.Index;
                var binaryFeedIndex = feedIndex.MarshalBinary();
                httpContext.Response.Headers[SwarmHttpConsts.SwarmFeedIndexHeader] = binaryFeedIndex.ToArray().ToHex();
                httpContext.Response.Headers.Append(
                    CorsConstants.AccessControlExposeHeaders,
                    SwarmHttpConsts.SwarmFeedIndexHeader);

                //report no cache headers
                httpContext.Response.Headers.SetNoCache();
            }
            
            // Serve content or redirect if required.
            try
            {
                // Get content chunk reference with metadata.
                var result = await manifest.GetResourceChunkReferenceWithMetadataAsync(
                    address.Path,
                    ManifestPathResolver.BrowserResolver);
                var (reference, metadata) = result.Result;
            
                // Read metadata.
                if (!metadata.TryGetValue(ManifestEntry.ContentTypeKey, out var mimeType))
                    mimeType = FileContentTypeProvider.DefaultContentType;
                if (!metadata.TryGetValue(ManifestEntry.FilenameKey, out var filename))
                    filename = reference.Hash.ToString();
            
                // Set custom headers.
                var contentDisposition = new ContentDisposition
                {
                    FileName = filename,
                    Inline = true
                };
                httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
                httpContext.Response.Headers.AccessControlExposeHeaders = HeaderNames.ContentDisposition;
            
                // Return content.
                var chunkJoiner = new ChunkJoiner(chunkStore);
                var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(
                    reference,
                    null,
                    CancellationToken.None).ConfigureAwait(false);

                httpContext.Response.StatusCode = result.IsFromErrorDoc ? 404 : 200;
                return new FileStreamResult(dataStream, mimeType)
                {
                    EnableRangeProcessing = true
                };
            }
            catch (ManifestExplicitRedirectException e)
            {
                // Permanent redirect.
                var redirectAddress = new SwarmAddress(address.Hash, e.RedirectToPath);
                return NewPermanentRedirectResult(redirectAddress, httpContext.Request.QueryString);
            }
        }

        public async Task<IActionResult> UploadBzzAsync(
            HttpRequest request,
            string? name,
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            string contentType,
            bool isDirectory,
            string? indexDocument,
            string? errorDocument)
        {
            var hashingResult = await dataService.UploadAsync(
                batchId,
                null,
                compactLevel > 0,
                pinContent,
                async (chunkStore, postageStamper) =>
                {
                    //upload directory
                    if (isDirectory || contentType == BeehiveHttpConsts.MultiPartFormDataContentType)
                    {
                        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                        try
                        {
                            // Extract files in temp directory.
                            Directory.CreateDirectory(tempDirectory);
                            switch (contentType)
                            {
                                case BeehiveHttpConsts.MultiPartFormDataContentType:
                                    foreach (var file in request.Form.Files)
                                    {
                                        // Combine tempDirectory with file path to respect directory structure.
                                        var filePath = Path.Combine(tempDirectory, file.FileName);

                                        // Ensure directory structure exists.
                                        var parentDirPath = Path.GetDirectoryName(filePath);
                                        if (!string.IsNullOrEmpty(parentDirPath))
                                            Directory.CreateDirectory(parentDirPath);

                                        // Save file content to the determined path.
                                        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                                        await file.CopyToAsync(fileStream);
                                    }
                                    break;
                            
                                case BeehiveHttpConsts.TarContentType:
                                    await using (var tarInput = new TarInputStream(request.Body, Encoding.UTF8))
                                    {
                                        while (await tarInput.GetNextEntryAsync(CancellationToken.None) is { } entry)
                                        {
                                            var outputPath = Path.Combine(tempDirectory, entry.Name);
                                            var parentDir = Path.GetDirectoryName(outputPath);
                                            if (parentDir != null && !Directory.Exists(parentDir))
                                                Directory.CreateDirectory(parentDir);

                                            if (!entry.IsDirectory)
                                            {
                                                await using var outputStream = File.Create(outputPath);
                                                await tarInput.CopyEntryContentsAsync(outputStream, CancellationToken.None);
                                            }
                                        }
                                    }
                                    break;

                                default:
                                    throw new ArgumentException(
                                        "Invalid content-type for directory upload",
                                        nameof(contentType));
                            }
                            
                            // Upload directory.
                            return (await beeNetChunkService.UploadDirectoryAsync(
                                tempDirectory,
                                new Hasher(),
                                indexDocument,
                                errorDocument,
                                compactLevel,
                                false,
                                RedundancyLevel.None,
                                postageStamper,
                                null,
                                chunkStore)).ChunkReference;
                        }
                        finally
                        {
                            Directory.Delete(tempDirectory, true);
                        }
                    }

                    //upload file
                    return (await beeNetChunkService.UploadSingleFileAsync(
                        request.Body,
                        contentType,
                        name,
                        new Hasher(),
                        compactLevel,
                        false,
                        RedundancyLevel.None,
                        postageStamper,
                        null,
                        chunkStore)).ChunkReference;
                });

            return new JsonResult(new SimpleChunkReferenceDto(hashingResult.Hash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }

        // Helpers.
        private static RedirectResult NewPermanentRedirectResult(SwarmAddress address, QueryString queryString) =>
            new("/bzz/" + address + queryString, true, true);
    }
}