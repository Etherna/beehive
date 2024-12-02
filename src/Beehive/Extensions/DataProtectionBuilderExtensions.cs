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

using Etherna.Beehive.Configs.SystemStore;
using Etherna.MongODM.Core.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Etherna.Beehive.Extensions
{
    public static class DataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to persist keys to a MongoDb datastore
        /// </summary>
        /// <param name="builder">The <see cref="IDataProtectionBuilder"/> instance to modify.</param>
        /// <param name="dbContextOptions">Options for dbContext</param>
        /// <returns>The value <paramref name="builder"/>.</returns>
        public static IDataProtectionBuilder PersistKeysToDbContext(
            this IDataProtectionBuilder builder,
            DbContextOptions dbContextOptions)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.Services.Configure<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new XmlRepository(dbContextOptions);
            });

            return builder;
        }
    }
}
