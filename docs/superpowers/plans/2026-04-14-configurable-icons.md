# Configurable Icons from Docker Share Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serve shift icons from `config/icons/` on the Docker share instead of bundling them in the repository.

**Architecture:** Add a second `UseStaticFiles` middleware in `Server/Program.cs` with a `PhysicalFileProvider` pointing to `config/icons/`, served at the `/icons` URL path. Remove all PNG icon files and their `.csproj` entries from the repo. No frontend changes needed — `<img src="icons/filename.png">` continues to work unchanged.

**Tech Stack:** ASP.NET Core static files middleware, `Microsoft.Extensions.FileProviders.PhysicalFileProvider` (already in the ASP.NET Core SDK, no new package required)

---

## Files Changed

| Action | File | What changes |
|--------|------|--------------|
| Modify | `Server/Program.cs` | Add second `UseStaticFiles` for `config/icons/` |
| Modify | `Client/ShiftScheduler.Client.csproj` | Remove all `<Content Update="wwwroot\icons\*">` and `<Content Update="wwwroot\break-icon.png">` entries |
| Delete | `Client/wwwroot/icons/` | Remove entire directory and all PNGs |
| Delete | `Client/wwwroot/break-icon.png` (if present) | Remove stray root-level copy |

---

### Task 1: Add static file middleware for `config/icons/`

**Files:**
- Modify: `Server/Program.cs:125` (after `app.UseStaticFiles();`)

- [ ] **Step 1: Add the icons static file middleware**

In `Server/Program.cs`, replace:

```csharp
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
```

with:

```csharp
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var iconsPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "icons");
Directory.CreateDirectory(iconsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(iconsPath),
    RequestPath = "/icons"
});
```

- [ ] **Step 2: Verify the app builds**

```bash
dotnet build
```

Expected: Build succeeded with 0 errors (pre-existing warnings about QuestPDF and null reference are OK).

- [ ] **Step 3: Commit**

```bash
git checkout -b feature/configurable-icons
git add Server/Program.cs
git commit -m "feat: serve icons from config/icons/ Docker share via static file middleware"
```

---

### Task 2: Remove icon files and clean up the .csproj

**Files:**
- Modify: `Client/ShiftScheduler.Client.csproj` — remove `<ItemGroup>` with icon `<Content>` entries
- Delete: `Client/wwwroot/icons/` directory
- Delete: `Client/wwwroot/break-icon.png` (stray root-level copy listed in .csproj)

- [ ] **Step 1: Remove the entire icons ItemGroup from the .csproj**

In `Client/ShiftScheduler.Client.csproj`, remove the entire `<ItemGroup>` block that contains the icon entries (lines 14–48 in the current file), so the file becomes:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="9.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="9.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="9.0.8" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\ShiftScheduler.Shared.csproj" />
    <ProjectReference Include="..\Services\ShiftScheduler.Services.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Delete the icon files from the repo**

```bash
rm -rf Client/wwwroot/icons
rm -f Client/wwwroot/break-icon.png
```

- [ ] **Step 3: Verify the app still builds**

```bash
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Client/ShiftScheduler.Client.csproj
git add -u Client/wwwroot/
git commit -m "chore: remove bundled icon files from repo — icons now served from Docker share"
```

---

### Task 3: Smoke test the icons endpoint

This task verifies the middleware works end-to-end before the branch is merged.

- [ ] **Step 1: Copy a test icon to the config/icons directory**

```bash
mkdir -p config/icons
# Copy any PNG you have on hand, e.g. the app favicon
cp Client/wwwroot/favicon.png config/icons/test.png
```

- [ ] **Step 2: Start the server**

```bash
cd Server && dotnet run
```

Expected: App starts at `http://localhost:5000`.

- [ ] **Step 3: Verify the icon is served**

```bash
curl -o /dev/null -w "%{http_code}" http://localhost:5000/icons/test.png
```

Expected output: `200`

- [ ] **Step 4: Verify a missing icon returns 404**

```bash
curl -o /dev/null -w "%{http_code}" http://localhost:5000/icons/nonexistent.png
```

Expected output: `404`

- [ ] **Step 5: Stop the server and clean up the test file**

```bash
rm config/icons/test.png
```

- [ ] **Step 6: Merge to main**

```bash
cd ..
git checkout main
git merge --squash feature/configurable-icons
git commit -m "feat: serve icons from config/icons/ Docker share"
git branch -d feature/configurable-icons
```
