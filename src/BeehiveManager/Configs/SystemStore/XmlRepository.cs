// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.MongODM.Core.Options;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Etherna.BeehiveManager.Configs.SystemStore
{
    internal sealed class XmlRepository : IXmlRepository
    {
        // Consts.
        public const string CollectionName = "dataProtectionKeys";

        // Fields.
        private readonly IMongoCollection<BsonDocument> collection;

        // Constructors.
        public XmlRepository(DbContextOptions options)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            // Initialize MongoDB driver.
#pragma warning disable CA2000 // Dispose objects before losing scope
            var client = new MongoClient(options.ConnectionString);
#pragma warning restore CA2000 // Dispose objects before losing scope
            var database = client.GetDatabase(options.DbName);
            collection = database.GetCollection<BsonDocument>(CollectionName);
        }

        // Methods.
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return collection.AsQueryable().ToList().Select(bsonDoc =>
            {
                //remove unnecessary document id added by mongodb
                bsonDoc.Remove("_id");

                var jsonStr = bsonDoc.ToJson();
                var xDocument = JsonConvert.DeserializeXNode(jsonStr)!;
                return xDocument.Root!;
            }).ToList();
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            ArgumentNullException.ThrowIfNull(element, nameof(element));

            //remove all comments. Json doesn't support it, but Json.NET serialize them anyway
            element.DescendantNodes().Where(x => x.NodeType == XmlNodeType.Comment).Remove();

            var jsonStr = JsonConvert.SerializeXNode(element);
            var bsonDoc = BsonDocument.Parse(jsonStr);
            collection.InsertOne(bsonDoc);
        }
    }
}
