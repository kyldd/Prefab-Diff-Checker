# Prefab Diff Checker

**A readable way to see what actually changed inside a Unity prefab.**

Unity prefabs are serialized as YAML, which is great for version control but not particularly great to read.

Prefab Diff Checker lets you select a prefab in your project and compare it against another version from Git. Instead of showing the raw file diff, it rebuilds both versions as Unity data and shows the changes side by side.

GameObjects, Components, properties and references stay recognizable, so you can spend less time digging through YAML trying to figure out what actually changed.

> Git tells you that the prefab changed. Prefab Diff Checker helps you understand what changed.

## What it does

Prefab Diff Checker compares your current prefab against the same prefab from a Git revision and presents the result using the structure you're already familiar with in Unity.

It can show:

- GameObjects that were added or removed
- Components that were added, removed or changed
- Inspector property changes with old and new values
- Objects that moved within the prefab hierarchy
- Changes to common Unity values and object references
- Only the parts of the prefab that actually changed

The comparison stays inside the Unity Editor, with filters and change navigation for larger prefabs.

## Why I made this

Prefab diffs are one of those things that technically already work with Git, but aren't particularly pleasant to review.

Even a small Inspector change can turn into something like:

```yaml
m_Color: {r: 0.84, g: 0.72, b: 0.31, a: 1}
m_Sprite: {fileID: 21300000, guid: ..., type: 3}
```

and larger prefab changes quickly become difficult to follow.

What I usually want to know is closer to:

```text
ShopPanel
    UIImage
        Sprite
            coin_icon -> gem_icon

    PriceLabel
        Text
            500 -> 750
```

Prefab Diff Checker is an attempt to make that comparison feel more like inspecting a prefab and less like reading its serialization format.

## Installation

### Package Manager

In Unity, open:

**Window > Package Manager**

Choose:

**Add package from Git URL**

and enter:

```text
https://github.com/kyldd/Prefab-Diff-Checker.git
```

### Manual installation

You can also copy:

```text
Editor/PrefabDiffChecker
```

into an Editor folder in your project:

```text
Assets/
    Editor/
        PrefabDiffChecker/
```

## Usage

Open:

**Tools > Prefab Diff Checker**

Select the prefab you want to inspect, enter the Git revision you want to compare against, and click **Compare**.

For example:

```text
origin/develop
HEAD
HEAD~1
abc1234
```

The Git version appears on the left and your current local prefab appears on the right.

That's it.

## Git stays read-only

Prefab Diff Checker uses Git as a source for the older prefab.

It does not switch branches or modify your repository.

There are no checkout, fetch, pull, push, reset, commit or other repository-changing operations in the tool. Your working tree and Git state are left alone.

The revision you want to compare against simply needs to already be available in your local repository.

For a remote-tracking branch, that will usually look like:

```text
origin/develop
```

rather than:

```text
develop
```

## Compatibility

The tool is intended to work with **Unity 2019.4 LTS and newer**.

Git must be installed and available from the command line.

If you find something that behaves differently on a particular Unity version, please open an issue.

## Current limitations

Prefab Diff Checker is still fairly new, and there are a few cases I want to improve.

Historical prefabs are temporarily loaded into the current Unity project for inspection. This means references to external assets are resolved against your current project.

So if both a prefab and one of the materials, textures or other assets it references changed between revisions, the external dependency may represent the current version rather than its historical version.

Matching also intentionally leans conservative. If the tool isn't confident that two heavily rearranged objects are the same object, it may show them as removed and added rather than making an incorrect match.

If you find a prefab that produces a strange comparison, feel free to open an issue.

## Contributing

Bug reports, ideas and pull requests are welcome.

If you're reporting a comparison problem, a small reproducible example is especially useful. Please don't include proprietary project assets or repository data.

## License

Prefab Diff Checker is free and open source under the [MIT License](LICENSE).
