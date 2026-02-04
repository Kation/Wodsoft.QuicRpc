using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Wodsoft.QuicRpc.SourceGenerators;

namespace Wodsoft.QuicRpc.SourceGeneratorTest
{
    public class BuildTest
    {
        [Fact]
        public async Task SourceGenerator()
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            MSBuildLocator.RegisterInstance(instances.First());

            await Build(@"..\..\..\..\Wodsoft.QuicRpc.UnitTest\Wodsoft.QuicRpc.UnitTest.csproj");
        }

        private async Task Build(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(path);
            var compilation = await project.GetCompilationAsync();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new ISourceGenerator[] { new QuicRpcFunctionsGenerator(), new QuicRpcClientGenerator() }, driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGenerators(compilation!);
        }
    }
}
