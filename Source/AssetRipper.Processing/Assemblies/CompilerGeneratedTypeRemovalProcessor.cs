using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssetRipper.Import.Structure.Assembly.Managers;

namespace AssetRipper.Processing.Assemblies;

/// <summary>
/// 移除编译器生成的异步状态机类型，避免在反编译输出中生成不必要的类。
/// </summary>
public sealed class CompilerGeneratedTypeRemovalProcessor : IAssetProcessor
{
	public void Process(GameData gameData) => Process(gameData.AssemblyManager);
	
	private static void Process(IAssemblyManager manager)
	{
		manager.ClearStreamCache();
		
		RemoveCompilerGeneratedStateMachineTypes(manager);
		RemoveAsyncAndIteratorStateMachineAttributes(manager);
	}
	
	/// <summary>
	/// 移除编译器生成的异步状态机类型
	/// </summary>
	private static void RemoveCompilerGeneratedStateMachineTypes(IAssemblyManager manager)
	{
		foreach (ModuleDefinition module in manager.GetAllModules())
		{
			for (int i = module.TopLevelTypes.Count - 1; i >= 0; i--)
			{
				TypeDefinition type = module.TopLevelTypes[i];
				if (IsCompilerGeneratedStateMachine(type))
				{
					module.TopLevelTypes.RemoveAt(i);
				}
				else
				{
					// 检查嵌套类型
					RemoveNestedStateMachineTypes(type);
				}
			}
		}
	}
	
	/// <summary>
	/// 移除嵌套的编译器生成状态机类型
	/// </summary>
	private static void RemoveNestedStateMachineTypes(TypeDefinition parentType)
	{
		for (int i = parentType.NestedTypes.Count - 1; i >= 0; i--)
		{
			TypeDefinition nestedType = parentType.NestedTypes[i];
			if (IsCompilerGeneratedStateMachine(nestedType))
			{
				parentType.NestedTypes.RemoveAt(i);
			}
			else
			{
				// 递归检查更深层的嵌套类型
				RemoveNestedStateMachineTypes(nestedType);
			}
		}
	}
	
	/// <summary>
	/// 判断是否为编译器生成的状态机类型
	/// </summary>
	private static bool IsCompilerGeneratedStateMachine(TypeDefinition type)
	{
		// 检查名称模式：形如 <PlayLevel>d__23 或 _003CPlayLevel_003Ed__23
		string? name = type.Name;
		if (name is null)
			return false;
		
		// 检查编码后的名称模式
		if (name.Contains("_003C") && name.Contains("_003E") && name.Contains("d__"))
			return true;
		
		// 检查原始名称模式
		if (name.StartsWith("<") && name.Contains(">d__"))
			return true;
		
		// 检查是否有CompilerGeneratedAttribute且实现了状态机接口
		if (HasCompilerGeneratedAttribute(type))
		{
			if (ImplementsStateMachineInterface(type))
				return true;
		}
		
		return false;
	}
	
	/// <summary>
	/// 检查类型是否有CompilerGeneratedAttribute
	/// </summary>
	private static bool HasCompilerGeneratedAttribute(TypeDefinition type)
	{
		return type.CustomAttributes.Any(attr => 
			attr.Constructor?.DeclaringType?.Name == "CompilerGeneratedAttribute");
	}
	
	/// <summary>
	/// 检查类型是否实现了状态机相关接口
	/// </summary>
	private static bool ImplementsStateMachineInterface(TypeDefinition type)
	{
		// 检查基类和接口
		if (type.BaseType?.Name == "ValueType" || type.BaseType?.Name == "Object")
		{
			// 检查实现的接口
			foreach (InterfaceImplementation iface in type.Interfaces)
			{
				string? interfaceName = iface.Interface?.Name;
				if (interfaceName == "IAsyncStateMachine" || 
				    interfaceName == "IEnumerator" || 
				    interfaceName == "IEnumerator`1")
				{
					return true;
				}
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// 移除方法上引用已删除状态机类型的 AsyncStateMachine 和 IteratorStateMachine 属性
	/// </summary>
	private static void RemoveAsyncAndIteratorStateMachineAttributes(IAssemblyManager manager)
	{
		foreach (TypeDefinition type in manager.GetAllTypes())
		{
			foreach (MethodDefinition method in type.Methods)
			{
				RemoveAsyncAndIteratorStateMachineAttributesFromMethod(method);
			}
		}
	}

	/// <summary>
	/// 从方法中移除 AsyncStateMachine 和 IteratorStateMachine 属性
	/// </summary>
	private static void RemoveAsyncAndIteratorStateMachineAttributesFromMethod(MethodDefinition method)
	{
		for (int i = method.CustomAttributes.Count - 1; i >= 0; i--)
		{
			CustomAttribute attribute = method.CustomAttributes[i];
			if (IsAsyncStateMachineAttribute(attribute) || IsIteratorStateMachineAttribute(attribute))
			{
				method.CustomAttributes.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// 检查是否为 IteratorStateMachine 属性
	/// </summary>
	private static bool IsIteratorStateMachineAttribute(CustomAttribute attribute)
	{
		return attribute.Constructor?.DeclaringType?.Name == "IteratorStateMachineAttribute";
	}

	/// <summary>
	/// 检查是否为 AsyncStateMachine 属性
	/// </summary>
	private static bool IsAsyncStateMachineAttribute(CustomAttribute attribute)
	{
		return attribute.Constructor?.DeclaringType?.Name == "AsyncStateMachineAttribute";
	}


}