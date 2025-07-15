using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;

namespace AssetRipper.Export.UnityProjects;

public sealed partial class ProjectExporter
{
	public event Action? EventExportPreparationStarted;
	public event Action? EventExportPreparationFinished;
	public event Action? EventExportStarted;
	public event Action<int, int>? EventExportProgressUpdated;
	public event Action? EventExportFinished;

	private readonly ObjectHandlerStack<IAssetExporter> assetExporterStack = new();
	private readonly IAssemblyManager _assemblyManager;

	//Exporters
	private DummyAssetExporter DummyExporter { get; } = new DummyAssetExporter();

	/// <summary>Adds an exporter to the stack of exporters for this asset type.</summary>
	/// <typeparam name="T">The c sharp type of this asset type. Any inherited types also get this exporter.</typeparam>
	/// <param name="exporter">The new exporter. If it doesn't work, the next one in the stack is used.</param>
	/// <param name="allowInheritance">Should types that inherit from this type also use the exporter?</param>
	public void OverrideExporter<T>(IAssetExporter exporter, bool allowInheritance = true)
	{
		assetExporterStack.OverrideHandler(typeof(T), exporter, allowInheritance);
	}

	/// <summary>Adds an exporter to the stack of exporters for this asset type.</summary>
	/// <param name="type">The c sharp type of this asset type. Any inherited types also get this exporter.</param>
	/// <param name="exporter">The new exporter. If it doesn't work, the next one in the stack is used.</param>
	/// <param name="allowInheritance">Should types that inherit from this type also use the exporter?</param>
	public void OverrideExporter(Type type, IAssetExporter exporter, bool allowInheritance)
	{
		assetExporterStack.OverrideHandler(type, exporter, allowInheritance);
	}

	/// <summary>
	/// Use the <see cref="DummyExporter"/> for the specified class type.
	/// </summary>
	/// <typeparam name="T">The base type for assets of that <paramref name="classType"/>.</typeparam>
	/// <param name="classType">The class id of assets we are using the <see cref="DummyExporter"/> for.</param>
	/// <param name="isEmptyCollection">
	/// True: an exception will be thrown if the asset is referenced by another asset.<br/>
	/// False: any references to this asset will be replaced with a missing reference.
	/// </param>
	/// <param name="isMetaType"><see cref="AssetType.Meta"/> or <see cref="AssetType.Serialized"/>?</param>
	private void OverrideDummyExporter<T>(ClassIDType classType, bool isEmptyCollection, bool isMetaType)
	{
		DummyExporter.SetUpClassType(classType, isEmptyCollection, isMetaType);
		OverrideExporter<T>(DummyExporter, true);
	}

	public AssetType ToExportType(Type type)
	{
		foreach (IAssetExporter exporter in assetExporterStack.GetHandlerStack(type))
		{
			if (exporter.ToUnknownExportType(type, out AssetType assetType))
			{
				return assetType;
			}
		}
		throw new NotSupportedException($"There is no exporter that know {nameof(AssetType)} for unknown asset '{type}'");
	}

	private IExportCollection CreateCollection(IUnityObjectBase asset, IEnumerable<string>? dllNames = null)
	{
		foreach (IAssetExporter exporter in assetExporterStack.GetHandlerStack(asset.GetType()))
		{
			if (asset is AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript && exporter is Scripts.ScriptExporter scriptExporter)
			{
				if (scriptExporter.TryCreateCollection(asset, out IExportCollection? collection, dllNames))
				{
					return collection;
				}
			}
			else
			{
				if (exporter.TryCreateCollection(asset, out IExportCollection? collection))
				{
					return collection;
				}
			}
		}
		throw new Exception($"There is no exporter that can handle '{asset}'");
	}

