﻿using Neo4jClient.DataAnnotations.Cypher;
using Neo4jClient.DataAnnotations.Tests.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Xunit;

namespace Neo4jClient.DataAnnotations.Tests
{
    public class EntityExpressionVisitorTests
    {
        private static JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            Converters = new List<JsonConverter>() { new EntityConverter() },
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        private static Func<object, string> serializer = (entity) => JsonConvert.SerializeObject(entity, serializerSettings);

        public static List<object[]> ParamsData = new List<object[]>()
        {
            new object[] { (Expression<Func<ActorNode>>)(() => Params.Get<ActorNode>("actor")), "actor" },

            new object[] { (Expression<Func<int>>)(() => (int)Params.Get("actor")["address_location_latitude"]),
                "actor.address_location_latitude" },

            new object[] { (Expression<Func<string>>)(() => ((string[])Params.Get("actor")["roles"])[0]),
                "actor.roles[0]" },

            new object[] { (Expression<Func<string>>)(() => (Params.Get("actor")["roles"] as string[])[0]),
                "actor.roles[0]" },

            new object[] { (Expression<Func<string>>)(() => Params.Get<ActorNode>("actor").Roles.ElementAt(2)),
                "actor.Roles[2]" },

            new object[] { (Expression<Func<Location>>)(() => (Params.Get<ActorNode>("actor").Address as AddressWithComplexType).Location),
                "actor.NewAddressName.Location" },

            new object[] { (Expression<Func<double>>)(() => (Params.Get<ActorNode>("actor").Address as AddressWithComplexType).Location.Latitude),
                "actor.NewAddressName_Location_Latitude" },
        };

        [Theory]
        [MemberData("ParamsData", MemberType = typeof(EntityExpressionVisitorTests))]
        public void ParamsSerialization<T>(Expression<Func<T>> expression,
            string expectedText, bool useResolvedJsonName = true)
        {
            TestUtilities.AddEntityTypes();

            var retrievedMembers = Utilities.GetSimpleMemberAccessStretch(expression.Body, out var val);

            Assert.Equal(true, Utilities.HasParams(retrievedMembers));

            var paramText = Utilities
                .BuildParams(retrievedMembers, serializer, out var typeReturned, useResolvedJsonName: useResolvedJsonName);

            //if (expectedText != paramText)
            //    System.Diagnostics.Debugger.Launch();

            Assert.Equal(expectedText, paramText);
            Assert.Equal(typeof(T), typeReturned);
        }

