# Samples

The pipeline samples ship in a separate package, `com.unity.pipeline.samples`, so that
`com.unity.pipeline` contains only the code that runs in your project.

## Getting the samples

Add `com.unity.pipeline.samples` through the Package Manager (**Add package by name**). It
depends on `com.unity.pipeline`, so the pipeline package resolves automatically if you do not
already have it.

Adding the package does not copy anything into your project. Select it in the Package Manager,
open the **Samples** tab, and import the sample you want — it is copied into
`Assets/Samples/` and is yours to edit.

## What is available

| Sample | What it shows |
|--------|---------------|
| Code Reload Examples | Two scenes for [`[CodeReload]`](code-reload.md) method editing. `CodeReload_InPlace` is a minimal component reloaded in place. `CodeReload_Minics` is a self-playing Pong game reloaded through the minics interpreter, which works even in il2cpp players in exchange for supporting only a subset of C#. |

## Why a separate package

Samples carry scenes, assets and sometimes heavy render-pipeline dependencies that most
projects using the pipeline API do not want. Keeping them in their own package means the
pipeline package stays small, and a sample's dependencies are pulled in only by the people
who ask for that sample.
