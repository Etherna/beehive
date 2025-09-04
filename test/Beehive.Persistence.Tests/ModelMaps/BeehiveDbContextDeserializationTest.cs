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
using Etherna.Beehive.Persistence.Helpers;
using Etherna.BeeNet.Models;
using Etherna.DomainEvents;
using Etherna.MongoDB.Bson.IO;
using Etherna.MongoDB.Bson.Serialization;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Serialization.Serializers;
using Etherna.MongODM.Core.Utility;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Xunit;
using PostageStamp = Etherna.Beehive.Domain.Models.PostageStamp;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
    public class BeehiveDbContextDeserializationTest
    {
        // Fields.
        private readonly BeehiveDbContext dbContext;
        private readonly Mock<IMongoDatabase> mongoDatabaseMock = new();

        // Constructor.
        public BeehiveDbContextDeserializationTest()
        {
            // Setup dbContext.
            var eventDispatcherMock = new Mock<IEventDispatcher>();
            dbContext = new BeehiveDbContext(eventDispatcherMock.Object, null);

            DbContextMockHelper.InitializeDbContextMock(dbContext, mongoDatabaseMock);
        }

        // Data.
        public static IEnumerable<object[]> BeeNodeDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<BeeNode>>();

                // "6b94df32-034f-46f9-a5c1-239905ad5d07" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("62d9e1ab5b300b294022c2c6"),
                            "_m" : "6b94df32-034f-46f9-a5c1-239905ad5d07",
                            "CreationDateTime" : ISODate("2022-07-21T23:47:54.036+0000"),
                            "ConnectionString" : "http://127.0.0.1:1633",
                            "IsBatchCreationEnabled" : true
                        }
                        """;

                    var expectedNodeMock = new Mock<BeeNode>();
                    expectedNodeMock.Setup(n => n.Id).Returns("62d9e1ab5b300b294022c2c6");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2022, 07, 21, 23, 47, 54, 036));
                    expectedNodeMock.Setup(n => n.ConnectionString).Returns(new Uri("http://127.0.0.1:1633", UriKind.Absolute));
                    expectedNodeMock.Setup(n => n.IsBatchCreationEnabled).Returns(true);

                    tests.Add(new DeserializationTestElement<BeeNode>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ChunkDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<Chunk>>();

                // "06aaf593-07af-4fca-99a9-bdc3718547d8" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683834b4fd678b310f161d23"),
                            "_m" : "06aaf593-07af-4fca-99a9-bdc3718547d8",
                            "CreationDateTime" : ISODate("2025-05-29T10:19:32.890+0000"),
                            "Hash" : "fd06a035dcba7c912eb76db8ba696a63b01c7253614f28ee975f1bb60fbb222d",
                            "IsSoc" : true,
                            "Payload" : BinData(0, "ABAAAAAAAABcnoa45zctbM5JJJklV1unLHANcvM2dNtAqyLpyOhrW7Rm20FRG7kB"),
                            "Pins" : [
                                {
                                    "_m" : "d04090e8-1246-4ab8-bd6b-a37d9339c638",
                                    "_id" : ObjectId("683834b4fd678b310f161d22")
                                }
                            ]
                        }
                        """;
                    
                    var expectedNodeMock = new Mock<Chunk>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683834b4fd678b310f161d23");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 19, 32, 890));
                    expectedNodeMock.Setup(n => n.Hash).Returns("fd06a035dcba7c912eb76db8ba696a63b01c7253614f28ee975f1bb60fbb222d");
                    expectedNodeMock.Setup(n => n.IsSoc).Returns(true);
                    expectedNodeMock.Setup(n => n.Payload).Returns(new byte[]
                    {
                        0, 16, 0, 0, 0, 0, 0, 0, 92, 158, 134, 184, 231, 55, 45, 108, 206, 73, 36, 153, 37, 87, 91, 167,
                        44, 112, 13, 114, 243, 54, 116, 219, 64, 171, 34, 233, 200, 232, 107, 91, 180, 102, 219, 65, 81,
                        27, 185, 1
                    });
                    {
                        var pinMock = new Mock<ChunkPin>();
                        pinMock.Setup(p => p.Id).Returns("683834b4fd678b310f161d22");
                        expectedNodeMock.Setup(n => n.Pins).Returns([pinMock.Object]);
                    }

                    tests.Add(new DeserializationTestElement<Chunk>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ChunkPinDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<ChunkPin>>();
                
                // "63e584f0-2298-4c8f-bbc2-a84b90d836c2" - v0.4.1
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("68b1d83ee745e3c427e2ff1b"),
                            "_m" : "63e584f0-2298-4c8f-bbc2-a84b90d836c2",
                            "CreationDateTime" : ISODate("2025-08-29T16:41:34.971+0000"),
                            "Reference" : "fec916246ac6f124c9d9ae2488e05311cda2e25e7e5cac2fdbe4f14914b29b9128ee51c12f9b4934756b5608d01451777788eea5df2770f36e6bc780e5f44f58",
                            "IsProcessed" : true,
                            "MissingChunks" : [

                            ],
                            "TotPinnedChunks" : NumberLong(392)
                        }
                        """;
                
                    var expectedNodeMock = new Mock<ChunkPin>();
                    expectedNodeMock.Setup(n => n.Id).Returns("68b1d83ee745e3c427e2ff1b");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 08, 29, 16, 41, 34, 971));
                    expectedNodeMock.Setup(n => n.Reference).Returns("fec916246ac6f124c9d9ae2488e05311cda2e25e7e5cac2fdbe4f14914b29b9128ee51c12f9b4934756b5608d01451777788eea5df2770f36e6bc780e5f44f58");
                    expectedNodeMock.Setup(n => n.IsProcessed).Returns(true);
                    expectedNodeMock.Setup(n => n.MissingChunks).Returns([]);
                    expectedNodeMock.Setup(n => n.TotPinnedChunks).Returns(392);
                    
                    tests.Add(new DeserializationTestElement<ChunkPin>(sourceDocument, expectedNodeMock.Object));
                }

                // "832d06b1-ed82-4f4f-9df9-ad24565df38d" - v0.4.0 - succeeded
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683834b4fd678b310f161d22"),
                            "_m" : "832d06b1-ed82-4f4f-9df9-ad24565df38d",
                            "CreationDateTime" : ISODate("2025-05-29T10:19:32.774+0000"),
                            "EncKey" : null,
                            "Hash" : "999ed1f9419ed5f5a410172e1a437fa83bd480942361ca73c511feb4736aed28",
                            "IsProcessed" : true,
                            "MissingChunks" : [

                            ],
                            "RecEnc" : false,
                            "TotPinnedChunks" : NumberLong(392)
                        }
                        """;
                
                    var expectedNodeMock = new Mock<ChunkPin>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683834b4fd678b310f161d22");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 19, 32, 774));
                    expectedNodeMock.Setup(n => n.Reference).Returns("999ed1f9419ed5f5a410172e1a437fa83bd480942361ca73c511feb4736aed28");
                    expectedNodeMock.Setup(n => n.IsProcessed).Returns(true);
                    expectedNodeMock.Setup(n => n.MissingChunks).Returns([]);
                    expectedNodeMock.Setup(n => n.TotPinnedChunks).Returns(392);
                    
                    tests.Add(new DeserializationTestElement<ChunkPin>(sourceDocument, expectedNodeMock.Object));
                }

                // "832d06b1-ed82-4f4f-9df9-ad24565df38d" - v0.4.0 - missing chunks
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683836940549de109dc450bb"),
                            "_m" : "832d06b1-ed82-4f4f-9df9-ad24565df38d",
                            "CreationDateTime" : ISODate("2025-05-29T10:27:32.106+0000"),
                            "EncKey" : "12345678919ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed28",
                            "Hash" : "999ed1f9419ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed28",
                            "IsProcessed" : true,
                            "MissingChunks" : [
                                "999ed1f9419ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed28"
                            ],
                            "RecEnc" : true,
                            "TotPinnedChunks" : NumberLong(0)
                        }
                        """;
                
                    var expectedNodeMock = new Mock<ChunkPin>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683836940549de109dc450bb");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 27, 32, 106));
                    expectedNodeMock.Setup(n => n.Reference).Returns("999ed1f9419ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed2812345678919ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed28");
                    expectedNodeMock.Setup(n => n.IsProcessed).Returns(true);
                    expectedNodeMock.Setup(n => n.MissingChunks).Returns(["999ed1f9419ed5f5a410172e1a437fa83bd480942361ca72c511feb4736aed28"]);
                    expectedNodeMock.Setup(n => n.TotPinnedChunks).Returns(0);
                    
                    tests.Add(new DeserializationTestElement<ChunkPin>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ChunkPinLockDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<ChunkPinLock>>();

                // "a73d46c1-b548-4461-b4d3-b947de2f97e9" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683868e863614b9c605374f1"),
                            "_m" : "a73d46c1-b548-4461-b4d3-b947de2f97e9",
                            "CreationDateTime" : ISODate("2025-05-29T14:02:16.555+0000"),
                            "ExclusiveAccess" : true,
                            "ExpirationTime" : ISODate("2025-05-29T15:02:16.555+0000"),
                            "LockedAt" : ISODate("2025-05-29T14:02:16.555+0000"),
                            "ResourceId" : "683834b4fd678b310f161d22"
                        }
                        """;
                
                    var expectedNodeMock = new Mock<ChunkPinLock>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683868e863614b9c605374f1");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 14, 02, 16, 555));
                    expectedNodeMock.Setup(n => n.ExclusiveAccess).Returns(true);
                    expectedNodeMock.Setup(n => n.ExpirationTime).Returns(new DateTime(2025, 05, 29, 15, 02, 16, 555));
                    expectedNodeMock.Setup(n => n.LockedAt).Returns(new DateTime(2025, 05, 29, 14, 02, 16, 555));
                    expectedNodeMock.Setup(n => n.ResourceId).Returns("683834b4fd678b310f161d22");
                    
                    tests.Add(new DeserializationTestElement<ChunkPinLock>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> PostageBatchCacheDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<PostageBatchCache>>();

                // "38f60c18-d20c-4d24-a619-2af9d7a5119f" - v0.4.0
                {
                    var sourceDocument = File.ReadAllText("TxtDocuments/38f60c18-d20c-4d24-a619-2af9d7a5119f");
                
                    var expectedNodeMock = new Mock<PostageBatchCache>();
                    expectedNodeMock.Setup(n => n.Id).Returns("68383479fd678b310f161d21");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 18, 33, 413));
                    expectedNodeMock.Setup(n => n.BatchId).Returns("c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f");
                    expectedNodeMock.Setup(n => n.Depth).Returns(25);
                    expectedNodeMock.Setup(n => n.IsImmutable).Returns(true);
                    expectedNodeMock.Setup(n => n.OwnerNodeId).Returns("68311505d850b75f33146f7c");
                
                    tests.Add(new DeserializationTestElement<PostageBatchCache>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> PostageBatchLockDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<PostageBatchLock>>();

                // "e26fdf55-0245-4ead-b20a-13296e69d61d" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683835b9cacad22df40bdff7"),
                            "ExclusiveAccess" : false,
                            "ResourceId" : "c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f",
                            "Counter" : NumberInt(1),
                            "CreationDateTime" : ISODate("2025-05-29T10:23:53.887+0000"),
                            "ExpirationTime" : ISODate("2025-05-29T11:23:53.838+0000"),
                            "LockedAt" : ISODate("2025-05-29T10:23:53.887+0000"),
                            "_m" : "e26fdf55-0245-4ead-b20a-13296e69d61d"
                        }
                        """;
                
                    var expectedNodeMock = new Mock<PostageBatchLock>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683835b9cacad22df40bdff7");
                    expectedNodeMock.Setup(n => n.ExclusiveAccess).Returns(false);
                    expectedNodeMock.Setup(n => n.ResourceId).Returns("c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f");
                    expectedNodeMock.Setup(n => n.Counter).Returns(1);
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 23, 53, 887));
                    expectedNodeMock.Setup(n => n.ExpirationTime).Returns(new DateTime(2025, 05, 29, 11, 23, 53, 838));
                    expectedNodeMock.Setup(n => n.LockedAt).Returns(new DateTime(2025, 05, 29, 10, 23, 53, 887));
                
                    tests.Add(new DeserializationTestElement<PostageBatchLock>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> PostageStampDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<PostageStamp>>();

                // "a7a3075b-ab81-4bd0-8a5e-a35a5a576a71" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683834b4fd678b310f161eaa"),
                            "_m" : "a7a3075b-ab81-4bd0-8a5e-a35a5a576a71",
                            "CreationDateTime" : ISODate("2025-05-29T10:19:32.975+0000"),
                            "BatchId" : "c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f",
                            "ChunkHash" : "eeb4cd7212fc58ab4362a322c25a5fb71278cba959e9c1f8f88d24bc9c2919c4",
                            "BucketId" : NumberInt(61108),
                            "BucketCounter" : NumberInt(1)
                        }
                        """;
                
                    var expectedNodeMock = new Mock<PostageStamp>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683834b4fd678b310f161eaa");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 19, 32, 975));
                    expectedNodeMock.Setup(n => n.BatchId).Returns("c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f");
                    expectedNodeMock.Setup(n => n.ChunkHash).Returns("eeb4cd7212fc58ab4362a322c25a5fb71278cba959e9c1f8f88d24bc9c2919c4");
                    expectedNodeMock.Setup(n => n.BucketId).Returns(61108);
                    expectedNodeMock.Setup(n => n.BucketCounter).Returns(1);
                
                    tests.Add(new DeserializationTestElement<PostageStamp>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> PushingChunkRefDeserializationTests
        {
            get
            {
                var tests = new List<DeserializationTestElement<PushingChunkRef>>();

                // "30e3473f-5d56-4821-9c66-aa8922b46942" - v0.4.0
                {
                    var sourceDocument =
                        """
                        {
                            "_id" : ObjectId("683834b5fd678b310f16202f"),
                            "_m" : "30e3473f-5d56-4821-9c66-aa8922b46942",
                            "CreationDateTime" : ISODate("2025-05-29T10:19:32.890+0000"),
                            "BatchId" : "c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f",
                            "FailedAttempts" : NumberInt(10),
                            "HandledDateTime" : ISODate("2025-05-29T10:22:33.892+0000"),
                            "Hash" : "038f0b1c228f83058bf0fb5cc4b572a01d67c2eb0fd81a197b944970b48d8428"
                        }
                        """;
                
                    var expectedNodeMock = new Mock<PushingChunkRef>();
                    expectedNodeMock.Setup(n => n.Id).Returns("683834b5fd678b310f16202f");
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2025, 05, 29, 10, 19, 32, 890));
                    expectedNodeMock.Setup(n => n.BatchId).Returns("c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f");
                    expectedNodeMock.Setup(n => n.FailedAttempts).Returns(10);
                    expectedNodeMock.Setup(n => n.HandledDateTime).Returns(new DateTime(2025, 05, 29, 10, 22, 33, 892));
                    expectedNodeMock.Setup(n => n.Hash).Returns("038f0b1c228f83058bf0fb5cc4b572a01d67c2eb0fd81a197b944970b48d8428");
                
                    tests.Add(new DeserializationTestElement<PushingChunkRef>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Theory, MemberData(nameof(BeeNodeDeserializationTests))]
        public void BeeNodeDeserialization(DeserializationTestElement<BeeNode> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<BeeNode>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.ConnectionString, result.ConnectionString);
            Assert.Equal(testElement.ExpectedModel.IsBatchCreationEnabled, result.IsBatchCreationEnabled);
            Assert.NotNull(result.Id);
            Assert.NotNull(result.ConnectionString);
        }
        
        [Theory, MemberData(nameof(ChunkDeserializationTests))]
        public void ChunkDeserialization(DeserializationTestElement<Chunk> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<Chunk>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.Hash, result.Hash);
            Assert.Equal(testElement.ExpectedModel.IsSoc, result.IsSoc);
            Assert.Equal(testElement.ExpectedModel.Payload, result.Payload);
            Assert.Equal(testElement.ExpectedModel.Pins, result.Pins, EntityModelEqualityComparer.Instance);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(ChunkPinDeserializationTests))]
        public void ChunkPinDeserialization(DeserializationTestElement<ChunkPin> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<ChunkPin>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.Reference, result.Reference);
            Assert.Equal(testElement.ExpectedModel.IsProcessed, result.IsProcessed);
            Assert.Equal(testElement.ExpectedModel.MissingChunks, result.MissingChunks);
            Assert.Equal(testElement.ExpectedModel.TotPinnedChunks, result.TotPinnedChunks);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(ChunkPinLockDeserializationTests))]
        public void ChunkPinLockDeserialization(DeserializationTestElement<ChunkPinLock> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<ChunkPinLock>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.ExclusiveAccess, result.ExclusiveAccess);
            Assert.Equal(testElement.ExpectedModel.ExpirationTime, result.ExpirationTime);
            Assert.Equal(testElement.ExpectedModel.LockedAt, result.LockedAt);
            Assert.Equal(testElement.ExpectedModel.ResourceId, result.ResourceId);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(PostageBatchCacheDeserializationTests))]
        public void PostageBatchCacheDeserialization(DeserializationTestElement<PostageBatchCache> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<PostageBatchCache>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.BatchId, result.BatchId);
            Assert.True(result.Buckets.Count() == PostageBuckets.BucketsSize);
            Assert.Equal(testElement.ExpectedModel.Depth, result.Depth);
            Assert.Equal(testElement.ExpectedModel.IsImmutable, result.IsImmutable);
            Assert.Equal(testElement.ExpectedModel.OwnerNodeId, result.OwnerNodeId);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(PostageBatchLockDeserializationTests))]
        public void PostageBatchLockDeserialization(DeserializationTestElement<PostageBatchLock> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<PostageBatchLock>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.ExclusiveAccess, result.ExclusiveAccess);
            Assert.Equal(testElement.ExpectedModel.ExpirationTime, result.ExpirationTime);
            Assert.Equal(testElement.ExpectedModel.LockedAt, result.LockedAt);
            Assert.Equal(testElement.ExpectedModel.ResourceId, result.ResourceId);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(PostageStampDeserializationTests))]
        public void PostageStampDeserialization(DeserializationTestElement<PostageStamp> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<PostageStamp>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.BatchId, result.BatchId);
            Assert.Equal(testElement.ExpectedModel.ChunkHash, result.ChunkHash);
            Assert.Equal(testElement.ExpectedModel.BucketId, result.BucketId);
            Assert.Equal(testElement.ExpectedModel.BucketCounter, result.BucketCounter);
            Assert.NotNull(result.Id);
        }
        
        [Theory, MemberData(nameof(PushingChunkRefDeserializationTests))]
        public void PushingChunkRefDeserialization(DeserializationTestElement<PushingChunkRef> testElement)
        {
            ArgumentNullException.ThrowIfNull(testElement, nameof(testElement));

            // Setup.
            using var documentReader = new JsonReader(testElement.SourceDocument);
            var modelMapSerializer = new ModelMapSerializer<PushingChunkRef>(dbContext);
            var deserializationContext = BsonDeserializationContext.CreateRoot(documentReader);
            testElement.SetupAction(mongoDatabaseMock, dbContext);

            // Action.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext); //run into a db execution context
            var result = modelMapSerializer.Deserialize(deserializationContext);

            // Assert.
            Assert.Equal(testElement.ExpectedModel.Id, result.Id);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.BatchId, result.BatchId);
            Assert.Equal(testElement.ExpectedModel.FailedAttempts, result.FailedAttempts);
            Assert.Equal(testElement.ExpectedModel.HandledDateTime, result.HandledDateTime);
            Assert.Equal(testElement.ExpectedModel.Hash, result.Hash);
            Assert.NotNull(result.Id);
        }
    }
}
