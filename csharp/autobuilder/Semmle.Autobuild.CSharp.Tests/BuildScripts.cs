﻿using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Semmle.Util;
using Semmle.Autobuild.Shared;
using Semmle.Util.Logging;

namespace Semmle.Autobuild.CSharp.Tests
{
    /// <summary>
    /// Test class to script Autobuilder scenarios.
    /// For most methods, it uses two fields:
    /// - an IList to capture the the arguments passed to it
    /// - an IDictionary of possible return values.
    /// </summary>
    internal class TestActions : IBuildActions
    {
        /// <summary>
        /// List of strings passed to FileDelete.
        /// </summary>
        public IList<string> FileDeleteIn { get; } = new List<string>();

        void IBuildActions.FileDelete(string file)
        {
            FileDeleteIn.Add(file);
        }

        public IList<string> FileExistsIn { get; } = new List<string>();
        public IDictionary<string, bool> FileExists { get; } = new Dictionary<string, bool>();

        bool IBuildActions.FileExists(string file)
        {
            FileExistsIn.Add(file);
            if (FileExists.TryGetValue(file, out var ret))
                return ret;

            if (FileExists.TryGetValue(Path.GetFileName(file), out ret))
                return ret;

            throw new ArgumentException("Missing FileExists " + file);
        }

        public IList<string> RunProcessIn { get; } = new List<string>();
        public IDictionary<string, int> RunProcess { get; } = new Dictionary<string, int>();
        public IDictionary<string, string> RunProcessOut { get; } = new Dictionary<string, string>();
        public IDictionary<string, string> RunProcessWorkingDirectory { get; } = new Dictionary<string, string>();
        public HashSet<string> CreateDirectories { get; } = new HashSet<string>();
        public HashSet<(string, string)> DownloadFiles { get; } = new HashSet<(string, string)>();

        int IBuildActions.RunProcess(string cmd, string args, string? workingDirectory, IDictionary<string, string>? env, out IList<string> stdOut)
        {
            var pattern = string.IsNullOrEmpty(args) ? cmd : cmd + " " + args;
            RunProcessIn.Add(pattern);

            if (!RunProcessOut.TryGetValue(pattern, out var str))
                throw new ArgumentException("Missing RunProcessOut " + pattern);

            stdOut = str.Split("\n");

            RunProcessWorkingDirectory.TryGetValue(pattern, out var wd);

            if (wd != workingDirectory)
                throw new ArgumentException($"Unexpected RunProcessWorkingDirectory, got {wd ?? "null"} expected {workingDirectory ?? "null"} in {pattern}");

            if (!RunProcess.TryGetValue(pattern, out var ret))
                throw new ArgumentException("Missing RunProcess " + pattern);

            return ret;
        }

        int IBuildActions.RunProcess(string cmd, string args, string? workingDirectory, IDictionary<string, string>? env)
        {
            var pattern = string.IsNullOrEmpty(args) ? cmd : cmd + " " + args;
            RunProcessIn.Add(pattern);
            RunProcessWorkingDirectory.TryGetValue(pattern, out var wd);

            if (wd != workingDirectory)
                throw new ArgumentException($"Unexpected RunProcessWorkingDirectory, got {wd ?? "null"} expected {workingDirectory ?? "null"} in {pattern}");

            if (!RunProcess.TryGetValue(pattern, out var ret))
                throw new ArgumentException("Missing RunProcess " + pattern);

            return ret;
        }

        int IBuildActions.RunProcess(string cmd, string args, string? workingDirectory, IDictionary<string, string>? env, BuildOutputHandler onOutput, BuildOutputHandler onError)
        {
            var ret = (this as IBuildActions).RunProcess(cmd, args, workingDirectory, env, out var stdout);

            stdout.ForEach(line => onOutput(line));

            return ret;
        }

        public IList<string> DirectoryDeleteIn { get; } = new List<string>();

        void IBuildActions.DirectoryDelete(string dir, bool recursive)
        {
            DirectoryDeleteIn.Add(dir);
        }

        public IDictionary<string, bool> DirectoryExists { get; } = new Dictionary<string, bool>();

