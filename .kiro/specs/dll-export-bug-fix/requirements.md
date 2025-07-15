# Requirements Document

## Introduction

AssetRipper项目中存在一个bug：当用户选择多个dll进行导出cs代码时，导出功能不生效。通过代码分析发现，问题出现在导出流程中对dll白名单的处理逻辑上。需要修复这个bug，确保多选dll导出cs代码功能正常工作。

## Requirements

### Requirement 1

**User Story:** 作为AssetRipper用户，我希望能够选择多个dll文件并成功导出对应的cs代码，以便我能够获取特定程序集的源代码。

#### Acceptance Criteria

1. WHEN 用户在Web界面选择多个dll文件进行导出 THEN 系统应该正确处理所有选中的dll
2. WHEN 用户提交导出请求 THEN 系统应该只导出选中dll对应的MonoScript资源
3. WHEN 导出过程执行 THEN 系统应该为每个选中的dll生成对应的cs文件
4. WHEN 导出完成 THEN 用户应该能够在输出目录中找到所有选中dll的cs代码文件

### Requirement 2

**User Story:** 作为开发者，我希望dll白名单过滤逻辑能够正确工作，以便系统能够准确识别和处理用户选择的dll。

#### Acceptance Criteria

1. WHEN dll白名单不为空 THEN 系统应该只处理白名单中包含的dll
2. WHEN 检查dll是否在白名单中 THEN 系统应该使用正确的dll名称格式进行匹配
3. WHEN 创建ScriptExportCollection THEN 系统应该正确传递dll白名单参数
4. WHEN 过滤MonoScript资源 THEN 系统应该基于dll白名单进行准确过滤

### Requirement 3

**User Story:** 作为系统管理员，我希望导出过程中的错误能够被正确记录和处理，以便我能够诊断和解决导出问题。

#### Acceptance Criteria

1. WHEN dll白名单过滤失败 THEN 系统应该记录详细的错误信息
2. WHEN 找不到匹配的dll THEN 系统应该提供清晰的警告信息
3. WHEN 导出过程中出现异常 THEN 系统应该优雅地处理错误并继续处理其他dll
4. WHEN 导出完成 THEN 系统应该提供导出结果的摘要信息