        [Fact]
        public void AnonymousType()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () =>
                new { Name = "Ellen Pompeo", Born = Params.Get<ActorNode>("shondaRhimes").Born,
                    Roles = new string[] { "Meredith Grey" }, Age = 47.ToString() };

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(3, entityVisitor.Params[0].Count); //because we are accounting for an implicit convert to object type.
            Assert.Equal("Get(\"shondaRhimes\")", entityVisitor.Params[0][0].ToString());
            Assert.Equal(3, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            var result = newExpression.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Ellen Pompeo" },
                { "Born", null }, //to be later replaced at serialization with params
                { "Roles", new string[] {"Meredith Grey"} },
                { "Age", "47" }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void ComplexAnonymousType_ComplexName()
        {
            TestUtilities.AddEntityTypes();

            //the following is purely a test, and not necessarily a good example for neo4j cypher.
            Expression<Func<object>> expression = () => new { new AddressWithComplexType()
            {
                AddressLine = Params.Get("A")["AddressLine"] as string,
                Location = new Location()
                {
                    Latitude = (double)Params.Get("A")["Location_Latitude"],
                    Longitude = 56.90
                }
            }.Location };

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(2, entityVisitor.Params.Count);

            Assert.Equal(3, entityVisitor.Params[0].Count);
            Assert.Equal("Get(\"A\").get_Item(\"AddressLine\")", entityVisitor.Params[0][1].ToString());
            Assert.Equal(5, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            var result = newExpression.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Location_Latitude", 0.0 }, //because the inner property was assigned 0 by the GetParams method.
                { "Location_Longitude", 56.90 }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void EscapedComplexAnonymousType_SimpleName()
        {
            TestUtilities.AddEntityTypes();

            //the following is purely a test, and not necessarily a good example for neo4j cypher.
            Expression<Func<object>> expression = () => new
            {
                Address = (TestUtilities.Actor.Address as AddressWithComplexType)._(),
                Coordinates = new double[] { (TestUtilities.Actor.Address as AddressWithComplexType).Location.Latitude,
                    (double)Params.Get("shondaRhimes")["NewAddressName_Location_Longitude"] }
            };

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(1, entityVisitor.Params.Count);

            Assert.Equal(3, entityVisitor.Params[0].Count);
            Assert.Equal("Get(\"shondaRhimes\")", entityVisitor.Params[0][0].ToString());
            Assert.Equal(4, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            var result = newExpression.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Address", TestUtilities.Actor.Address as AddressWithComplexType }, //same object returned because it was escaped
                { "Coordinates", new double[] { (TestUtilities.Actor.Address as AddressWithComplexType).Location.Latitude, 0.0 } }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void With()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => TestUtilities.Actor.With(a => a.Born == Params.Get<ActorNode>("ellenPompeo").Born && a.Name == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(1, entityVisitor.Params.Count);

            Assert.Equal(2, entityVisitor.Params[0].Count);
            Assert.Equal("Get(\"ellenPompeo\").Born", entityVisitor.Params[0][1].ToString());
            Assert.Equal(3, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            var result = entityVisitor.WithPredicateNode.ExecuteExpression<ActorNode>();
            Assert.NotNull(result);

            Assert.Equal("Shonda Rhimes", result.Name);
            Assert.Equal(0, result.Born);
        }

        [Fact]
        public void AnonymousTypeMemberAccessWith()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => new { TestUtilities.Actor.Name, TestUtilities.Actor.Born, TestUtilities.Actor.Address }
                .With(a => a.Address.AddressLine == Params.Get<ActorNode>("shondaRhimes").Address.AddressLine && a.Name == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(1, entityVisitor.Params.Count);

            Assert.Equal(3, entityVisitor.Params[0].Count);
            Assert.Equal("Get(\"shondaRhimes\").Address", entityVisitor.Params[0][1].ToString());
            Assert.Equal(5, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            dynamic result = entityVisitor.WithPredicateNode.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Shonda Rhimes"},
                { "Born", 0 },
                { "NewAddressName_AddressLine", null },
                { "NewAddressName_City", null },
                { "NewAddressName_State", null },
                { "NewAddressName_Country", null }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void ComplexAnonymousTypeWith()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => new { TestUtilities.Actor.Name, TestUtilities.Actor.Born, Address = TestUtilities.Actor.Address as AddressWithComplexType }
            .With(a => a.Address == new AddressWithComplexType()
            {   //Use this style only if you're sure all the properties here are assigned, 
                //because this address object would replace the instance address property entirely.
                //Also note that there's a good chance the parameters set inline here wouldn't make it to the generated pattern.
                //This was done mainly for testing. 
                //Use a => a.Address.Location.Longitude == (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"] instead.

                AddressLine = Params.Get<ActorNode>("shondaRhimes").Address.AddressLine,
                Location = new Location()
                {
                    Latitude = 4.0,
                    Longitude = (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"]
                }
            } && a.Name == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(2, entityVisitor.Params.Count);

            dynamic result = entityVisitor.WithPredicateNode.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Shonda Rhimes"},
                { "Born", 0 },
                { "NewAddressName_AddressLine", null },
                { "NewAddressName_City", null },
                { "NewAddressName_State", null },
                { "NewAddressName_Country", null },
                { "NewAddressName_Location_Latitude", 4.0 },
                { "NewAddressName_Location_Longitude", 0.0 }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void ComplexAnonymousTypeMemberAccessWith()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => new { TestUtilities.Actor.Name, TestUtilities.Actor.Born, Address = TestUtilities.Actor.Address as AddressWithComplexType }
            .With(a => a.Address.Location.Longitude == new AddressWithComplexType()
            {   //Using this style, be sure that the parameters set inline here wouldn't make it to the generated pattern.
                //This is because this MemberInit is taken as an object value since it was accessed, and then used directly
                //This was done mainly for testing. 
                //Use a => a.Address.Location.Longitude == (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"] instead.

                AddressLine = Params.Get<ActorNode>("shondaRhimes").Address.AddressLine,
                Location = new Location()
                {
                    Latitude = 4.0,
                    Longitude = (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"]
                }
            }.Location.Longitude && a.Name == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(2, entityVisitor.Params.Count);

            dynamic result = entityVisitor.WithPredicateNode.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Shonda Rhimes"},
                { "Born", 0 },
                { "NewAddressName_AddressLine", null },
                { "NewAddressName_City", null },
                { "NewAddressName_State", null },
                { "NewAddressName_Country", null },
                { "NewAddressName_Location_Latitude", 0.0 },
                { "NewAddressName_Location_Longitude", 0.0 }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void AnonymousTypeComplexMemberWith()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => new { TestUtilities.Actor.Name, TestUtilities.Actor.Born, TestUtilities.Actor.Address }
            .With(a => a.Address == new AddressWithComplexType()
            {   //Use this style only if you're sure all the properties here are assigned, 
                //because this address object would replace the instance address property entirely.
                //Also note that there's a good chance the parameters set inline here wouldn't make it to the generated pattern.
                //This was done mainly for testing. 
                //Use a => a.Address.Location.Longitude == (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"] instead.

                AddressLine = Params.Get<ActorNode>("shondaRhimes").Address.AddressLine,
                Location = new Location()
                {
                    Latitude = 4.0,
                    Longitude = (double)Params.Get("ellenPompeo")["NewAddressName_Location_Longitude"]
                }
            } && a.Name == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(2, entityVisitor.Params.Count);

            dynamic result = entityVisitor.WithPredicateNode.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Shonda Rhimes"},
                { "Born", 0 },
                { "NewAddressName_AddressLine", null },
                { "NewAddressName_City", null },
                { "NewAddressName_State", null },
                { "NewAddressName_Country", null },
                //{ "NewAddressName_Location_Latitude", 4.0 },
                //{ "NewAddressName_Location_Longitude", 0.0 }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }

        [Fact]
        public void DictionaryWith()
        {
            TestUtilities.AddEntityTypes();

            Expression<Func<object>> expression = () => new Dictionary<string, object>()
            {
                { "Name", TestUtilities.Actor.Name },
                { "Born", TestUtilities.Actor.Born },
                { "Address", TestUtilities.Actor.Address }
            }.With(a => a["Address"] == Params.Get<ActorNode>("ellenPompeo").Address && (int)a["Born"] == 1671 && a["Name"] == "Shonda Rhimes");

            var entityVisitor = new EntityExpressionVisitor(serializer);
            var newExpression = entityVisitor.Visit(expression.Body);

            Assert.NotNull(newExpression);
            Assert.NotEqual(newExpression, expression.Body);

            Assert.Equal(4, entityVisitor.Params.Count);

            Assert.Equal(4, entityVisitor.Params[0].Count);
            Assert.Equal("Get(\"ellenPompeo\").Address", entityVisitor.Params[0][1].ToString());
            Assert.Equal(3, entityVisitor.ParamsPaths[0].Count);
            Assert.Equal("GetParams(0)", entityVisitor.ParamsPaths[0][0].ToString());

            dynamic result = entityVisitor.WithPredicateNode.ExecuteExpression<Dictionary<string, object>>();
            Assert.NotNull(result);

            Dictionary<string, object> tokensExpected = new Dictionary<string, object>()
            {
                { "Name", "Shonda Rhimes"},
                { "Born", 1671 },
                { "NewAddressName_AddressLine", null },
                { "NewAddressName_City", null },
                { "NewAddressName_State", null },
                { "NewAddressName_Country", null }
            };

            Assert.Equal(tokensExpected.Count, result.Count);

            foreach (var pair in result)
            {
                Assert.Equal(tokensExpected[pair.Key], pair.Value);
            }
        }
    }
}
