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

using Etherna.ExecContext.AsyncLocal;
using Hangfire.Server;
using System;

namespace Etherna.Beehive.Configs.Hangfire
{
    /// <summary>
    /// Opens an execution context of the Etherna.DomainEvents library for each Hangfire job: its event
    /// dispatcher requires an ambient one, and a job inherits none from a request. Scrinium opens the
    /// context of its own library with a filter of the same shape. Needed until
    /// https://etherna.atlassian.net/browse/DOME-9 frees the dispatcher from the execution context.
    /// </summary>
    internal sealed class DomainEventsExecutionContextFilter : IServerFilter
    {
        // Consts.
        private const string ContextHandlerKey = "DomainEventsExecutionContextHandler";

        // Methods.
        public void OnPerformed(PerformedContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Items.TryGetValue(ContextHandlerKey, out var contextHandler))
                ((IDisposable)contextHandler).Dispose();
        }

        public void OnPerforming(PerformingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.Items[ContextHandlerKey] = AsyncLocalContext.Instance.InitAsyncLocalContext();
        }
    }
}
