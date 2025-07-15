const { createApp } = Vue

const app = createApp({
	data() {
		return {
			load_path: '',
			load_path_exists: false,
			export_path: '',
			export_path_has_files: false,
			create_subfolder: false,
			selected_dlls: [], // 显式声明，确保多选DLL与其它表单项无关
			available_dlls: [] // 存储所有可用的DLL列表，用于全选功能
		}
	},
	methods: {
		async handleLoadPathChange() {
			// Add a debounce mechanism to avoid too many requests in a short time
			if (this.debouncedInput) {
				clearTimeout(this.debouncedInput);
			}

			this.debouncedInput = setTimeout(async () => {
				try {
					this.load_path_exists = await this.fetchDirectoryExists(this.load_path) || await this.fetchFileExists(this.load_path);
				} catch (error) {
					console.error('Error fetching data:', error);
				}
			}, 300); // Adjust the debounce time as needed (300 milliseconds in this example)
		},
		async handleExportPathChange() {
			// Add a debounce mechanism to avoid too many requests in a short time
			if (this.debouncedInput) {
				clearTimeout(this.debouncedInput);
			}

			this.debouncedInput = setTimeout(async () => {
				try {
					if (this.create_subfolder) {
						this.export_path_has_files = false;
					} else {
						this.export_path_has_files = await this.fetchDirectoryExists(this.export_path) && !(await this.fetchDirectoryEmpty(this.export_path));
					}
				} catch (error) {
					console.error('Error fetching data:', error);
				}
			}, 300); // Adjust the debounce time as needed (300 milliseconds in this example)
		},
		async handleSelectLoadFile() {
			// Add a debounce mechanism to avoid too many requests in a short time
			if (this.debouncedInput) {
				clearTimeout(this.debouncedInput);
			}

			this.debouncedInput = setTimeout(async () => {
				try {
					const response = await fetch(`/Dialogs/OpenFile`);
					this.load_path = await response.json();
				} catch (error) {
					console.error('Error fetching data:', error);
				}
				await this.handleLoadPathChange();
			}, 300); // Adjust the debounce time as needed (300 milliseconds in this example)
		},
		async handleSelectLoadFolder() {
			// Add a debounce mechanism to avoid too many requests in a short time
			if (this.debouncedInput) {
				clearTimeout(this.debouncedInput);
			}

			this.debouncedInput = setTimeout(async () => {
				try {
					const response = await fetch(`/Dialogs/OpenFolder`);
					this.load_path = await response.json();
				} catch (error) {
					console.error('Error fetching data:', error);
				}
				await this.handleLoadPathChange();
			}, 300); // Adjust the debounce time as needed (300 milliseconds in this example)
		},
		async handleSelectExportFolder() {
			// Add a debounce mechanism to avoid too many requests in a short time
			if (this.debouncedInput) {
				clearTimeout(this.debouncedInput);
			}

			this.debouncedInput = setTimeout(async () => {
				try {
					const response = await fetch(`/Dialogs/OpenFolder`);
					this.export_path = await response.json();
				} catch (error) {
					console.error('Error fetching data:', error);
				}
				await this.handleExportPathChange();
			}, 300); // Adjust the debounce time as needed (300 milliseconds in this example)
		},
		async fetchFileExists(path) {
			const response = await fetch(`/IO/File/Exists?Path=${encodeURIComponent(path)}`);
			return await response.json();
		},
		async fetchDirectoryExists(path) {
			const response = await fetch(`/IO/Directory/Exists?Path=${encodeURIComponent(path)}`);
			return await response.json();
		},
		async fetchDirectoryEmpty(path) {
			const response = await fetch(`/IO/Directory/Empty?Path=${encodeURIComponent(path)}`);
			return await response.json();
		},
		// DLL多选处理方法
		handleDllSelectionChange() {
			// Vue.js的v-model会自动处理数组更新，这里可以添加额外的逻辑
			console.log('Selected DLLs:', this.selected_dlls);
		},
		selectAllDlls() {
			// 使用available_dlls数组进行全选，如果为空则回退到DOM查询
			if (this.available_dlls.length > 0) {
				this.selected_dlls = [...this.available_dlls];
			} else {
				// 回退方案：通过DOM查询获取所有DLL选项的值
				const allCheckboxes = document.querySelectorAll('input[type="checkbox"][id^="dll_"]');
				const allDlls = [];
				allCheckboxes.forEach(cb => {
					// Vue.js绑定的:value属性在运行时会被设置到value属性
					let value = cb.value;
					if (!value) {
						// 如果value为空，尝试从:value属性获取并清理引号
						value = cb.getAttribute(':value');
						if (value) {
							value = value.replace(/^['"]|['"]$/g, '');
						}
					}
					if (value && value.trim()) {
						allDlls.push(value.trim());
					}
				});
				this.selected_dlls = [...allDlls];
				// 同时更新available_dlls以供后续使用
				this.available_dlls = [...allDlls];
			}
		},
		clearAllDlls() {
			this.selected_dlls = [];
		},
		// 表单提交前的验证和处理
		handleExportFormSubmit(event) {
			// 确保选中的DLL列表正确传递
			if (this.selected_dlls.length === 0) {
				console.warn('No DLLs selected for export - will export all DLLs');
			} else {
				console.log('Submitting export with selected DLLs:', this.selected_dlls);
			}
			// 让表单正常提交，不阻止默认行为
			return true;
		},
		// 初始化可用DLL列表的方法
		initializeAvailableDlls() {
			// 等待DOM渲染完成后获取所有DLL选项
			this.$nextTick(() => {
				const allCheckboxes = document.querySelectorAll('input[type="checkbox"][id^="dll_"]');
				const allDlls = [];
				allCheckboxes.forEach(cb => {
					// Vue.js绑定的:value属性在运行时会被设置到value属性
					// 首先尝试从value属性获取，然后从:value属性获取
					let value = cb.value;
					if (!value) {
						// 如果value为空，尝试从:value属性获取并清理引号
						value = cb.getAttribute(':value');
						if (value) {
							value = value.replace(/^['"]|['"]$/g, '');
						}
					}
					if (value && value.trim()) {
						allDlls.push(value.trim());
					}
				});
				this.available_dlls = [...allDlls];
				console.log('Initialized available DLLs:', this.available_dlls);
			});
		}
	},
	mounted() {
		// 组件挂载后初始化可用的DLL列表
		this.initializeAvailableDlls();
	}
})

const mountedApp = app.mount('#app')