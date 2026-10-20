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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// A logger keeping the events it receives, to assert what a flow logged
    /// </summary>
    internal sealed class CapturingLogger : ILogger
    {
        // Properties.
        public List<(LogLevel Level, string EventName, string Message)> Events { get; } = [];

        // Methods.
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            Events.Add((logLevel, eventId.Name ?? "", formatter(state, exception)));
        }
    }
}
