# Migrating C# Projects to SDK-Style `.csproj`

This guide describes how to use Sharpmake's SDK-style project generation features to produce leaner `.csproj` files that delegate file discovery to the .NET SDK rather than enumerating every source file explicitly.

## Background

For .NET Core and SDK-schema projects, Sharpmake already emits SDK-style `.csproj` files (with explicit SDK imports and no `<Project Sdk="...">` shorthand). However, it still carries over old-style habits: every source file is listed explicitly in `<ItemGroup>` instead of relying on the SDK's built-in globs, and several properties are emitted that the SDK renders redundant. This guide shows how to shed those leftovers.

## Step 1: Enable SDK file discovery

Set `EnableDefaultItems = true` in your project constructor:

```cs
[Sharpmake.Generate]
public class MyLibrary : CSharpProject
{
    public MyLibrary()
    {
        EnableDefaultItems = true;
        // ... rest of constructor
    }
}
```

When this is set, Sharpmake will no longer scan and list source files for item types the SDK already covers (`Compile`, `None`, `EmbeddedResource`, `Page`). The SDK's built-in globs handle discovery instead.

## Step 2: Remove or suppress properties no longer needed

### `AppDesignerFolder`

SDK-style projects typically don't need the `Properties/` designer folder. Suppress the element by setting it to an empty string:

```cs
public MyLibrary()
{
    EnableDefaultItems = true;
    AppDesignerFolder = "";
}
```

### `IntermediateOutputPath`

Sharpmake allows omitting the per-configuration `<IntermediateOutputPath>` element by setting `conf.IntermediatePath = null` in your `ConfigureAll`. MSBuild reconstructs it from `BaseIntermediateOutputPath`:

```cs
[Configure]
public virtual void ConfigureAll(Configuration conf, Target target)
{
    conf.IntermediatePath = null;
    // ...
}
```

Set `BaseIntermediateOutputPath` at the project level if you need a custom intermediate root:

```cs
public MyLibrary()
{
    EnableDefaultItems = true;
    BaseIntermediateOutputPath = @"[project.SharpmakeCsPath]\..\tmp\obj\[project.Name]\$(TargetFramework)\";
}
```

## Step 3: Handle files that fall outside SDK discovery

### Files outside the project tree

The SDK only discovers files under the project directory. For individual out-of-tree files, use `SourceFiles.Add` with an explicit path:

```cs
SourceFiles.Add(@"[project.SharpmakeCsPath]\..\shared\Generated\Foo.Generated.cs");
```

For a whole subtree, use `AdditionalSourceRootPaths`. In both cases Sharpmake emits explicit `<Compile Include="..." Link="..."/>` entries regardless of `EnableDefaultItems`.

### Content and resource directories

`ContentPath` and `ResourcesPath` scans are extension-agnostic and still run when `EnableDefaultItems = false`. If you set `EnableDefaultItems = true`, use `AdditionalContent` or explicit `Globs` entries for any content files the SDK won't discover on its own.

### Glob overlays

For fine-grained control over SDK discovery — for example, collecting specific file patterns as `None` items — use `Globs.Add`:

```cs
public MyLibrary()
{
    EnableDefaultItems = true;

    Globs.Add(new GlobSetting
    {
        Include = @"**\*.md;**\*.png",
        Exclude = @"**\generated\**",
        ItemType = GlobItemType.None,
    });
}
```

This emits a single `<None Include="..." Exclude="..." />` entry instead of a file-by-file list. The `Exclude` pattern is also forwarded when the glob item type is `None`.

To update metadata on SDK-discovered files — for example, marking files as copy-on-build:

```cs
Globs.Add(new GlobSetting
{
    Update = @"**\*.json",
    CopyToOutputDirectory = CopyToOutputDirectory.PreserveNewest,
    ItemType = GlobItemType.None,
});
```

This emits `<None Update="**\*.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`.

### Removing files from SDK globs

To exclude specific files or patterns from the SDK's own glob discovery:

```cs
Globs.Add(new GlobSetting
{
    Remove = @"**\*.Generated.cs",
    ItemType = GlobItemType.Compile,
});
```

This emits `<Compile Remove="**\*.Generated.cs" />`. Note that the SDK's `None` glob (`**`) will then pick those files up as `None` items, keeping them visible in the project. To suppress them from the project entirely, add a matching `None` removal as well:

```cs
Globs.Add(new GlobSetting { Remove = @"**\*.Generated.cs", ItemType = GlobItemType.Compile });
Globs.Add(new GlobSetting { Remove = @"**\*.Generated.cs", ItemType = GlobItemType.None });
```

## Step 4: Fine-grained per-type control

If you need SDK discovery for some item types but not others, use the per-type `bool?` properties. Each defaults to `null` (element omitted, SDK decides). Set to `false` to explicitly suppress a type's glob:

| Property | Controls |
|---|---|
| `EnableDefaultCompileItems` | `.cs` glob |
| `EnableDefaultEmbeddedResourceItems` | `.resx` glob |
| `EnableDefaultNoneItems` | catch-all `None` glob |
| `EnableDefaultPageItems` | `.xaml` pages glob |
| `EnableDefaultApplicationDefinition` | `App.xaml` application definition |

Example — disable `None` discovery while keeping everything else:

```cs
public MyLibrary()
{
    EnableDefaultItems = true;
    EnableDefaultNoneItems = false;
}
```

## Project references

For SDK-style projects, `<ProjectReference>` entries omit the optional `<Project>` GUID by default — MSBuild resolves by path and does not need it. Legacy (non-SDK) projects continue to emit both `<Project>` and `<Name>` as before.

If your IDE requires the GUID for code-navigation (e.g. Rider reports spurious "Ambiguous reference" errors without it — see [RIDER-26499](https://youtrack.jetbrains.com/issue/RIDER-26499/Ambiguous-reference)), opt in per project:

```csharp
public MyProject()
{
    ForceProjectReferenceGuid = true;
}
```

## Summary

A minimal SDK-style project declaration looks like this:

```cs
[Sharpmake.Generate]
public class MyLibrary : CSharpProject
{
    public MyLibrary()
    {
        Name = "MyLibrary";
        SourceRootPath = @"[project.SharpmakeCsPath]\src";

        EnableDefaultItems = true;
        AppDesignerFolder = "";
        BaseIntermediateOutputPath = @"[project.SharpmakeCsPath]\..\tmp\obj\[project.Name]\$(TargetFramework)\";

        AddTargets(new Target(
            Platform.anycpu,
            DevEnv.vs2022,
            Optimization.Debug | Optimization.Release,
            DotNetFramework.net8_0));
    }

    [Configure]
    public virtual void ConfigureAll(Configuration conf, Target target)
    {
        conf.ProjectFileName = "[project.Name].[target.DevEnv]";
        conf.ProjectPath = @"[project.SharpmakeCsPath]\..\projects";
        conf.IntermediatePath = null;
        conf.Options.Add(Options.CSharp.Prefer32Bit.Unset);
    }
}
```

The resulting `.csproj` will contain only property groups and project references — no file lists.
