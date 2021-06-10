using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;
using System.Linq;
using Azure.Identity;
using Azure.Core;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Management.CosmosDB.Models;

namespace Company.Function
{
    public static class CosmosHttpTrigger
    {
        [FunctionName("CosmosHttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string accessToken = await GetAccessToken().ConfigureAwait(false);
            string connectionString = await GetConnectionString(accessToken).ConfigureAwait(false);
            IList<MyItem> items = GetItemsAsync(connectionString);

            string responseMessage = $"Items: {JsonConvert.SerializeObject(items)}";

            return new OkObjectResult(responseMessage);
        }

        private static async Task<string> GetAccessToken()
        {
            string scope = Environment.GetEnvironmentVariable("RESOURCECONNECTOR_TESTWEBAPPFUNCSYSTEMASSIGNEDIDENTITYCONNECTIONSUCCEEDED_SCOPE");
            ManagedIdentityCredential cred = new ManagedIdentityCredential();
            TokenRequestContext reqContext = new TokenRequestContext(new string[] { scope });
            AccessToken token = await cred.GetTokenAsync(reqContext).ConfigureAwait(false);
            return token.Token;
        }

        private static async Task<string> GetConnectionString(string accessToken)
        {
            string subscriptionId = "937bc588-a144-4083-8612-5f9ffbbddb14";
            string resourceGroupName = "servicelinker-test-win-group";
            string accountName = "servicelinker-table-cosmos";

            string endpoint = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{accountName}/listConnectionStrings?api-version=2019-12-12";
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage result = await httpClient.PostAsync(endpoint, new StringContent("")).ConfigureAwait(false);
            DatabaseAccountListConnectionStringsResult connStrResult = await result.Content.ReadAsAsync<DatabaseAccountListConnectionStringsResult>().ConfigureAwait(false);

            foreach (DatabaseAccountConnectionString connStr in connStrResult.ConnectionStrings)
            {
                if (connStr.Description.Contains("Primary") && connStr.Description.Contains("Table"))
                {
                    return connStr.ConnectionString;
                }
            }
            return null;
        }

        private static IList<MyItem> GetItemsAsync(string connectionString) 
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudTableClient tableClient = account.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference("MyItem");
            List<MyItem> items = table.ExecuteQuery(new TableQuery<MyItem>()).ToList();
            return items;
        }
    }

    public class MyItem : TableEntity
    {

        public string ItemId { get; set; }

        public string Name { get; set; }

        public object Value { get; set; }

        public MyItem()
        {

        }

        public MyItem(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }
    }
}
