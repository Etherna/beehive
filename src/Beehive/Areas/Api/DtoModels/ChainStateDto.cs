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

using Etherna.BeeNet.Models;
using System;

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    // Bee output for compatibility:
    // {
    //     "chainTip": 44678020,
    //     "block": 44678015,
    //     "totalAmount": "565891072859",
    //     "currentPrice": "81935"
    // }
    public sealed class ChainStateDto(
        ulong block,
        ulong chainTip,
        BzzValue totalAmount,
        BzzValue currentPrice,
        DateTimeOffset timeStamp)
    {
        public ulong ChainTip { get; } = chainTip;
        public ulong Block { get; } = block;
        public BzzValue TotalAmount { get; } = totalAmount;
        public BzzValue CurrentPrice { get; } = currentPrice;
        public DateTimeOffset TimeStamp { get; } = timeStamp;
    }
}