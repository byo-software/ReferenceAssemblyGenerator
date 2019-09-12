# ReferenceAssemblyGenerator
A dotnet tool to generate reference assemblies.

## What is a reference assembly?
Reference assemblies are assemblies which only contain metadata but no executable IL code.

## Why would you need this?
Sometimes you do not want to ship executable code but only metadata so other developers can interact with your dll.

## Usage
`dotnet generatereference [--keep-non-public] [-o <outputfile>] <assemblyPath>`
