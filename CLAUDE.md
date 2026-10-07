# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

This is a freshly scaffolded ASP.NET Core MVC app (the stock `dotnet new mvc` template) targeting **.NET 10** (`net10.0`, SDK 10.0.x). The solution uses the new XML solution format (`FBISDNETCore.slnx`) and contains a single project, `FBISDNETCore.MVC`. There is no test project, no database/EF Core, and no authentication yet (`UseAuthorization()` is in the pipeline but nothing is configured).

## Commands

Run from the repository root (where `FBISDNETCore.slnx` lives), not from the `FBISDNETCore.MVC/` project folder:

```bash
dotnet build FBISDNETCore.slnx
dotnet run --project FBISDNETCore.MVC                         # http profile: http://localhost:5024
dotnet run --project FBISDNETCore.MVC --launch-profile https  # https://localhost:7272
dotnet watch --project FBISDNETCore.MVC                       # hot reload during development
```

No tests exist yet. If a test project is added, register it in `FBISDNETCore.slnx` and run with `dotnet test FBISDNETCore.slnx` (single test: `dotnet test --filter "FullyQualifiedName~ClassName.MethodName"`).

## Architecture

- **Startup**: `Program.cs` uses the classic `Program.Main` style (not top-level statements). Services are registered on `builder.Services`; the pipeline is HTTPS redirection → routing → authorization → static assets → conventional route `{controller=Home}/{action=Index}/{id?}`. Exception handler (`/Home/Error`) and HSTS apply only outside Development.
- **Static assets**: served via `MapStaticAssets()` / `.WithStaticAssets()` (build-time fingerprinted assets), not `UseStaticFiles()`. Client libraries (Bootstrap, jQuery, jquery-validation) are vendored under `wwwroot/lib` — there is no npm/LibMan build step.
- **Views**: Razor views under `Views/{Controller}/{Action}.cshtml`, shared layout in `Views/Shared/_Layout.cshtml`. CSS isolation is enabled (`_Layout.cshtml.css` compiles into `FBISDNETCore.MVC.styles.css`, referenced from the layout).
- **Project settings**: nullable reference types and implicit usings are enabled; namespaces use the block-scoped `namespace FBISDNETCore.MVC.X { ... }` form as in the template.
