# ReferenceAssemblyGenerator
A dotnet tool to generate reference assemblies.

### What is a reference assembly?
Reference assemblies are assemblies which only contain metadata but no executable IL code.

### Why would you need this?
Sometimes you do not want to ship executable code but only metadata for developers to interact with your dll.
This can be especially useful if other developers are developing addons/plugins/integrations to your proprietary product.
You can then just provide your reference assembly to them. They will not need access to your product.

### Usage
`dotnet generatereference [--keep-non-public] [-o <outputfile>] <assemblyPath>`

### License
[MIT](https://github.com/ImperialPlugins/ReferenceAssemblyGenerator/blob/master/LICENSE)
