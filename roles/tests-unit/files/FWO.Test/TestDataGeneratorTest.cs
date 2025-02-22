using FWO.Basics.TestDataGeneration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Numerics;
using NUnit.Framework;
using NetTools;
using FWO.Api.Data;

namespace FWO.Test
{
    [TestFixture]
    public class TestDataGeneratorTest
    {
        [Test]
        public void ImportOneInstance()
        {
            // ARRANGE

            TestDataGenerator<ModellingConnection> testDataGenerator = new();
            string json = @"
            {
            ""id"": 0,
            ""app_id"": null,
            ""proposed_app_id"": null,
            ""app"": {},
            ""name"": """",
            ""reason"": """",
            ""is_interface"": false,
            ""used_interface_id"": null,
            ""is_requested"": false,
            ""is_published"": false,
            ""ticket_id"": null,
            ""common_service"": false,
            ""creator"": null,
            ""creation_date"": null,
            ""conn_prop"": """",
            ""extra_params"": """",
            ""services"": [],
            ""service_groups"": [],
            ""source_nwobjects"": [],
            ""source_approles"": [],
            ""destination_nwobjects"": [],
            ""destination_approles"": [],
            ""source_areas"": [],
            ""destination_areas"": [],
            ""source_other_groups"": [],
            ""destination_other_groups"": [],
            ""src_from_interface"": false,
            ""dst_from_interface"": false,
            ""interface_is_requested"": false,
            ""interface_is_rejected"": false,
            ""order_number"": 0,
            ""props"": null,
            ""extra_configs"": [],
            ""extra_configs_from_interface"": []
            }";
            ModellingConnection? generatedInstance;

            // ACT

            generatedInstance = testDataGenerator.ImportInstance(json).SingleInstance;

            // ASSERT

            Assert.Multiple(() =>
            {
                Assert.That(generatedInstance, Is.Not.Null, "Generated instance should not be null.");
                Assert.That(generatedInstance, Is.InstanceOf<ModellingConnection>(), "Generated instance should be an instance of ModellingConnection.");

                Assert.That(generatedInstance!.Id, Is.EqualTo(0), "Id should be 0.");
                Assert.That(generatedInstance.AppId, Is.Null, "AppId should be null.");
                Assert.That(generatedInstance.ProposedAppId, Is.Null, "ProposedAppId should be null.");
                Assert.That(generatedInstance.App, Is.Not.Null, "Owner should not be null.");
                Assert.That(generatedInstance.Name, Is.EqualTo(""), "Name should be an empty string.");
                Assert.That(generatedInstance.Reason, Is.EqualTo(""), "Reason should be an empty string.");
                Assert.That(generatedInstance.IsInterface, Is.False, "IsInterface should be false.");
                Assert.That(generatedInstance.UsedInterfaceId, Is.Null, "UsedInterfaceId should be null.");
                Assert.That(generatedInstance.IsRequested, Is.False, "IsRequested should be false.");
                Assert.That(generatedInstance.IsPublished, Is.False, "IsPublished should be false.");
                Assert.That(generatedInstance.TicketId, Is.Null, "TicketId should be null.");
                Assert.That(generatedInstance.IsCommonService, Is.False, "IsCommonService should be false.");
                Assert.That(generatedInstance.Creator, Is.Null, "Creator should be null.");
                Assert.That(generatedInstance.CreationDate, Is.Null, "CreationDate should be null.");
                Assert.That(generatedInstance.Properties, Is.EqualTo(""), "Properties should be an empty string.");
                Assert.That(generatedInstance.ExtraParams, Is.EqualTo(""), "ExtraParams should be an empty string.");
                
                // Listen prüfen
                Assert.That(generatedInstance.Services, Is.Empty, "Services should be an empty list.");
                Assert.That(generatedInstance.ServiceGroups, Is.Empty, "ServiceGroups should be an empty list.");
                Assert.That(generatedInstance.SourceAppServers, Is.Empty, "SourceAppServers should be an empty list.");
                Assert.That(generatedInstance.SourceAppRoles, Is.Empty, "SourceAppRoles should be an empty list.");
                Assert.That(generatedInstance.DestinationAppServers, Is.Empty, "DestinationAppServers should be an empty list.");
                Assert.That(generatedInstance.DestinationAppRoles, Is.Empty, "DestinationAppRoles should be an empty list.");
                Assert.That(generatedInstance.SourceAreas, Is.Empty, "SourceAreas should be an empty list.");
                Assert.That(generatedInstance.DestinationAreas, Is.Empty, "DestinationAreas should be an empty list.");
                Assert.That(generatedInstance.SourceOtherGroups, Is.Empty, "SourceOtherGroups should be an empty list.");
                Assert.That(generatedInstance.DestinationOtherGroups, Is.Empty, "DestinationOtherGroups should be an empty list.");
                
                // Boolean Flags
                Assert.That(generatedInstance.SrcFromInterface, Is.False, "SrcFromInterface should be false.");
                Assert.That(generatedInstance.DstFromInterface, Is.False, "DstFromInterface should be false.");
                Assert.That(generatedInstance.InterfaceIsRequested, Is.False, "InterfaceIsRequested should be false.");
                Assert.That(generatedInstance.InterfaceIsRejected, Is.False, "InterfaceIsRejected should be false.");
                
                Assert.That(generatedInstance.OrderNumber, Is.EqualTo(0), "OrderNumber should be 0.");
                Assert.That(generatedInstance.Props, Is.Null, "Props should be null.");
                Assert.That(generatedInstance.ExtraConfigs, Is.Empty, "ExtraConfigs should be an empty list.");
                Assert.That(generatedInstance.ExtraConfigsFromInterface, Is.Empty, "ExtraConfigsFromInterface should be an empty list.");
            });
        }