	public void Export(GameBundle fileCollection, CoreConfiguration options, FileSystem fileSystem, IEnumerable<string>? dllNames)
	{
		EventExportPreparationStarted?.Invoke();
		List<IExportCollection> collections = CreateCollections(fileCollection, dllNames);
		EventExportPreparationFinished?.Invoke();

		EventExportStarted?.Invoke();
		ProjectAssetContainer container = new ProjectAssetContainer(this, options, fileCollection.FetchAssets(), collections);
		int exportableCount = collections.Count(c => c.Exportable);
		int currentExportable = 0;

		for (int i = 0; i < collections.Count; i++)
		{
			IExportCollection collection = collections[i];
			container.CurrentCollection = collection;
			if (collection.Exportable)
			{
				currentExportable++;
				Logger.Info(LogCategory.ExportProgress, $"({currentExportable}/{exportableCount}) Exporting '{collection.Name}'");
				bool exportedSuccessfully = collection.Export(container, options.ProjectRootPath, fileSystem);
				if (!exportedSuccessfully)
				{
					Logger.Warning(LogCategory.ExportProgress, $"Failed to export '{collection.Name}' ({collection.GetType().Name})");
				}
			}
			EventExportProgressUpdated?.Invoke(i, collections.Count);
		}
		EventExportFinished?.Invoke();
	}

	private List<IExportCollection> CreateCollections(GameBundle fileCollection, IEnumerable<string>? dllNames)
	{
		List<IExportCollection> collections = new();
		HashSet<IUnityObjectBase> queued = new();

		// 统一dllNames为List<string>，便于多次查找
		List<string>? dllList = dllNames?.ToList();

		foreach (IUnityObjectBase asset in fileCollection.FetchAssets())
		{
			if (!queued.Contains(asset))
			{
				// 只收集属于白名单dll的脚本集合
				if (dllList != null && asset is AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript monoScript)
				{
					string asmName = monoScript.GetAssemblyNameFixed();
					string dllName = asmName + ".dll";
					if (!dllList.Contains(dllName))
					{
						continue;
					}
				}
				IExportCollection collection = CreateCollection(asset, dllList);
				foreach (IUnityObjectBase element in collection.Assets)
				{
					queued.Add(element);
				}
				collections.Add(collection);
			}
		}

		// 补充：确保所有白名单DLL都能被收集并反编译导出（即使没有MonoScript）
		if (dllList != null && dllList.Count > 0)
		{
			// 获取所有已收集的脚本对应的DLL名
			var collectedDlls = new HashSet<string>(
				collections
					.OfType<AssetRipper.Export.UnityProjects.Scripts.ScriptExportCollection>()
					.SelectMany(c => c.Assets)
					.OfType<AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript>()
					.Select(s => s.GetAssemblyNameFixed() + ".dll"),
				StringComparer.OrdinalIgnoreCase);

			// 获取所有可用的Assembly
			var allAssemblies = _assemblyManager.GetAssemblies().ToList();
			foreach (var dll in dllList)
			{
				if (!collectedDlls.Contains(dll))
				{
					var asmName = dll.Substring(0, dll.Length - 4); // 去掉.dll
					var assembly = allAssemblies.FirstOrDefault(a => string.Equals(a.Name, asmName, StringComparison.OrdinalIgnoreCase));
					if (assembly != null)
					{
						// 构造一个空的MonoScript集合，强制创建ScriptExportCollection
						var scriptExporter = assetExporterStack.GetHandlerStack(typeof(AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript))
							.OfType<AssetRipper.Export.UnityProjects.Scripts.ScriptExporter>().FirstOrDefault();
						if (scriptExporter != null)
						{
							if (scriptExporter.TryCreateCollection(null, out IExportCollection? collection, dllList))
							{
								collections.Add(collection);
							}
						}
					}
				}
			}
		}

		// 补充：确保所有ScriptExporter都设置白名单，强制Decompile
		if (dllList != null && dllList.Count > 0)
		{
			foreach (var exporter in assetExporterStack.GetHandlerStack(typeof(AssetRipper.SourceGenerated.Classes.ClassID_115.IMonoScript)))
			{
				if (exporter is AssetRipper.Export.UnityProjects.Scripts.ScriptExporter scriptExporter)
				{
					scriptExporter.SetForceDecompileDlls(dllList);
				}
			}
		}

		return collections;
	}
}
