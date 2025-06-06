# Azure DevOps Test Case Linker

This tool automates the creation and association of MSTest `[TestMethod]` unit tests with Azure DevOps Test Cases using the REST API.

## 🚀 What It Does

- Reflects over a compiled test DLL
- Detects all public `[TestMethod]`s
- Skips private methods
- Skips methods already linked to test cases
- Creates a new test case for each method not already linked
- Associates each method with its test case using PATCH updates

## ✅ Requirements

- .NET 6.0 SDK or higher
- MSTest-based test project (compiled DLL)
- A valid Azure DevOps Personal Access Token (PAT) with:
  - `Work Items (Read & Write)`
  - `Test Management (Read & Write)`

## 🛠 Configuration

Update the following values in `Program.cs`:

```csharp
private static string organization = "<your-organization>";
private static string project = "<your-project>";
private static string pat = "<your-PAT>";
private static string testDllPath = @"C:\Path\To\Your\TestAssembly.dll";
private static string testAssemblyName = "<YourTestAssemblyName>";
```

## 💡 Example

If you have a test method like:

```csharp
[TestMethod]
public void MyLoginTest() { ... }
```

This tool will:
- Create a test case titled `MyLoginTest`
- Link the automation to:  
  `Namespace.ClassName.MyLoginTest`

## 🧪 Limitations

- Only public instance methods are processed
- Currently supports MSTest `[TestMethod]` attributes only
- It skips methods already linked to test cases

## 📦 How To Run

```bash
dotnet build
dotnet run
```

## 📜 License

MIT

---

Contributions welcome!
