#addin "Cake.FileHelpers&version=3.2.1"
#addin "Cake.MsDeploy&version=0.8.0"

#r "System.Text.Json"
using System.Text.Json;

//////////////////////////////////////////////////////////////////////
// CHANGE projectName VARIABLE
//////////////////////////////////////////////////////////////////////
var projectName = Argument("projectName", "bi-clickstream-validation");
var octopusPackageId = Argument("octopusPackageId", "Healthgrades.Hgdata." + projectName);

// System
var target = Argument("target", "Default");
var octopusServerUrl = Argument("OctoupsServerUrl", "https://octopus.aws.healthgrades.zone");
var octopusApiKey = Argument("OctopusApiKey", (string)null) ?? EnvironmentVariable("OctopusMasterFeedApiKey") ?? (string)null;

// Directories.
var buildToolsDirectory = Directory("build-tools");
var buildArtifactsDirectory = buildToolsDirectory + Directory("build-artifacts");
var buildOutputDirectory = buildToolsDirectory + Directory("build-output");
var buildMetaDataDirectory = buildOutputDirectory + Directory("build-metadata");
var octopusPublishDirectory = buildArtifactsDirectory + Directory("octopus");
var packagingBaseDir = buildOutputDirectory + Directory("packaging");
var publishDirectory = packagingBaseDir + Directory("build-metadata");
var helmPublishDirectory = buildArtifactsDirectory + Directory("helm");

// Aws
var dockerEcrBase = Argument("dockerEcrBase", "958306274796.dkr.ecr.us-east-1.amazonaws.com");
var dockerFile = Argument("DockerFile", "Dockerfile");
// Project
var dockerImageName = Argument("dockerImageName", "hgd-" + projectName);
var CI_JOB_TOKEN = Argument("CI_JOB_TOKEN", "CIJOBTOKENDEFAULT");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

public class GitVersionData
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public Object PreReleaseTag { get; set; }
    public string PreReleaseTagWithDash { get; set; }
    public string PreReleaseLabel { get; set; }
    public Object PreReleaseNumber { get; set; }
    public Object WeightedPreReleaseNumber { get; set; }
    public Object BuildMetaData { get; set; }
    public string BuildMetaDataPadded { get; set; }
    public string FullBuildMetaData { get; set; }
    public string MajorMinorPatch { get; set; }
    public string SemVer { get; set; }
    public string LegacySemVer { get; set; }
    public string LegacySemVerPadded { get; set; }
    public string AssemblySemVer { get; set; }
    public string AssemblySemFileVer { get; set; }
    public string FullSemVer { get; set; }
    public string InformationalVersion { get; set; }
    public string BranchName { get; set; }
    public string EscapedBranchName { get; set; }
    public string Sha { get; set; }
    public Object ShortSha { get; set; }
    public string NuGetVersionV2 { get; set; }
    public string NuGetVersion { get; set; }
    public string NuGetPreReleaseTagV2 { get; set; }
    public string NuGetPreReleaseTag { get; set; }
    public string VersionSourceSha { get; set; }
    public int CommitsSinceVersionSource { get; set; }
    public string CommitsSinceVersionSourcePadded { get; set; }
    public string CommitDate { get; set; }
}

GitVersionData gitVersionInfo = null;

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildToolsDirectory);
    CleanDirectory(buildOutputDirectory);
    CleanDirectory(buildArtifactsDirectory);
    CleanDirectory(buildMetaDataDirectory);
    CleanDirectory(buildMetaDataDirectory);
});

Task("Calculate-SemVer")
    .IsDependentOn("Clean")
    .Does(()=>
{
    var gvTxt = new StringBuilder();
    var procSettings = new ProcessSettings{
        Arguments = "gitversion /output json"
    };
    procSettings.SetRedirectStandardOutput(true)
            .SetRedirectedStandardOutputHandler(
                (str)=> { gvTxt.Append(str); return null; }
            );
    StartProcess("dotnet", procSettings);
    gitVersionInfo = JsonSerializer.Deserialize<GitVersionData>(gvTxt.ToString());
    var gvTxtFileName = buildMetaDataDirectory + File("gitversion.json");
    FileWriteText(gvTxtFileName, JsonSerializer.Serialize(gitVersionInfo, new JsonSerializerOptions { WriteIndented  = true }));
    Information(FileReadText(gvTxtFileName));

});

