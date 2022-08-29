using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Persistence.Helpers;
using Etherna.DomainEvents;
using Etherna.MongoDB.Bson.IO;
using Etherna.MongoDB.Bson.Serialization;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Serialization.Serializers;
using Etherna.MongODM.Core.Utility;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
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

                // "6b94df32-034f-46f9-a5c1-239905ad5d07" - v0.3.0
                {
                    var sourceDocument =
                        @"{
                            ""_id"" : ObjectId(""62d9e1ab5b300b294022c2c6""),
                            ""_m"" : ""6b94df32-034f-46f9-a5c1-239905ad5d07"",
                            ""CreationDateTime"" : ISODate(""2022-07-21T23:47:54.036+0000""),
                            ""Addresses"" : {
                                ""_m"" : ""b4fc3145-6864-43d0-8ba5-c43f36877519"",
                                ""Ethereum"" : ""0xa5014fFebdaf630e13BAddFb8531bE2d002439B3"",
                                ""Overlay"" : ""49579b44d869e9ad4e37825aa32e819ceb6f059452a0dc51740e06f4ad082c77"",
                                ""PssPublicKey"" : ""03fdaf7fa8fb061a4a641f3e3970bc960574f36d0a3ca28e721a7bc5762a198bf3"",
                                ""PublicKey"" : ""039563dc29f0f764aa907f468a9e2905d8dda557f0faf32e6f71f7e87853b36bf1""
                            },
                            ""ConnectionScheme"" : ""http"",
                            ""DebugPort"" : NumberInt(1635),
                            ""GatewayPort"" : NumberInt(1633),
                            ""Hostname"" : ""127.0.0.1""
                        }";

                    var expectedNodeMock = new Mock<BeeNode>();
                    expectedNodeMock.Setup(n => n.Id).Returns("62d9e1ab5b300b294022c2c6");
                    {
                        var addressesMock = new Mock<BeeNodeAddresses>();
                        addressesMock.Setup(a => a.Ethereum).Returns("0xa5014fFebdaf630e13BAddFb8531bE2d002439B3");
                        addressesMock.Setup(a => a.Overlay).Returns("49579b44d869e9ad4e37825aa32e819ceb6f059452a0dc51740e06f4ad082c77");
                        addressesMock.Setup(a => a.PssPublicKey).Returns("03fdaf7fa8fb061a4a641f3e3970bc960574f36d0a3ca28e721a7bc5762a198bf3");
                        addressesMock.Setup(a => a.PublicKey).Returns("039563dc29f0f764aa907f468a9e2905d8dda557f0faf32e6f71f7e87853b36bf1");
                        expectedNodeMock.Setup(n => n.Addresses).Returns(addressesMock.Object);
                    }
                    expectedNodeMock.Setup(n => n.CreationDateTime).Returns(new DateTime(2022, 07, 21, 23, 47, 54, 036));
                    expectedNodeMock.Setup(n => n.ConnectionScheme).Returns("http");
                    expectedNodeMock.Setup(n => n.DebugPort).Returns(1635);
                    expectedNodeMock.Setup(n => n.GatewayPort).Returns(1633);
                    expectedNodeMock.Setup(n => n.Hostname).Returns("127.0.0.1");

                    tests.Add(new DeserializationTestElement<BeeNode>(sourceDocument, expectedNodeMock.Object));
                }

                return tests.Select(t => new object[] { t });
            }
        }

        // Tests.
        [Theory, MemberData(nameof(BeeNodeDeserializationTests))]
        public void BeeNodeDeserialization(DeserializationTestElement<BeeNode> testElement)
        {
            if (testElement is null)
                throw new ArgumentNullException(nameof(testElement));

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
            Assert.Equal(testElement.ExpectedModel.Addresses?.Ethereum, result.Addresses?.Ethereum);
            Assert.Equal(testElement.ExpectedModel.Addresses?.Overlay, result.Addresses?.Overlay);
            Assert.Equal(testElement.ExpectedModel.Addresses?.PssPublicKey, result.Addresses?.PssPublicKey);
            Assert.Equal(testElement.ExpectedModel.Addresses?.PublicKey, result.Addresses?.PublicKey);
            Assert.Equal(testElement.ExpectedModel.CreationDateTime, result.CreationDateTime);
            Assert.Equal(testElement.ExpectedModel.ConnectionScheme, result.ConnectionScheme);
            Assert.Equal(testElement.ExpectedModel.DebugPort, result.DebugPort);
            Assert.Equal(testElement.ExpectedModel.GatewayPort, result.GatewayPort);
            Assert.Equal(testElement.ExpectedModel.Hostname, result.Hostname);
            Assert.NotNull(result.Id);
            Assert.NotNull(result.ConnectionScheme);
            Assert.NotNull(result.Hostname);
        }
    }
}
