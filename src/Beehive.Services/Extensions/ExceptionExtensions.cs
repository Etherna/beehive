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

using Etherna.SwarmSdk.Exceptions;
using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Extensions
{
    public static class ExceptionExtensions
    {
        // Consts.
        private const string BeeErrorMessagePropertyName = "message";

        // Methods.
        /// <summary>
        /// The reason to report for a failed call to a bee node: the message of the node's error body when the node
        /// answered one, the exception message otherwise
        /// </summary>
        public static string GetBeeNodeFailureReason(this Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (exception is not SwarmSdkApiException apiException)
                return exception.Message;

            // The node answered: bee's error body is a json object carrying the reason in "message".
            if (!string.IsNullOrWhiteSpace(apiException.Response))
            {
                try
                {
                    using var document = JsonDocument.Parse(apiException.Response);
                    if (document.RootElement.ValueKind == JsonValueKind.Object &&
                        document.RootElement.TryGetProperty(BeeErrorMessagePropertyName, out var message) &&
                        message.ValueKind == JsonValueKind.String)
                        return message.GetString()!;
                }
                catch (JsonException) { }
            }

            // Without a readable body, the first line of the message: the sdk appends the status and the body to it.
            var messageEnd = apiException.Message.AsSpan().IndexOfAny('\r', '\n');
            return messageEnd < 0 ? apiException.Message : apiException.Message[..messageEnd];
        }

        /// <summary>
        /// Whether the exception reports a bee node that could not serve a call, instead of a defect of beehive
        /// </summary>
        public static bool IsBeeNodeCallFailure(this Exception exception) =>
            exception is SwarmSdkApiException or HttpRequestException or SocketException or TaskCanceledException;
    }
}
