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
using Etherna.Beehive.Extensions;
using Etherna.Beehive.HttpTransformers;
using Etherna.Beehive.Services.Chunks;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Manifest;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BzzControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IChunkService beeNetChunkService,
        IBeehiveDbContext dbContext,
        IHttpForwarder forwarder)
        : IBzzControllerService
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        public async Task<IResult> DownloadBzzAsync(
            SwarmAddress address,
            HttpContext httpContext)
        {
            // Try to get from chunk's db.
            try
            {
                var chunkStore = new DbChunkStore(dbContext);
                var chunkJoiner = new ChunkJoiner(chunkStore);
                var rootManifest = new ReferencedMantarayManifest(
                    chunkStore,
                    address.Hash);

                var chunkReference = await rootManifest.ResolveAddressToChunkReferenceAsync(address.Path)
                    .ConfigureAwait(false);

                var metadata = await rootManifest.GetResourceMetadataAsync(address);
                var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(
                    chunkReference,
                    null,
                    CancellationToken.None).ConfigureAwait(false);

                metadata.TryGetValue("Content-Type", out var contentType);
                metadata.TryGetValue("Filename", out var fileName);

                return Results.File(dataStream, contentType, fileName);
            }
            catch
            {
            } //proceed with forward on any error

            // Select node and forward request.
            var node = await beeNodeLiveManager.SelectDownloadNodeAsync(address);
            return await node.ForwardRequestAsync(
                forwarder,
                httpContext,
                new DownloadHttpTransformer());
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
            
            // Create db chunk store, with pinning if required.
            ChunkPin? pin = null;
            List<UploadedChunkRef> chunkRefs = [];
            if (pinContent)
            {
                pin = new ChunkPin(null); //set root hash later
                await dbContext.ChunkPins.CreateAsync(pin);
                pin = await dbContext.ChunkPins.FindOneAsync(pin.Id);
            }
            var dbChunkStore = new DbChunkStore(
                dbContext,
                onSavingChunk: c =>
                {
                    if (pin != null)
                        c.AddPin(pin);
                    chunkRefs.Add(new(c.Hash, batchId));
                });
            
            // Create and store chunks.
            SwarmChunkReference hashingResult;
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
                                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
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
                        null,
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
                    null,
                    null,
                    dbChunkStore);
                hashingResult = uploadResult.ChunkReference;
            }
            await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);
            
            // Update pin, if required.
            if (pin != null)
            {
                pin.SucceededProvisional(hashingResult);
                await dbContext.SaveChangesAsync();
            }

            return new JsonResult(new ChunkReferenceDto(
                hashingResult.Hash,
                hashingResult.EncryptionKey,
                hashingResult.UseRecursiveEncryption))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
    }
}