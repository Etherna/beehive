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

using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.SwarmSdk.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public interface INodePinsImportService
    {
        /// <summary>
        /// Delete the finished imports of a node, with their pin records: the failed ones, the completed ones,
        /// or both. A running import is never deleted
        /// </summary>
        /// <param name="failed">True for the failed imports only, false for the completed ones only, null for both</param>
        /// <returns>The deleted imports</returns>
        Task<long> DeleteImportsAsync(string nodeId, bool? failed);

        /// <summary>
        /// Every import of every node, newest first. An import whose job is gone without completing it is failed
        /// </summary>
        Task<IReadOnlyList<NodePinsImport>> GetImportsAsync(int page, int take);

        /// <summary>
        /// The contents pinned on a node right now, and the ones among them that beehive doesn't hold in full yet
        /// </summary>
        Task<NodePinsSnapshot> GetPinsSnapshotAsync(string nodeId);

        /// <summary>
        /// Start the import of the pins of a node, as a background job
        /// </summary>
        /// <param name="references">The contents to import: null or empty for every pin of the node</param>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.ResourceLockException">An import of the node is still running</exception>
        Task<NodePinsImport> StartImportAsync(
            string nodeId,
            bool unpinImported,
            IEnumerable<SwarmReference>? references = null);

        /// <summary>
        /// The running or last import of a node: null when the node was never imported
        /// </summary>
        Task<NodePinsImport?> TryGetLatestImportAsync(string nodeId);
    }
}
