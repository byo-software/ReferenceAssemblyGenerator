# ReferenceAssemblyGenerator
A dotnet tool to generate reference assemblies.

### What is a reference assembly?
[Reference assemblies](https://github.com/dotnet/roslyn/blob/master/docs/features/refout.md) are assemblies which only contain metadata but no executable IL code.

### Why would you need this?
Sometimes you do not want to ship executable code but only metadata for developers to interact with your dll.
This can be especially useful if other developers are developing addons/plugins/integrations to your proprietary product.
You can then just provide your reference assembly to them. They will not need access to your product.

### Usage
#### CLI
`dotnet tool install ReferenceAssemblyGenerator.CLI <-g|--global>`
`dotnet generatereference -- [--keep-non-public] [--force] [--use-ret] [--output <outputfile>] <assemblyPath>`

#### NuGet
First install the CLI tool globally: `dotnet tool install ReferenceAssemblyGenerator.CLI --global`.

After that install `ReferenceAssemblyGenerator.Targets` to your project and set `<GenerateReference>` to true in your .csproj.
You can also set `<ReferenceKeepNonPublic>`, `<ReferenceUseRet>` and `<ReferenceOutputPath>`.

By default, `<ReferenceOutputPath>` will be equal to the output file. 

### License
[MIT](https://github.com/ImperialPlugins/ReferenceAssemblyGenerator/blob/master/LICENSE)


### ReferenceAssemblyGenerator vs. Rosyln (/refout and /refonly)
The C# and Visual Basic .NET compiler (Rosyln) contains the similar options [/refout and /refonly](https://github.com/dotnet/roslyn/blob/master/docs/features/refout.md).

* Unlike Rosyln, ReferenceAssemblyGenerator removes non-public types and members too. It also removes all non-public attributes.
* Rosyln only supports `throw null` as dummy instructions while ReferenceAssemblyGenerator also supports just `ret` instead.
* Rosyln can only generate reference assemblies if you have the full source code. ReferenceAssemblyGenerator does not need source code, only the .dll or .exe.
