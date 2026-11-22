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

using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Pages.Admin
{
    public class ChunksMigrationModel(IChunksMigrationService chunksMigrationService) : PageModel
    {
        // Consts.
        public const int MaxListedMigrations = 20;

        // Properties.
        public DeprecatedChunksCount DeprecatedChunks { get; private set; } = new(0, 0);

        [TempData]
        public string? Message { get; set; }

        /// <summary>
        /// The newest migrations, the running or last one first
        /// </summary>
        public IReadOnlyList<ChunksMigration> Migrations { get; private set; } = [];

        // Methods.
        public async Task OnGetAsync()
        {
            DeprecatedChunks = await chunksMigrationService.CountDeprecatedChunksAsync(HttpContext.RequestAborted);
            Migrations = await chunksMigrationService.GetMigrationsAsync(0, MaxListedMigrations, HttpContext.RequestAborted);
        }

        public async Task<IActionResult> OnPostStartAsync()
        {
            try
            {
                var migration = await chunksMigrationService.StartMigrationAsync(HttpContext.RequestAborted);
                Message = $"Chunks migration started, job {migration.JobId}";
            }
            catch (ResourceLockException)
            {
                Message = "A chunks migration is still running";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostStopAsync(string migrationId)
        {
            ArgumentNullException.ThrowIfNull(migrationId);

            var migration = await chunksMigrationService.StopMigrationAsync(migrationId, HttpContext.RequestAborted);
            Message = migration.IsCompleted ?
                $"Chunks migration {migration.Id} already ended, {migration.State}" :
                $"Chunks migration {migration.Id}: stop asked, it stops at the end of the page it is migrating";

            return RedirectToPage();
        }
    }
}
