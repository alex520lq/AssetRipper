# Design Document

## Overview

通过代码分析发现，AssetRipper项目中多个dll导出cs代码不生效的bug主要存在于以下几个方面：

1. **前端界面问题**：CommandsPage.cs中虽然渲染了dll多选界面，但JavaScript代码中缺少对selected_dlls的正确处理
2. **后端传参问题**：Commands.cs中ExportUnityProject命令正确接收了dlls参数，但传递给导出流程时可能存在格式不匹配
3. **导出逻辑问题**：ProjectExporter.cs中的CreateCollections方法对dll白名单的过滤逻辑存在潜在问题
4. **ScriptExportCollection问题**：dll白名单的检查逻辑可能不够健壮

## Architecture

修复方案采用分层架构：

```
前端层 (JavaScript/Vue.js)
    ↓ (HTTP POST)
Web API层 (Commands.cs)
    ↓ (方法调用)
业务逻辑层 (GameFileLoader.cs, ExportHandler.cs)
    ↓ (导出处理)
导出引擎层 (ProjectExporter.cs, ScriptExporter.cs)
    ↓ (集合创建)
脚本处理层 (ScriptExportCollection.cs)
```

## Components and Interfaces

### 1. 前端组件修复

**CommandsPage.cs**
- 确保dll多选界面正确渲染
- 修复表单提交时的dll参数传递

**commands_page.js**
- 添加selected_dlls数组的正确初始化和处理
- 确保表单提交时包含选中的dll列表

### 2. Web API层修复

**Commands.cs**
- 验证ExportUnityProject命令中dlls参数的接收和传递
- 确保参数格式正确传递给GameFileLoader

### 3. 业务逻辑层修复

**GameFileLoader.cs**
- 修复ExportUnityProject方法中dllNames参数的处理
- 确保参数正确传递给ExportHandler

**ExportHandler.cs**
- 添加带dll白名单的Export重载方法
- 确保dll白名单正确传递给ProjectExporter

### 4. 导出引擎层修复

**ProjectExporter.cs**
- 修复CreateCollections方法中的dll过滤逻辑
- 改进MonoScript资源的dll名称匹配算法
- 添加详细的日志记录

**ScriptExporter.cs**
- 确保TryCreateCollection方法正确处理dll白名单
- 验证dll白名单参数的传递

### 5. 脚本处理层修复

**ScriptExportCollection.cs**
- 修复dll白名单检查逻辑中的字符串匹配问题
- 改进错误处理和日志记录
- 确保assembly.Name与dll白名单的格式一致性

## Data Models

### DLL白名单数据流

```csharp
// 前端选中的dll列表
string[] selectedDlls = ["Assembly-CSharp.dll", "UnityEngine.dll"]

// 传递给导出流程
IEnumerable<string> dllNames = selectedDlls

// ScriptExportCollection中的处理
HashSet<string> _dllWhiteList = new HashSet<string>(dllNames)

// 匹配逻辑
string assemblyName = assembly.Name; // "Assembly-CSharp"
string dllName = assemblyName + ".dll"; // "Assembly-CSharp.dll"
bool shouldExport = _dllWhiteList.Contains(dllName);
```

### MonoScript过滤逻辑

```csharp
// 当前的过滤逻辑（存在问题）
if (dllNames != null && asset is IMonoScript monoScript)
{
    string asmName = monoScript.GetAssemblyNameFixed() + ".dll";
    if (!dllNames.Contains(asmName))
    {
        continue; // 跳过不在白名单中的脚本
    }
}

// 改进后的过滤逻辑
if (dllNames != null && asset is IMonoScript monoScript)
{
    string asmName = monoScript.GetAssemblyNameFixed();
    string dllName = asmName + ".dll";
    if (!dllNames.Contains(dllName))
    {
        Logger.Debug(LogCategory.Export, $"Skipping MonoScript from {dllName} (not in whitelist)");
        continue;
    }
    Logger.Debug(LogCategory.Export, $"Including MonoScript from {dllName}");
}
```

## Error Handling

### 1. 参数验证
- 验证dll白名单不为空
- 验证dll名称格式正确
- 验证选中的dll确实存在于AssemblyManager中

### 2. 匹配失败处理
- 当dll白名单中的dll在AssemblyManager中找不到时，记录警告
- 当没有任何dll匹配时，提供清晰的错误信息
- 提供导出摘要，显示实际处理的dll数量

### 3. 日志记录
- 在关键步骤添加Debug级别的日志
- 记录dll白名单的内容
- 记录实际匹配和跳过的dll
- 记录最终导出的脚本数量

## Testing Strategy

### 1. 单元测试
- 测试dll名称格式化逻辑
- 测试白名单匹配算法
- 测试边界条件（空白名单、不存在的dll等）

### 2. 集成测试
- 测试完整的导出流程
- 测试多选dll的端到端功能
- 测试错误场景的处理

### 3. 用户界面测试
- 验证dll多选界面的正确渲染
- 验证表单提交的参数传递
- 验证导出结果的正确性

## Performance Considerations

### 1. 内存优化
- 使用HashSet进行O(1)的dll白名单查找
- 避免重复的字符串操作

### 2. 处理优化
- 在CreateCollections阶段就过滤掉不需要的MonoScript
- 避免创建不必要的ExportCollection对象

### 3. 日志优化
- 使用适当的日志级别避免性能影响
- 在Release模式下禁用详细的Debug日志