using System.Net;
using Octokit;
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

public static async Task Run(HttpRequestMessage req, TraceWriter log)
{
    // Get request body
    dynamic data = await req.Content.ReadAsAsync<object>();

    // Extract github release from request body
    string releaseBody = data?.release?.body;
    string releaseName = data?.release?.name;
    string repositoryName = data?.repository?.full_name;

    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageAccountConnectionString"));
    var blobClient = storageAccount.CreateCloudBlobClient();
    var container = blobClient.GetContainerReference("releases");
    var blob = container.GetBlockBlobReference(releaseName + ".md" );

    string txtIssues = await GetReleaseDetails(IssueTypeQualifier.Issue, repositoryName);
    string txtPulls = await GetReleaseDetails(IssueTypeQualifier.PullRequest, repositoryName);
    
    var text = String.Format("# {0} \n {1} \n\n" + "# Issues Closed:" + txtIssues + "\n\n# Changes Merged:" + txtPulls, releaseName, releaseBody);

    await blob.UploadTextAsync(text);    
}

public static async Task<string> GetReleaseDetails(IssueTypeQualifier type, string repoName)
{
    //Connect to client with OAuth App
    var github = new GitHubClient(new ProductHeaderValue(Environment.GetEnvironmentVariable("ReleaseNotes")));
    var twoWeeks = DateTime.Now.Subtract(TimeSpan.FromDays(14));
    var range = new DateRange(twoWeeks, SearchQualifierOperator.GreaterThanOrEqualTo);
    var request = new SearchIssuesRequest();

    request.Repos.Add(repoName);
    request.Type = type;

    //Find Issues or PRs closed or merged within the past 14 days in specified Repo
    if (type == IssueTypeQualifier.Issue)
    {
        request.Closed = range;
    }
    else
    {
        request.Merged = range;
    }

    var issues = await github.Search.SearchIssues(request);

    //Iterate and format text 
    string searchResults = string.Empty;
    foreach(Issue x in issues.Items)
    {
      searchResults += String.Format("\n - [{0}]({1})", x.Title, x.HtmlUrl);
    }

    return searchResults;
}