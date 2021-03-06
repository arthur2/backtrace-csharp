﻿using Backtrace.Base;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Model.Database;
using Backtrace.Services;
using Backtrace.Types;
using Moq;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backtrace.Tests.Events
{
    /// <summary>
    /// Tests BacktraceAPI events 
    /// </summary>
    [TestFixture(Author = "Konrad Dysput", Category = "Events.TestClientEvents", Description = "Test client events")]
    public class ApiEventsTests
    {
        private BacktraceClient _backtraceClient;
        private BacktraceClient _clientWithInvalidParameters;

        [SetUp]
        public void Setup()
        {
            var credentials = new BacktraceCredentials("https://validurl.com/", "validToken");
            var invalidCredentials = new BacktraceCredentials("https://validurl.com/", "invalidToken");
            //mock API
            var serverUrl = $"{credentials.BacktraceHostUri.AbsoluteUri}post?format=json&token={credentials.Token}";
            var invalidUrl = $"{credentials.BacktraceHostUri.AbsoluteUri}post?format=json&token={credentials.Token}";

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(serverUrl)
                .Respond("application/json", "{'object' : 'aaa'}");

            mockHttp.When(invalidUrl)
                .Respond("application/json", "{'message': 'invalid data'}");
            var api = new BacktraceApi(credentials, 0)
            {
                HttpClient = mockHttp.ToHttpClient()
            };

            var apiWithInvalidUrl = new BacktraceApi(invalidCredentials, 100)
            {
                HttpClient = mockHttp.ToHttpClient()
            };


            //mock database
            var database = new Mock<IBacktraceDatabase>();
            database.Setup(n =>
                n.Add(It.IsAny<BacktraceReport>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<MiniDumpType>()));

            database.Setup(n =>
               n.Delete(It.IsAny<BacktraceDatabaseRecord>()));

            //setup new client
            _backtraceClient = new BacktraceClient(credentials, database: database.Object, reportPerMin: 0)
            {
                BacktraceApi = api,
                Database = database.Object
            };
            _clientWithInvalidParameters = new BacktraceClient(invalidCredentials, database: database.Object, reportPerMin: 0)
            {
                BacktraceApi = apiWithInvalidUrl,
                Database = database.Object
            };
        }

        [Test(Author = "Konrad Dysput", Description = "Test valid report submission")]
        public async Task TestValidSubmissionEvents()
        {
            bool responseEventTrigger = false;
            int totalResponses = 0;
            _backtraceClient.OnServerResponse = (BacktraceResult res) =>
            {
                totalResponses++;
                responseEventTrigger = true;
                Assert.AreEqual(res.Object, "aaa");
            };
            await _backtraceClient.SendAsync("custom message");
            await _backtraceClient.SendAsync(new Exception("Backtrace API tests"));
            await _backtraceClient.SendAsync(new BacktraceReport("backtrace report message"));
            await _backtraceClient.SendAsync(new BacktraceReport(new Exception("Backtrace report exception")));

            Assert.IsTrue(responseEventTrigger);
            Assert.AreEqual(totalResponses, 4);
        }

        [Test(Author = "Konrad Dysput", Description = "Test invalid report submission")]
        public async Task TestInvalidSubmissionEvents()
        {
            bool responseEventTrigger = false;
            int totalResponses = 0;
            _clientWithInvalidParameters.OnServerError = (Exception e) =>
            {
                totalResponses++;
                responseEventTrigger = true;
            };

            await _clientWithInvalidParameters.SendAsync("custom message");
            await _clientWithInvalidParameters.SendAsync(new Exception("Backtrace API tests"));
            await _clientWithInvalidParameters.SendAsync(new BacktraceReport("backtrace report message"));
            await _clientWithInvalidParameters.SendAsync(new BacktraceReport(new Exception("Backtrace report exception")));

            Assert.IsTrue(responseEventTrigger);
            Assert.AreEqual(totalResponses, 4);
        }
    }
}