        [Test]
        public void GenerateOneInstance()
        {
            // ARRANGE
            TestDataGenerator<ModellingConnection> testDataGenerator = new();
            string json = @"
            {
                ""config"": [
                    {
                        ""set"": {
                            ""Name"": {
                                ""testinstance1"": 0.25,
                                ""testinstance2"": 0.25,
                                ""testinstance3"": 0.25,
                                ""testinstance4"": 0.25
                            }
                        }
                    }
                ]
            }";
            List<ModellingConnection> generatedInstances = new();
            int iterations = 100;
            List<string> validNames = new() { "testinstance1", "testinstance2", "testinstance3", "testinstance4" };


            // ACT
            for (int i = 1; i <= iterations; i++)
            {
                generatedInstances.Add(testDataGenerator.GenerateInstance(json).SingleInstance);
            }
            

            // ASSERT
            Assert.That(generatedInstances, Has.Count.EqualTo(iterations), "Die Anzahl der generierten Instanzen stimmt nicht mit iterations überein.");
            Assert.That(generatedInstances.All(instance => validNames.Contains(instance.Name)), Is.True, "Mindestens eine generierte Instanz hat einen ungültigen Namen.");
        }

        [Test]
        public void SetUpOneInstance()
        {
            // ARRANGE
            TestDataGenerator<ModellingConnection> testDataGenerator = new();
            string json = @"
            {
                ""config"": [
                    {
                        ""set"": {
                            ""name"": ""testinstance""
                        }
                    }
                ]
            }";
            ModellingConnection testInstance = new();

            // ACT
            testDataGenerator.SetUpInstance(testInstance, json);


            // ASSERT
            Assert.Multiple(() =>
            {
                Assert.That(testInstance, Is.Not.Null, "Generated instance should not be null.");
                Assert.That(testInstance, Is.InstanceOf<ModellingConnection>(), "Generated instance should be an instance of ModellingConnection.");

                Assert.That(testInstance!.Id, Is.EqualTo(0), "Id should be 0.");
            });
        }

    }

}
