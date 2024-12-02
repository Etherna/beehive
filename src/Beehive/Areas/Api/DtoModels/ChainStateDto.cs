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

using Etherna.Beehive.Services.Utilities.Models;
using System;

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    public class ChainStateDto
    {
        public ChainStateDto(ChainState chainState)
        {
            ArgumentNullException.ThrowIfNull(chainState, nameof(chainState));

            Block = chainState.Block;
            CurrentPrice = chainState.CurrentPrice.ToPlurLong();
            SourceNodeId = chainState.SourceNodeId;
            TimeStamp = chainState.TimeStamp;
            TotalAmount = chainState.TotalAmount.ToPlurLong();
        }

        public long Block { get; }
        public long CurrentPrice { get; }
        public string SourceNodeId { get; }
        public DateTime TimeStamp { get; }
        public long TotalAmount { get; }
    }
}