        bool IBuildActions.DirectoryExists(string dir)
        {
            if (!DirectoryExists.TryGetValue(dir, out var ret))
                throw new ArgumentException("Missing DirectoryExists " + dir);

            return ret;
        }

        public IDictionary<string, string?> GetEnvironmentVariable { get; } = new Dictionary<string, string?>();

        string? IBuildActions.GetEnvironmentVariable(string name)
        {
            GetEnvironmentVariable.TryGetValue(name, out var ret);
            return ret;
        }

        public string GetCurrentDirectory { get; set; } = "";

        string IBuildActions.GetCurrentDirectory()
        {
            return GetCurrentDirectory;
        }

        public IDictionary<string, string> EnumerateFiles { get; } = new Dictionary<string, string>();

        IEnumerable<string> IBuildActions.EnumerateFiles(string dir)
        {
            if (!EnumerateFiles.TryGetValue(dir, out var str))
                throw new ArgumentException("Missing EnumerateFiles " + dir);

            return str.Split("\n").Select(p => PathCombine(dir, p));
        }

        public IDictionary<string, string> EnumerateDirectories { get; } = new Dictionary<string, string>();

        IEnumerable<string> IBuildActions.EnumerateDirectories(string dir)
        {
            if (!EnumerateDirectories.TryGetValue(dir, out var str))
                throw new ArgumentException("Missing EnumerateDirectories " + dir);

            return string.IsNullOrEmpty(str)
                ? Enumerable.Empty<string>()
                : str.Split("\n").Select(p => PathCombine(dir, p));
        }

        public bool IsWindows { get; set; }

        bool IBuildActions.IsWindows() => IsWindows;

        public bool IsMacOs { get; set; }

        bool IBuildActions.IsMacOs() => IsMacOs;

        public bool IsRunningOnAppleSilicon { get; set; }

        bool IBuildActions.IsRunningOnAppleSilicon() => IsRunningOnAppleSilicon;

