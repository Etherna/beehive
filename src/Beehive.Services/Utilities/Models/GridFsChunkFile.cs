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

using Etherna.MongoDB.Bson;
using System;

namespace Etherna.Beehive.Services.Utilities.Models
{
    /// <summary>
    /// A file of the deprecated GridFS bucket of the chunks: its name is the hash of the chunk it holds
    /// </summary>
    public sealed class GridFsChunkFile(
        ObjectId id,
        string fileName,
        DateTime uploadDateTime)
    {
        public string FileName { get; } = fileName ?? throw new ArgumentNullException(nameof(fileName));
        public ObjectId Id { get; } = id;
        public DateTime UploadDateTime { get; } = uploadDateTime;
    }
}
