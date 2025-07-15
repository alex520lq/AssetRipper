using AsmResolver;
using AssetRipper.GUI.Web.Paths;

namespace AssetRipper.GUI.Web.Pages;

public sealed class CommandsPage : VuePage
{
	public static CommandsPage Instance { get; } = new();

	public override string? GetTitle() => Localization.Commands;

	public override void WriteInnerContent(TextWriter writer)
	{
		if (!GameFileLoader.IsLoaded)
		{
			using (new P(writer).End())
			{
				using (new Form(writer).WithAction("/LoadFolder").WithMethod("post").End())
				{
					new Input(writer).WithClass("form-control").WithType("text").WithName("Path")
						.WithCustomAttribute("v-model", "load_path")
						.WithCustomAttribute("@input", "handleLoadPathChange").Close();
					new Input(writer).WithCustomAttribute("v-if", "load_path_exists").WithType("submit")
						.WithClass("btn btn-primary").WithValue(Localization.MenuLoad).Close();
					new Button(writer).WithCustomAttribute("v-else").WithClass("btn btn-primary")
						.WithCustomAttribute("disabled").Close(Localization.MenuLoad);
				}

				if (Dialogs.Supported)
				{
					new Button(writer).WithCustomAttribute("@click", "handleSelectLoadFile")
						.WithClass("btn btn-success").Close(Localization.SelectFile);
					new Button(writer).WithCustomAttribute("@click", "handleSelectLoadFolder")
						.WithClass("btn btn-success").Close(Localization.SelectFolder);
				}
			}
		}
		else
		{
			using (new P(writer).End())
			{
				WriteLink(writer, "/Reset", Localization.MenuFileReset, "btn btn-danger");
			}

			using (new P(writer).End())
			{
				using (new Form(writer).End())
				{
					new Input(writer).WithClass("form-control").WithType("text").WithName("Path")
						.WithCustomAttribute("v-model", "export_path")
						.WithCustomAttribute("@input", "handleExportPathChange").Close();
				}

				// DLL多选界面渲染
				if (GameFileLoader.IsLoaded)
				{
					var assemblies = GameFileLoader.AssemblyManager.GetAssemblies().ToList();
					if (assemblies.Count > 0)
					{
						// 排序并格式化DLL名称
						var sortedAssemblies = assemblies
							.Select(a => (a.Name?.ToString() ?? "Unknown") + ".dll")
							.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
							.ToList();
						
						using (new Div(writer).WithClass("mb-3").End())
						{
							new Strong(writer).Close("选择要导出的DLL：");
							
							// 添加全选/清空按钮
							using (new Div(writer).WithClass("mb-2").End())
							{
								new Button(writer)
									.WithType("button")
									.WithClass("btn btn-sm btn-outline-primary me-2")
									.WithCustomAttribute("@click", "selectAllDlls")
									.Close("全选");
								new Button(writer)
									.WithType("button")
									.WithClass("btn btn-sm btn-outline-secondary")
									.WithCustomAttribute("@click", "clearAllDlls")
									.Close("清空");
							}
							
							// DLL选择容器，添加滚动和样式
							using (new Div(writer).WithClass("dll-selection-container")
								       .WithStyle("max-height: 300px; overflow-y: auto; border: 1px solid #dee2e6; border-radius: 0.375rem; padding: 0.75rem; background-color: #f8f9fa;")
								       .End())
							{
								foreach (var dllName in sortedAssemblies)
								{
									// 生成安全的checkbox ID
									string checkboxId = $"dll_{dllName.Replace(".", "_").Replace("-", "_").Replace(" ", "_")}";
									using (new Div(writer).WithClass("form-check").End())
									{
										new Input(writer)
											.WithType("checkbox")
											.WithClass("form-check-input")
											.WithId(checkboxId)
											.WithCustomAttribute("v-model", "selected_dlls")
											.WithCustomAttribute(":value", $"'{dllName}'")
											.WithCustomAttribute("@change", "handleDllSelectionChange")
											.Close();
										new Label(writer)
											.WithClass("form-check-label")
											.WithCustomAttribute("for", checkboxId)
											.Close(dllName);
									}
								}
							}
							
							// 显示选中的DLL数量
							using (new Small(writer).WithClass("text-muted").End())
							{
								writer.Write("已选择: {{ selected_dlls.length }} 个DLL");
							}
						}
					}
				}

				using (new Div(writer).WithClass("form-check mb-2").End())
				{
					new Input(writer)
						.WithType("checkbox")
						.WithClass("form-check-input")
						.WithCustomAttribute("v-model", "create_subfolder")
						.WithCustomAttribute("@input", "handleExportPathChange")
						.WithId("createSubfolder")
						.Close();
					new Label(writer)
						.WithClass("form-check-label")
						.WithCustomAttribute("for", "createSubfolder")
						.Close(Localization.CreateSubfolder);
				}

				using (new Form(writer).WithAction("/Export/UnityProject").WithMethod("post").WithCustomAttribute("@submit", "handleExportFormSubmit").End())
				{
					new Input(writer).WithType("hidden").WithName("Path").WithCustomAttribute("v-model", "export_path")
						.Close();
					new Input(writer).WithType("hidden").WithName("CreateSubfolder")
						.WithCustomAttribute("v-model", "create_subfolder").Close();
					
					// 使用template和v-for正确处理DLL数组提交
					// 确保每个选中的DLL都作为单独的隐藏字段提交
					writer.Write("<template v-for=\"dll in selected_dlls\" :key=\"dll\">");
					new Input(writer).WithType("hidden").WithName("Dlls").WithCustomAttribute(":value", "dll").Close();
					writer.Write("</template>");

					new Button(writer)
						.WithCustomAttribute("v-if", "export_path === '' || export_path !== export_path.trim()")
						.WithClass("btn btn-primary").WithCustomAttribute("disabled")
						.Close(Localization.ExportUnityProject);
					new Input(writer).WithCustomAttribute("v-else-if", "export_path_has_files").WithType("submit")
						.WithClass("btn btn-danger").WithValue(Localization.ExportUnityProject).Close();
					new Input(writer).WithCustomAttribute("v-else").WithType("submit").WithClass("btn btn-primary")
						.WithValue(Localization.ExportUnityProject).Close();
				}

				using (new Form(writer).WithAction("/Export/PrimaryContent").WithMethod("post").End())
				{
					new Input(writer).WithType("hidden").WithName("Path").WithCustomAttribute("v-model", "export_path")
						.Close();
					new Input(writer).WithType("hidden").WithName("CreateSubfolder")
						.WithCustomAttribute("v-model", "create_subfolder").Close();

					new Button(writer)
						.WithCustomAttribute("v-if", "export_path === '' || export_path !== export_path.trim()")
						.WithClass("btn btn-primary").WithCustomAttribute("disabled")
						.Close(Localization.ExportPrimaryContent);
					new Input(writer).WithCustomAttribute("v-else-if", "export_path_has_files").WithType("submit")
						.WithClass("btn btn-danger").WithValue(Localization.ExportPrimaryContent).Close();
					new Input(writer).WithCustomAttribute("v-else").WithType("submit").WithClass("btn btn-primary")
						.WithValue(Localization.ExportPrimaryContent).Close();
				}

				if (Dialogs.Supported)
				{
					new Button(writer).WithCustomAttribute("@click", "handleSelectExportFolder")
						.WithClass("btn btn-success").Close(Localization.SelectFolder);
				}

				using (new Div(writer).WithCustomAttribute("v-if", "export_path_has_files").End())
				{
					new P(writer).Close(Localization.WarningThisDirectoryIsNotEmptyAllContentWillBeDeleted);
				}
			}
		}
	}

	protected override void WriteScriptReferences(TextWriter writer)
	{
		base.WriteScriptReferences(writer);
		new Script(writer).WithSrc("/js/commands_page.js").Close();
	}

	private static void WriteLink(TextWriter writer, string url, string name, string? @class = null)
	{
		using (new Form(writer).WithAction(url).WithMethod("post").End())
		{
			new Input(writer).WithType("submit").WithClass(@class).WithValue(name.ToHtml()).Close();
		}
	}
}