        public string PathCombine(params string[] parts)
        {
            return string.Join(IsWindows ? '\\' : '/', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        string IBuildActions.GetFullPath(string path) => path;

        string? IBuildActions.GetFileName(string? path) => Path.GetFileName(path?.Replace('\\', '/'));

        public string? GetDirectoryName(string? path)
        {
            var dir = Path.GetDirectoryName(path?.Replace('\\', '/'));
            return dir is null ? path : path?.Substring(0, dir.Length);
        }

        void IBuildActions.WriteAllText(string filename, string contents)
        {
        }

        public IDictionary<string, XmlDocument> LoadXml { get; } = new Dictionary<string, XmlDocument>();

        XmlDocument IBuildActions.LoadXml(string filename)
        {
            if (!LoadXml.TryGetValue(filename, out var xml))
                throw new ArgumentException("Missing LoadXml " + filename);
            return xml;
        }

        public string EnvironmentExpandEnvironmentVariables(string s)
        {
            foreach (var kvp in GetEnvironmentVariable)
                s = s.Replace($"%{kvp.Key}%", kvp.Value);

            return s;
        }

        public void CreateDirectory(string path)
        {
            if (!CreateDirectories.Contains(path))
                throw new ArgumentException($"Missing CreateDirectory, {path}");
        }

        public void DownloadFile(string address, string fileName, ILogger logger)
        {
            if (!DownloadFiles.Contains((address, fileName)))
                throw new ArgumentException($"Missing DownloadFile, {address}, {fileName}");
        }


        public IDiagnosticsWriter CreateDiagnosticsWriter(string filename) => new TestDiagnosticWriter();
    }

    internal class TestDiagnosticWriter : IDiagnosticsWriter
    {
        public IList<Semmle.Util.DiagnosticMessage> Diagnostics { get; } = new List<Semmle.Util.DiagnosticMessage>();

        public void AddEntry(Semmle.Util.DiagnosticMessage message) => this.Diagnostics.Add(message);

        public void Dispose() { }
    }

    /// <summary>
    /// A fake solution to build.
    /// </summary>
    internal class TestSolution : ISolution
    {
        public IEnumerable<SolutionConfigurationInSolution> Configurations => throw new NotImplementedException();

        public string DefaultConfigurationName => "Release";

        public string DefaultPlatformName => "x86";

        public string FullPath { get; set; }

        public Version ToolsVersion => new Version("14.0");

        public IEnumerable<IProjectOrSolution> IncludedProjects => throw new NotImplementedException();

        public TestSolution(string path)
        {
            FullPath = path;
        }
    }

    public class BuildScriptTests
    {
        private readonly TestActions actions = new TestActions();

        // Records the arguments passed to StartCallback.
        private readonly IList<string> startCallbackIn = new List<string>();

        private void StartCallback(string s, bool silent)
        {
            startCallbackIn.Add(s);
        }

        // Records the arguments passed to EndCallback
        private readonly IList<string> endCallbackIn = new List<string>();
        private readonly IList<int> endCallbackReturn = new List<int>();

        private void EndCallback(int ret, string s, bool silent)
        {
            endCallbackReturn.Add(ret);
            endCallbackIn.Add(s);
        }

        [Fact]
        public void TestBuildCommand()
        {
            var cmd = BuildScript.Create("abc", "def ghi", false, null, null);

            actions.RunProcess["abc def ghi"] = 1;
            cmd.Run(actions, StartCallback, EndCallback);
            Assert.Equal("abc def ghi", actions.RunProcessIn[0]);
            Assert.Equal("abc def ghi", startCallbackIn[0]);
            Assert.Equal("", endCallbackIn[0]);
            Assert.Equal(1, endCallbackReturn[0]);
        }

        [Fact]
        public void TestAnd1()
        {
            var cmd = BuildScript.Create("abc", "def ghi", false, null, null) & BuildScript.Create("codeql", null, false, null, null);

            actions.RunProcess["abc def ghi"] = 1;
            cmd.Run(actions, StartCallback, EndCallback);

            Assert.Equal("abc def ghi", actions.RunProcessIn[0]);
            Assert.Equal("abc def ghi", startCallbackIn[0]);
            Assert.Equal("", endCallbackIn[0]);
            Assert.Equal(1, endCallbackReturn[0]);
        }

        [Fact]
        public void TestAnd2()
        {
            var cmd = BuildScript.Create("codeql", null, false, null, null) & BuildScript.Create("abc", "def ghi", false, null, null);

            actions.RunProcess["abc def ghi"] = 1;
            actions.RunProcess["codeql"] = 0;
            cmd.Run(actions, StartCallback, EndCallback);

            Assert.Equal("codeql", actions.RunProcessIn[0]);
            Assert.Equal("codeql", startCallbackIn[0]);
            Assert.Equal("", endCallbackIn[0]);
            Assert.Equal(0, endCallbackReturn[0]);

            Assert.Equal("abc def ghi", actions.RunProcessIn[1]);
            Assert.Equal("abc def ghi", startCallbackIn[1]);
            Assert.Equal("", endCallbackIn[1]);
            Assert.Equal(1, endCallbackReturn[1]);
        }

        [Fact]
        public void TestOr1()
        {
            var cmd = BuildScript.Create("codeql", null, false, null, null) | BuildScript.Create("abc", "def ghi", false, null, null);

            actions.RunProcess["abc def ghi"] = 1;
            actions.RunProcess["codeql"] = 0;
            cmd.Run(actions, StartCallback, EndCallback);

            Assert.Equal("codeql", actions.RunProcessIn[0]);
            Assert.Equal("codeql", startCallbackIn[0]);
            Assert.Equal("", endCallbackIn[0]);
            Assert.Equal(0, endCallbackReturn[0]);
            Assert.Single(endCallbackReturn);
        }

        [Fact]
        public void TestOr2()
        {
            var cmd = BuildScript.Create("abc", "def ghi", false, null, null) | BuildScript.Create("codeql", null, false, null, null);

            actions.RunProcess["abc def ghi"] = 1;
            actions.RunProcess["codeql"] = 0;
            cmd.Run(actions, StartCallback, EndCallback);

            Assert.Equal("abc def ghi", actions.RunProcessIn[0]);
            Assert.Equal("abc def ghi", startCallbackIn[0]);
            Assert.Equal("", endCallbackIn[0]);
            Assert.Equal(1, endCallbackReturn[0]);

            Assert.Equal("codeql", actions.RunProcessIn[1]);
            Assert.Equal("codeql", startCallbackIn[1]);
            Assert.Equal("", endCallbackIn[1]);
            Assert.Equal(0, endCallbackReturn[1]);
        }

        [Fact]
        public void TestSuccess()
        {
            Assert.Equal(0, BuildScript.Success.Run(actions, StartCallback, EndCallback));
        }

        [Fact]
        public void TestFailure()
        {
            Assert.NotEqual(0, BuildScript.Failure.Run(actions, StartCallback, EndCallback));
        }

        [Fact]
        public void TestDeleteDirectorySuccess()
        {
            actions.DirectoryExists["trap"] = true;
            Assert.Equal(0, BuildScript.DeleteDirectory("trap").Run(actions, StartCallback, EndCallback));
            Assert.Equal("trap", actions.DirectoryDeleteIn[0]);
        }

        [Fact]
        public void TestDeleteDirectoryFailure()
        {
            actions.DirectoryExists["trap"] = false;
            Assert.NotEqual(0, BuildScript.DeleteDirectory("trap").Run(actions, StartCallback, EndCallback));
        }

        [Fact]
        public void TestDeleteFileSuccess()
        {
            actions.FileExists["csharp.log"] = true;
            Assert.Equal(0, BuildScript.DeleteFile("csharp.log").Run(actions, StartCallback, EndCallback));
            Assert.Equal("csharp.log", actions.FileExistsIn[0]);
            Assert.Equal("csharp.log", actions.FileDeleteIn[0]);
        }

        [Fact]
        public void TestDeleteFileFailure()
        {
            actions.FileExists["csharp.log"] = false;
            Assert.NotEqual(0, BuildScript.DeleteFile("csharp.log").Run(actions, StartCallback, EndCallback));
            Assert.Equal("csharp.log", actions.FileExistsIn[0]);
        }

        [Fact]
        public void TestTry()
        {
            Assert.Equal(0, BuildScript.Try(BuildScript.Failure).Run(actions, StartCallback, EndCallback));
        }

        private CSharpAutobuilder CreateAutoBuilder(bool isWindows,
            string? buildless = null,
            string? dotnetVersion = null,
            string cwd = @"C:\Project")
        {
            var codeqlUpperLanguage = Language.CSharp.UpperCaseName;
            actions.GetEnvironmentVariable[$"CODEQL_EXTRACTOR_{codeqlUpperLanguage}_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable[$"CODEQL_EXTRACTOR_{codeqlUpperLanguage}_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable[$"CODEQL_EXTRACTOR_{codeqlUpperLanguage}_ROOT"] = $@"C:\codeql\{codeqlUpperLanguage.ToLowerInvariant()}";
            actions.GetEnvironmentVariable[$"CODEQL_EXTRACTOR_{codeqlUpperLanguage}_DIAGNOSTIC_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_JAVA_HOME"] = @"C:\codeql\tools\java";
            actions.GetEnvironmentVariable["CODEQL_PLATFORM"] = isWindows ? "win64" : "linux64";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_OPTION_BUILDLESS"] = buildless;
            if (dotnetVersion is not null)
                actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_OPTION_DOTNET_VERSION"] = dotnetVersion;
            actions.GetEnvironmentVariable["ProgramFiles(x86)"] = isWindows ? @"C:\Program Files (x86)" : null;
            actions.GetCurrentDirectory = cwd;
            actions.IsWindows = isWindows;

            var options = new CSharpAutobuildOptions(actions);
            return new CSharpAutobuilder(actions, options);
        }

        [Fact]
        public void TestDefaultCSharpAutoBuilder()
        {
            actions.RunProcess["cmd.exe /C dotnet --info"] = 0;
            actions.RunProcess[@"cmd.exe /C dotnet clean C:\Project\test.csproj"] = 0;
            actions.RunProcess[@"cmd.exe /C dotnet restore C:\Project\test.csproj"] = 0;
            actions.RunProcess[@"cmd.exe /C dotnet build --no-incremental C:\Project\test.csproj"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists[@"C:\Project\test.csproj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbar.cs\ntest.csproj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            var xml = new XmlDocument();
            xml.LoadXml(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>");
            actions.LoadXml[@"C:\Project\test.csproj"] = xml;

            var autobuilder = CreateAutoBuilder(true);
            TestAutobuilderScript(autobuilder, 0, 4);
        }

        [Fact]
        public void TestLinuxCSharpAutoBuilder()
        {
            actions.RunProcess["dotnet --info"] = 0;
            actions.RunProcess[@"dotnet clean C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"dotnet restore C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"dotnet build --no-incremental C:\Project/test.csproj"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists[@"C:\Project/test.csproj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\ntest.cs\ntest.csproj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            var xml = new XmlDocument();
            xml.LoadXml(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>");
            actions.LoadXml[@"C:\Project/test.csproj"] = xml;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 0, 4);
        }

        [Fact]
        public void TestLinuxCSharpAutoBuilderExtractorFailed()
        {
            actions.FileExists["csharp.log"] = false;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\ntest.cs";
            actions.EnumerateDirectories[@"C:\Project"] = "";

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 1, 0);
        }

        [Fact]
        public void TestVsWhereSucceeded()
        {
            actions.IsWindows = true;
            actions.GetEnvironmentVariable["ProgramFiles(x86)"] = @"C:\Program Files (x86)";
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"] = true;
            actions.RunProcess[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe -prerelease -legacy -property installationPath"] = 0;
            actions.RunProcessOut[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe -prerelease -legacy -property installationPath"] = "C:\\VS1\nC:\\VS2\nC:\\VS3";
            actions.RunProcess[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe -prerelease -legacy -property installationVersion"] = 0;
            actions.RunProcessOut[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe -prerelease -legacy -property installationVersion"] = "10.0\n11.0\n16.0";

            var candidates = BuildTools.GetCandidateVcVarsFiles(actions).ToArray();
            Assert.Equal("C:\\VS1\\VC\\vcvarsall.bat", candidates[0].Path);
            Assert.Equal(10, candidates[0].ToolsVersion);
            Assert.Equal("C:\\VS2\\VC\\vcvarsall.bat", candidates[1].Path);
            Assert.Equal(11, candidates[1].ToolsVersion);
            Assert.Equal(@"C:\VS3\VC\Auxiliary\Build\vcvars32.bat", candidates[2].Path);
            Assert.Equal(16, candidates[2].ToolsVersion);
            Assert.Equal(@"C:\VS3\VC\Auxiliary\Build\vcvars64.bat", candidates[3].Path);
            Assert.Equal(16, candidates[3].ToolsVersion);
            Assert.Equal(@"C:\VS3\Common7\Tools\VsDevCmd.bat", candidates[4].Path);
            Assert.Equal(16, candidates[4].ToolsVersion);
            Assert.Equal(5, candidates.Length);
        }

        [Fact]
        public void TestVsWhereNotExist()
        {
            actions.IsWindows = true;
            actions.GetEnvironmentVariable["ProgramFiles(x86)"] = @"C:\Program Files (x86)";
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"] = false;

            var candidates = BuildTools.GetCandidateVcVarsFiles(actions).ToArray();
            Assert.Equal(4, candidates.Length);
        }

        [Fact]
        public void TestVcVarsAllBatFiles()
        {
            actions.IsWindows = true;
            actions.GetEnvironmentVariable["ProgramFiles(x86)"] = @"C:\Program Files (x86)";
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"] = false;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\vcvarsall.bat"] = true;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat"] = false;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\vcvarsall.bat"] = true;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat"] = false;

            var vcvarsfiles = BuildTools.VcVarsAllBatFiles(actions).ToArray();
            Assert.Equal(2, vcvarsfiles.Length);
        }

        private void TestAutobuilderScript(CSharpAutobuilder autobuilder, int expectedOutput, int commandsRun)
        {
            Assert.Equal(expectedOutput, autobuilder.GetBuildScript().Run(actions, StartCallback, EndCallback));

            // Check expected commands actually ran
            Assert.Equal(commandsRun, startCallbackIn.Count);
            Assert.Equal(commandsRun, endCallbackIn.Count);
            Assert.Equal(commandsRun, endCallbackReturn.Count);

            var action = actions.RunProcess.GetEnumerator();
            for (var cmd = 0; cmd < commandsRun; ++cmd)
            {
                Assert.True(action.MoveNext());

                Assert.Equal(action.Current.Key, startCallbackIn[cmd]);
                Assert.Equal(action.Current.Value, endCallbackReturn[cmd]);
            }
        }

        [Fact]
        public void TestLinuxBuildSh()
        {
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbuild/build.sh";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.RunProcess[@"/bin/chmod u+x C:\Project/build/build.sh"] = 0;
            actions.RunProcess[@"C:\Project/build/build.sh"] = 0;
            actions.RunProcessWorkingDirectory[@"C:\Project/build/build.sh"] = @"C:\Project/build";
            actions.FileExists["csharp.log"] = true;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 0, 2);
        }

        [Fact]
        public void TestLinuxBuildShCSharpLogMissing()
        {
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbuild.sh";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";

            actions.RunProcess[@"/bin/chmod u+x C:\Project/build.sh"] = 0;
            actions.RunProcess[@"C:\Project/build.sh"] = 0;
            actions.RunProcessWorkingDirectory[@"C:\Project/build.sh"] = @"C:\Project";
            actions.FileExists["csharp.log"] = false;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 1, 2);
        }

        [Fact]
        public void TestLinuxBuildShFailed()
        {
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbuild.sh";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";

            actions.RunProcess[@"/bin/chmod u+x C:\Project/build.sh"] = 0;
            actions.RunProcess[@"C:\Project/build.sh"] = 5;
            actions.RunProcessWorkingDirectory[@"C:\Project/build.sh"] = @"C:\Project";
            actions.FileExists["csharp.log"] = true;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 1, 2);
        }

        [Fact]
        public void TestWindowsBuildBat()
        {
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbuild.bat";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.RunProcess[@"cmd.exe /C C:\Project\build.bat"] = 0;
            actions.RunProcessWorkingDirectory[@"cmd.exe /C C:\Project\build.bat"] = @"C:\Project";
            actions.FileExists["csharp.log"] = true;

            var autobuilder = CreateAutoBuilder(true);
            TestAutobuilderScript(autobuilder, 0, 1);
        }

        [Fact]
        public void TestDotnetVersionNotInstalled()
        {
            actions.RunProcess["dotnet --list-sdks"] = 0;
            actions.RunProcessOut["dotnet --list-sdks"] = "2.1.2 [C:\\Program Files\\dotnet\\sdks]\n2.1.4 [C:\\Program Files\\dotnet\\sdks]";
            actions.RunProcess[@"chmod u+x scratch/.dotnet/dotnet-install.sh"] = 0;
            actions.RunProcess[@"scratch/.dotnet/dotnet-install.sh --channel release --version 2.1.3 --install-dir scratch/.dotnet"] = 0;
            actions.RunProcess[@"scratch/.dotnet/dotnet --info"] = 0;
            actions.RunProcess[@"scratch/.dotnet/dotnet clean C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"scratch/.dotnet/dotnet restore C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"scratch/.dotnet/dotnet build --no-incremental C:\Project/test.csproj"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists["test.csproj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.GetEnvironmentVariable["PATH"] = "/bin:/usr/bin";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\ntest.cs\ntest.csproj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            var xml = new XmlDocument();
            xml.LoadXml(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>");
            actions.LoadXml[@"C:\Project/test.csproj"] = xml;
            actions.DownloadFiles.Add(("https://dot.net/v1/dotnet-install.sh", "scratch/.dotnet/dotnet-install.sh"));
            actions.CreateDirectories.Add(@"scratch/.dotnet");

            var autobuilder = CreateAutoBuilder(false, dotnetVersion: "2.1.3");
            TestAutobuilderScript(autobuilder, 0, 7);
        }

        [Fact]
        public void TestDotnetVersionAlreadyInstalled()
        {
            actions.RunProcess["dotnet --list-sdks"] = 0;
            actions.RunProcessOut["dotnet --list-sdks"] = @"2.1.3 [C:\Program Files\dotnet\sdks]
2.1.4 [C:\Program Files\dotnet\sdks]";
            actions.RunProcess[@"dotnet --info"] = 0;
            actions.RunProcess[@"dotnet clean C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"dotnet restore C:\Project/test.csproj"] = 0;
            actions.RunProcess[@"dotnet build --no-incremental C:\Project/test.csproj"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists["test.csproj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.GetEnvironmentVariable["PATH"] = "/bin:/usr/bin";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\nbar.cs\ntest.csproj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            var xml = new XmlDocument();
            xml.LoadXml(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>");
            actions.LoadXml[@"C:\Project/test.csproj"] = xml;

            var autobuilder = CreateAutoBuilder(false, dotnetVersion: "2.1.3");
            TestAutobuilderScript(autobuilder, 0, 5);
        }

        private void TestDotnetVersionWindows(Action action, int commandsRun)
        {
            actions.RunProcess["cmd.exe /C dotnet --list-sdks"] = 0;
            actions.RunProcessOut["cmd.exe /C dotnet --list-sdks"] = "2.1.4 [C:\\Program Files\\dotnet\\sdks]";
            action();
            actions.RunProcess[@"cmd.exe /C scratch\.dotnet\dotnet --info"] = 0;
            actions.RunProcess[@"cmd.exe /C scratch\.dotnet\dotnet clean C:\Project\test.csproj"] = 0;
            actions.RunProcess[@"cmd.exe /C scratch\.dotnet\dotnet restore C:\Project\test.csproj"] = 0;
            actions.RunProcess[@"cmd.exe /C scratch\.dotnet\dotnet build --no-incremental C:\Project\test.csproj"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists[@"C:\Project\test.csproj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.GetEnvironmentVariable["PATH"] = "/bin:/usr/bin";
            actions.EnumerateFiles[@"C:\Project"] = "foo.cs\ntest.cs\ntest.csproj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            var xml = new XmlDocument();
            xml.LoadXml(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
  </PropertyGroup>

</Project>");
            actions.LoadXml[@"C:\Project\test.csproj"] = xml;

            var autobuilder = CreateAutoBuilder(true, dotnetVersion: "2.1.3");
            TestAutobuilderScript(autobuilder, 0, commandsRun);
        }

        [Fact]
        public void TestDotnetVersionWindowsWithPwsh()
        {
            TestDotnetVersionWindows(() =>
            {
                actions.RunProcess[@"cmd.exe /C pwsh -NoProfile -ExecutionPolicy unrestricted -Command ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -Version 2.1.3 -InstallDir scratch\.dotnet"""] = 0;
            },
            6);
        }

        [Fact]
        public void TestDotnetVersionWindowsWithoutPwsh()
        {
            TestDotnetVersionWindows(() =>
            {
                actions.RunProcess[@"cmd.exe /C pwsh -NoProfile -ExecutionPolicy unrestricted -Command ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -Version 2.1.3 -InstallDir scratch\.dotnet"""] = 1;
                actions.RunProcess[@"cmd.exe /C powershell -NoProfile -ExecutionPolicy unrestricted -Command ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -Version 2.1.3 -InstallDir scratch\.dotnet"""] = 0;
            },
            7);
        }

        [Fact]
        public void TestDirsProjWindows()
        {
            actions.RunProcess[@"cmd.exe /C nuget restore C:\Project\dirs.proj -DisableParallelProcessing"] = 1;
            actions.RunProcess[@"cmd.exe /C scratch\.nuget\nuget.exe restore C:\Project\dirs.proj -DisableParallelProcessing"] = 0;
            actions.RunProcess["cmd.exe /C CALL ^\"C:\\Program^ Files^ ^(x86^)\\Microsoft^ Visual^ Studio^ 12.0\\VC\\vcvarsall.bat^\" && set Platform=&& type NUL && msbuild C:\\Project\\dirs.proj /t:rebuild"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists[@"C:\Project\a\test.csproj"] = true;
            actions.FileExists[@"C:\Project\dirs.proj"] = true;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"] = false;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\vcvarsall.bat"] = false;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 12.0\VC\vcvarsall.bat"] = true;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\vcvarsall.bat"] = false;
            actions.FileExists[@"C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat"] = true;

            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.EnumerateFiles[@"C:\Project"] = "a\\test.cs\na\\test.csproj\ndirs.proj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.CreateDirectories.Add(@"scratch\.nuget");
            actions.DownloadFiles.Add(("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", @"scratch\.nuget\nuget.exe"));

            var csproj = new XmlDocument();
            csproj.LoadXml(@"<?xml version=""1.0"" encoding=""utf - 8""?>
  <Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <ItemGroup>
      <Compile Include=""test.cs"" />
    </ItemGroup>
  </Project>");
            actions.LoadXml[@"C:\Project\a\test.csproj"] = csproj;

            var dirsproj = new XmlDocument();
            dirsproj.LoadXml(@"<Project DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""3.5"">
  <ItemGroup>
    <ProjectFiles Include=""a\test.csproj"" />
  </ItemGroup>
</Project>");
            actions.LoadXml[@"C:\Project\dirs.proj"] = dirsproj;

            var autobuilder = CreateAutoBuilder(true);
            TestAutobuilderScript(autobuilder, 0, 3);
        }

        [Fact]
        public void TestDirsProjLinux()
        {
            actions.RunProcess[@"nuget restore C:\Project/dirs.proj -DisableParallelProcessing"] = 1;
            actions.RunProcess[@"mono scratch/.nuget/nuget.exe restore C:\Project/dirs.proj -DisableParallelProcessing"] = 0;
            actions.RunProcess[@"msbuild C:\Project/dirs.proj /t:rebuild"] = 0;
            actions.FileExists["csharp.log"] = true;
            actions.FileExists[@"C:\Project/a/test.csproj"] = true;
            actions.FileExists[@"C:\Project/dirs.proj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.EnumerateFiles[@"C:\Project"] = "a/test.cs\na/test.csproj\ndirs.proj";
            actions.EnumerateDirectories[@"C:\Project"] = "";
            actions.CreateDirectories.Add("scratch/.nuget");
            actions.DownloadFiles.Add(("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", "scratch/.nuget/nuget.exe"));

            var csproj = new XmlDocument();
            csproj.LoadXml(@"<?xml version=""1.0"" encoding=""utf - 8""?>
  <Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <ItemGroup>
      <Compile Include=""test.cs"" />
    </ItemGroup>
  </Project>");
            actions.LoadXml[@"C:\Project/a/test.csproj"] = csproj;

            var dirsproj = new XmlDocument();
            dirsproj.LoadXml(@"<Project DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""3.5"">
  <ItemGroup>
    <ProjectFiles Include=""a\test.csproj"" />
  </ItemGroup>
</Project>");
            actions.LoadXml[@"C:\Project/dirs.proj"] = dirsproj;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 0, 3);
        }

        [Fact]
        public void TestCyclicDirsProj()
        {
            actions.FileExists["dirs.proj"] = true;
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_TRAP_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SOURCE_ARCHIVE_DIR"] = "";
            actions.GetEnvironmentVariable["CODEQL_EXTRACTOR_CSHARP_SCRATCH_DIR"] = "scratch";
            actions.FileExists["csharp.log"] = false;
            actions.EnumerateFiles[@"C:\Project"] = "dirs.proj";
            actions.EnumerateDirectories[@"C:\Project"] = "";

            var dirsproj1 = new XmlDocument();
            dirsproj1.LoadXml(@"<Project DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""3.5"">
  <ItemGroup>
    <ProjectFiles Include=""dirs.proj"" />
  </ItemGroup>
</Project>");
            actions.LoadXml[@"C:\Project/dirs.proj"] = dirsproj1;

            var autobuilder = CreateAutoBuilder(false);
            TestAutobuilderScript(autobuilder, 1, 0);
        }

        [Fact]
        public void TestAsStringWithExpandedEnvVarsWindows()
        {
            actions.IsWindows = true;
            actions.GetEnvironmentVariable["LGTM_SRC"] = @"C:\repo";
            Assert.Equal(@"C:\repo\test", @"%LGTM_SRC%\test".AsStringWithExpandedEnvVars(actions));
        }

        [Fact]
        public void TestAsStringWithExpandedEnvVarsLinux()
        {
            actions.IsWindows = false;
            actions.GetEnvironmentVariable["LGTM_SRC"] = "/tmp/repo";
            Assert.Equal("/tmp/repo/test", "$LGTM_SRC/test".AsStringWithExpandedEnvVars(actions));
        }
    }
}
