using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class AzureDevOpsTestLinker
{
    private static string organization = "<your-organization>";
    private static string project = "<your-project>";
    private static string pat = "<your-PAT>";
    private static string testDllPath = @"C:\Path\To\Your\TestAssembly.dll";
    private static string testAssemblyName = "<YourTestAssemblyName>";

    public static async Task Main()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://dev.azure.com/{Uri.EscapeDataString(organization)}/")
        };

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var asm = Assembly.LoadFrom(testDllPath);
        var allTestClasses = asm.GetTypes()
            .Where(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Any(m => m.GetCustomAttributes(false)
                                   .Any(attr => attr.GetType().FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")))
            .ToList();

        foreach (var testClass in allTestClasses)
        {
            var methods = testClass.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var testMethods = methods
                .Where(m => m.GetCustomAttributes(false)
                    .Any(attr => attr.GetType().FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"))
                .ToList();

            foreach (var method in testMethods)
            {
                string methodName = method.Name;
                string fullyQualifiedName = $"{testClass.FullName}.{methodName}";

                var searchUrl = $"{Uri.EscapeDataString(project)}/_apis/wit/wiql?api-version=7.1-preview.2";
                var query = new
                {
                    query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND [System.WorkItemType] = 'Test Case' AND [Microsoft.VSTS.TCM.AutomatedTestName] = '{fullyQualifiedName}' AND [Microsoft.VSTS.TCM.AutomatedTestStorage] = '{testAssemblyName}.dll'"
                };
                var queryContent = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");
                var searchResponse = await httpClient.PostAsync(searchUrl, queryContent);
                var searchResult = JsonConvert.DeserializeObject<dynamic>(await searchResponse.Content.ReadAsStringAsync());
                if (searchResult?.workItems?.Count > 0)
                {
                    Console.WriteLine($"⏭ Skipping '{fullyQualifiedName}' – already associated.");
                    continue;
                }

                Console.WriteLine($"🔨 Creating test case for: {methodName}");

                var patch = new[]
                {
                    new { op = "add", path = "/fields/System.Title", value = methodName }
                };

                var content = new StringContent(JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json-patch+json");
                var createUrl = $"{Uri.EscapeDataString(project)}/_apis/wit/workitems/$Test%20Case?api-version=7.1-preview.3";

                var response = await httpClient.PostAsync(createUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Failed to create test case: {response.StatusCode}\n{responseBody}");
                    continue;
                }

                var result = JsonConvert.DeserializeObject<dynamic>(responseBody);
                int testCaseId = (int)result.id;

                var automationPatch = new[]
                {
                    new { op = "add", path = "/fields/Microsoft.VSTS.TCM.AutomatedTestId", value = Guid.NewGuid().ToString() },
                    new { op = "add", path = "/fields/Microsoft.VSTS.TCM.AutomatedTestName", value = fullyQualifiedName },
                    new { op = "add", path = "/fields/Microsoft.VSTS.TCM.AutomatedTestStorage", value = testAssemblyName + ".dll" }
                };

                var automationContent = new StringContent(JsonConvert.SerializeObject(automationPatch), Encoding.UTF8, "application/json-patch+json");
                var patchUrl = $"{Uri.EscapeDataString(project)}/_apis/wit/workitems/{testCaseId}?api-version=7.1-preview.3";

                var patchResponse = await httpClient.PatchAsync(patchUrl, automationContent);
                var patchBody = await patchResponse.Content.ReadAsStringAsync();

                if (patchResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Linked {fullyQualifiedName} to test case ID {testCaseId}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to link automation: {patchResponse.StatusCode}\n{patchBody}");
                }
            }
        }

        Console.WriteLine("🎉 Done!");
    }
}

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
    {
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) { Content = content };
        return client.SendAsync(request);
    }
}
