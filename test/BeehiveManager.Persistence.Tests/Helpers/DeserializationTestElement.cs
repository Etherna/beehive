//   Copyright 2021-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Domain;
using Etherna.MongoDB.Driver;
using Moq;
using System;

namespace Etherna.BeehiveManager.Persistence.Helpers
{
    public class DeserializationTestElement<TModel>
    {
        public DeserializationTestElement(string sourceDocument, TModel expectedModel) :
            this(sourceDocument, expectedModel, (_, _) => { })
        { }

        public DeserializationTestElement(
            string sourceDocument,
            TModel expectedModel,
            Action<Mock<IMongoDatabase>, IBeehiveDbContext> setupAction)
        {
            SourceDocument = sourceDocument;
            ExpectedModel = expectedModel;
            SetupAction = setupAction;
        }

        public string SourceDocument { get; }
        public TModel ExpectedModel { get; }
        public Action<Mock<IMongoDatabase>, IBeehiveDbContext> SetupAction { get; }
    }
}