Task("Create-BuildTxt")
    .IsDependentOn("Calculate-SemVer")
    .Does(()=>
{
    var buildTxt = new [] {
        $@"build-number: {gitVersionInfo.InformationalVersion}",
        $@"commit-date: {gitVersionInfo.CommitDate}",
    };
    var buildTxtFileName = buildMetaDataDirectory + File("build.txt");
    FileWriteLines(buildTxtFileName, buildTxt);
    Information(FileReadText(buildTxtFileName));

    var gvTxtFileName = buildMetaDataDirectory + File("gitversion.json");
    FileWriteText(gvTxtFileName, JsonSerializer.Serialize(gitVersionInfo, new JsonSerializerOptions { WriteIndented  = true }));
    Information(FileReadText(gvTxtFileName));
});

Task("Create-BuildMetadata")
    .IsDependentOn("Clean")
    .IsDependentOn("Calculate-SemVer")
    .IsDependentOn("Create-BuildTxt");

Task("Get-Version")
    .Does(()=>
{
    var gvTxtFileName = buildMetaDataDirectory + File("gitversion.json");
    gitVersionInfo = JsonSerializer.Deserialize<GitVersionData>(System.IO.File.ReadAllText(gvTxtFileName));
    Information(gitVersionInfo.InformationalVersion);
});

Task("Build-Docker")
    .IsDependentOn("Get-Version")
    .Does(() =>
{
    Environment.SetEnvironmentVariable("DOCKER_BUILDKIT", "1", EnvironmentVariableTarget.Process);
    LocalStartProcess("docker", $"build . -t {dockerImageName}:{gitVersionInfo.FullSemVer} -f {dockerFile} --no-cache --progress=plain  --secret id=service_account_github_token,src=service_account_github_token.txt");
});

Task("Compile-Sources")
    .IsDependentOn("Get-Version")
    .Does(()=>
{
    CopyDirectory(buildOutputDirectory + Directory("build-metadata"), packagingBaseDir+ Directory("build-metadata"));
    CopyDirectory(Directory("k8s"), packagingBaseDir);
});


Task("OctoPack")
    .Does(()=>
{
    DotNetCoreTool($"octo pack --id={octopusPackageId} --basePath=\"{packagingBaseDir}\" --outFolder=\"{octopusPublishDirectory}\" --version=\"{gitVersionInfo.FullSemVer}\"");
});

Task("Push-Octopus")
    .Does(() =>
{
    foreach(var file in GetFiles(octopusPublishDirectory.ToString() + "/**/*.nupkg")){
        DotNetCoreTool($"octo push --package=\"{file.FullPath}\" --server=\"{octopusServerUrl}\" --apiKey=\"{octopusApiKey}\" --overwrite-mode=\"OverwriteExisting\"");
    }
});


Task("Push-Docker")
    .IsDependentOn("Get-Version")
    .IsDependentOn("Build-Docker")
    .Does(() =>
{
    Environment.SetEnvironmentVariable("DOCKER_BUILDKIT", "1", EnvironmentVariableTarget.Process);
    LocalStartProcess("docker", $"tag {dockerImageName}:{gitVersionInfo.FullSemVer} {dockerEcrBase}/{dockerImageName}:{gitVersionInfo.FullSemVer}");
    LocalStartProcess("docker", $"push {dockerEcrBase}/{dockerImageName}:{gitVersionInfo.FullSemVer}");

});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Set-Version")
    .IsDependentOn("Create-BuildMetadata");

Task("Build")
    .IsDependentOn("Set-Version")
    .IsDependentOn("Compile-Sources")
    .IsDependentOn("OctoPack");

Task("PushOctopusPackage")
    .IsDependentOn("Get-Version")
    .IsDependentOn("Push-Octopus");

Task("Build-Push-Docker")
    .IsDependentOn("Get-Version")
    .IsDependentOn("Build-Docker")
    .IsDependentOn("Push-Docker");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

public void LocalStartProcess(string command, string arguments)
{
    var startProcessReturn = StartProcess(command, arguments);
    if (startProcessReturn != 0)
    {
        throw new Exception($"ERROR: There is an issue your {command} command. Please check the above logs.");
    }
}