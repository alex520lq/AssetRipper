using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using AssetRipper.Import.Logging;

namespace AssetRipper.GUI.Web.Pages;

public static class Commands
{
	private const string RootPath = "/";
	private const string CommandsPath = "/Commands";

	/// <summary>
	/// For documentation purposes
	/// </summary>
	/// <param name="Path">The file system path.</param>
	internal record PathFormData(string Path);

	internal static RouteHandlerBuilder AcceptsFormDataContainingPath(this RouteHandlerBuilder builder)
	{
		return builder.Accepts<PathFormData>("application/x-www-form-urlencoded");
	}

	private static bool TryGetCreateSubfolder(IFormCollection form)
	{
		if (form.TryGetValue("CreateSubfolder", out StringValues values))
		{
			return values == "true";
		}

		return false;
	}

	public readonly struct LoadFile : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string[]? paths;
			if (form.TryGetValue("Path", out StringValues values))
			{
				paths = values;
			}
			else if (Dialogs.Supported)
			{
				paths = Dialogs.OpenFiles.GetUserInput();
			}
			else
			{
				return CommandsPath;
			}

			if (paths is { Length: > 0 })
			{
				GameFileLoader.LoadAndProcess(paths);
			}
			return null;
		}
	}

	public readonly struct LoadFolder : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string[]? paths;
			if (form.TryGetValue("Path", out StringValues values))
			{
				paths = values;
			}
			else if (Dialogs.Supported)
			{
				paths = Dialogs.OpenFolders.GetUserInput();
			}
			else
			{
				return CommandsPath;
			}

			if (paths is { Length: > 0 })
			{
				GameFileLoader.LoadAndProcess(paths);
			}
			return null;
		}
	}

	public readonly struct ExportUnityProject : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string? path;
			if (form.TryGetValue("Path", out StringValues values))
			{
				path = values;
			}
			else
			{
				return CommandsPath;
			}

			// 验证和解析 dlls 参数
			IEnumerable<string>? dllNames = null;
			if (form.TryGetValue("Dlls", out StringValues dllValues))
			{
				// 过滤空值并验证格式
				var validDlls = dllValues
					.Where(dll => !string.IsNullOrWhiteSpace(dll))
					.Select(dll => dll.Trim())
					.Where(dll => dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
					.ToList();

				if (validDlls.Count > 0)
				{
					dllNames = validDlls;
					Logger.Info(LogCategory.Export, $"Selected {validDlls.Count} DLLs for export: {string.Join(", ", validDlls)}");
				}
				else if (dllValues.Count > 0)
				{
					// 有dll参数但都无效
					Logger.Warning(LogCategory.Export, $"Invalid DLL names provided: {string.Join(", ", dllValues)}");
				}
			}

			if (!string.IsNullOrEmpty(path))
			{
				bool createSubfolder = TryGetCreateSubfolder(form);
				path = MaybeAppendTimestampedSubfolder(path, createSubfolder);
				
				try
				{
					GameFileLoader.ExportUnityProject(path, dllNames);
				}
				catch (Exception ex)
				{
					Logger.Error(LogCategory.Export, $"Failed to export Unity project: {ex.Message}");
					throw;
				}
			}
			return null;
		}
	}

	public readonly struct ExportPrimaryContent : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string? path;
			if (form.TryGetValue("Path", out StringValues values))
			{
				path = values;
			}
			else
			{
				return CommandsPath;
			}

			if (!string.IsNullOrEmpty(path))
			{
				bool createSubfolder = TryGetCreateSubfolder(form);
				path = MaybeAppendTimestampedSubfolder(path, createSubfolder);
				GameFileLoader.ExportPrimaryContent(path);
			}
			return null;
		}
	}

	public readonly struct ExportSelectedDlls : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();
			string? path = form["Path"];
			var dlls = form["Dlls"];
			if (!string.IsNullOrEmpty(path) && dlls.Count > 0)
			{
				GameFileLoader.ExportSelectedDlls(dlls, path);
			}
			return null;
		}
	}

	private static string MaybeAppendTimestampedSubfolder(string path, bool append)
	{
		if (append)
		{
			string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
			string subfolder = $"AssetRipper_export_{timestamp}";
			return Path.Combine(path, subfolder);
		}

		return path;
	}

	public readonly struct Reset : ICommand
	{
		static Task<string?> ICommand.Execute(HttpRequest request)
		{
			GameFileLoader.Reset();
			return Task.FromResult<string?>(null);
		}
	}

	public static async Task HandleCommand<T>(HttpContext context) where T : ICommand
	{
		string? redirectionTarget = await T.Execute(context.Request);
		context.Response.Redirect(redirectionTarget ?? RootPath);
	}
}
