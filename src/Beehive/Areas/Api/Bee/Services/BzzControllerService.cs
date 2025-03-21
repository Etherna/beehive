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
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Hashing.Signer;
using Etherna.BeeNet.Manifest;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Etherna.BeeNet.Stores;
using Etherna.MongoDB.Driver.Linq;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BzzControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IChunkService beeNetChunkService,
        IBeehiveDbContext dbContext,
        IFeedService feedService,
        IPostageBatchService postageBatchService)
        : IBzzControllerService
    {
        // Methods.
        public async Task<IActionResult> DownloadBzzAsync(
            SwarmAddress address,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));

            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
            
            // Decode manifest.
            var manifest = new ReferencedMantarayManifest(chunkStore, address.Hash);
            
            // Try to dereference feed manifest first.
            var feedManifest = await feedService.TryDecodeFeedManifestAsync(manifest, new Hasher());
            if (feedManifest != null)
            {
                //dereference feed
                var feedChunk = await feedManifest.TryFindFeedAtAsync(chunkStore, DateTimeOffset.UtcNow, null);
                if (feedChunk == null)
                    throw new KeyNotFoundException("Can't find feed updates");

                var wrappedChunk = await feedService.UnwrapChunkAsync(feedChunk, chunkStore);
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
                httpContext.Response.Headers.CacheControl = new[]
                {
                    "no-store",
                    "no-cache",
                    "must-revalidate",
                    "proxy-revalidate"
                };
                httpContext.Response.Headers.Expires = "0";
            }
            
            // Resolve content address.
            try
            {
                return await ServeContentAsync(
                    manifest,
                    address.Path,
                    httpContext,
                    chunkStore).ConfigureAwait(false);
            }
            catch(KeyNotFoundException)
            {
                // Check for existing directory redirect. Example: /mydir?args -> /mydir/?args
                if (!address.Path.EndsWith(SwarmAddress.Separator) &&
                    await manifest.HasPathPrefixAsync(address.Path + SwarmAddress.Separator))
                    return new RedirectResult(
                        httpContext.Request.Path + SwarmAddress.Separator + httpContext.Request.QueryString,
                        true);
                
                // Check index suffix to path.
                var metadata = await manifest.GetResourceMetadataAsync(MantarayManifest.RootPath);
                if (metadata.TryGetValue(ManifestEntry.WebsiteIndexDocPathKey, out var indexDocument) &&
                    Path.GetFileName(address.Path) != indexDocument)
                    return await ServeContentAsync(
                        manifest,
                        Path.Combine(address.Path, indexDocument),
                        httpContext,
                        chunkStore).ConfigureAwait(false);
                
                // check if error document is to be shown
                if (metadata.TryGetValue(ManifestEntry.WebsiteErrorDocPathKey, out var errorDocument) &&
                    address.Path != SwarmAddress.Separator + errorDocument)
                    return await ServeContentAsync(
                        manifest,
                        errorDocument,
                        httpContext,
                        chunkStore).ConfigureAwait(false);
                
                throw;
            }
        }

        public async Task<IActionResult> UploadBzzAsync(
            string? name,
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            string contentType,
            bool isDirectory,
            string? indexDocument,
            string? errorDocument,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Acquire lock on postage batch.
            await using var batchLockHandler = await postageBatchService.AcquireLockAsync(batchId, compactLevel > 0);
            
            // Verify postage batch, load status and build postage stamper.
            var postageBatchCache = await postageBatchService.TryGetPostageBatchAsync(batchId);
            if (postageBatchCache is null)
                throw new KeyNotFoundException();
            var postageStampsCache = await dbContext.PostageStamps.QueryElementsAsync(
                elements => elements.Where(s => s.BatchId == batchId)
                    .ToListAsync());

            using var postageBuckets = new PostageBuckets(postageBatchCache.Buckets.ToArray());
            var stampStore = new MemoryStampStore(
                postageStampsCache.Select(stamp =>
                    new StampStoreItem(
                        batchId,
                        stamp.ChunkHash,
                        new StampBucketIndex(
                            stamp.BucketId,
                            stamp.BucketCounter))));
            var postageStamper = new PostageStamper(
                new FakeSigner(),
                new PostageStampIssuer(
                    new PostageBatch(
                        id: postageBatchCache.BatchId,
                        amount: 0,
                        blockNumber: 0,
                        depth: postageBatchCache.Depth,
                        exists: true,
                        isImmutable: postageBatchCache.IsImmutable,
                        isUsable: true,
                        label: null,
                        storageRadius: null,
                        ttl: TimeSpan.FromDays(3650),
                        utilization: 0),
                    postageBuckets),
                stampStore);

            // Create pin if required.
            ChunkPin? pin = null;
            if (pinContent)
            {
                pin = new ChunkPin(null); //set root hash later
                await dbContext.ChunkPins.CreateAsync(pin);
                pin = await dbContext.ChunkPins.FindOneAsync(pin.Id);
            }
            
            // Upload.
            List<UploadedChunkRef> chunkRefs = [];
            SwarmChunkReference hashingResult;
            await using (
                var dbChunkStore = new BeehiveChunkStore(
                    beeNodeLiveManager,
                    dbContext,
                    onSavingChunk: c =>
                    {
                        if (pin != null)
                            c.AddPin(pin);
                        chunkRefs.Add(new(c.Hash, batchId));
                    }))
            {
                //create and store chunks
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
                                foreach (var file in httpContext.Request.Form.Files)
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
                                await TarFile.ExtractToDirectoryAsync(httpContext.Request.Body, tempDirectory, true);
                                break;
                        
                            default:
                                throw new ArgumentException(
                                    "Invalid content-type for directory upload",
                                    nameof(contentType));
                        }
                        
                        // Upload directory.
                        var uploadResult = await beeNetChunkService.UploadDirectoryAsync(
                            tempDirectory,
                            indexDocument,
                            errorDocument,
                            compactLevel,
                            false,
                            RedundancyLevel.None,
                            postageStamper,
                            null,
                            dbChunkStore);
                        hashingResult = uploadResult.ChunkReference;
                    }
                    finally
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                }
                else
                {
                    var uploadResult = await beeNetChunkService.UploadSingleFileAsync(
                        httpContext.Request.Body,
                        contentType,
                        name,
                        compactLevel,
                        false,
                        RedundancyLevel.None,
                        postageStamper,
                        null,
                        dbChunkStore);
                    hashingResult = uploadResult.ChunkReference;
                }
            }
            
            // Add new stamped chunks to postage batch cache.
            await postageBatchService.StoreStampedChunksAsync(
                postageBatchCache,
                postageStampsCache.Select(s => s.ChunkHash).ToHashSet(),
                postageStamper);

            // Confirm chunk's push.
            await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);
            
            // Update pin, if required.
            if (pin != null)
            {
                pin.SucceededProvisional(hashingResult, chunkRefs.Count);
                await dbContext.SaveChangesAsync();
            }

            return new JsonResult(new ManifestReferenceDto(hashingResult.Hash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
        
        // Helpers.
        private static async Task<IActionResult> ServeContentAsync(
            ReferencedMantarayManifest manifest,
            string contentPath,
            HttpContext httpContext,
            BeehiveChunkStore chunkStore)
        {
            var reference = await manifest.ResolveAddressToChunkReferenceAsync(contentPath);
            
            // Get metadata.
            var metadata = await manifest.GetResourceMetadataAsync(contentPath);
            
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

            return new FileStreamResult(dataStream, mimeType)
            {
                EnableRangeProcessing = true
            };
        }
    }
}