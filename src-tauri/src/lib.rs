use tauri::http::{header, Response};
use tauri::{Manager, Emitter, WindowEvent};
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::menu::{Menu, MenuItem};
use tauri_plugin_shell::process::CommandEvent;
use tauri::async_runtime::Mutex;
use serde::{Serialize, Deserialize};

struct StartupTime(std::time::Instant);

fn get_pawsdata_dir() -> std::path::PathBuf {
    let current_exe = std::env::current_exe().unwrap_or_default();
    let base_dir = current_exe.parent().unwrap_or(std::path::Path::new(""));
    
    // 1. Проверяем портативный режим (прямо рядом с EXE)
    let portable_data = base_dir.join("PawsData");
    if portable_data.exists() {
        return portable_data;
    }

    // 2. Проверяем папку binaries (специфика Tauri 2 dev режима для сайдкаров)
    let binaries_data = base_dir.join("binaries").join("PawsData");
    if binaries_data.exists() {
        return binaries_data;
    }

    // 3. Стандартный путь в AppData
    let local_app_data = std::env::var("LOCALAPPDATA").unwrap_or_else(|_| "".to_string());
    if !local_app_data.is_empty() {
        let appdata_path = std::path::Path::new(&local_app_data).join("PawsData");
        return appdata_path;
    }

    base_dir.to_path_buf()
}
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SidecarCommand {
    action: String,
    caller_id: String,
    params: std::collections::HashMap<String, serde_json::Value>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
#[allow(dead_code)]
struct SidecarResponse {
    success: bool,
    data: Option<serde_json::Value>,
    error: Option<String>,
}

#[tauri::command]
fn get_startup_telemetry(state: tauri::State<'_, StartupTime>) -> u64 {
    state.0.elapsed().as_millis() as u64
}

#[tauri::command]
fn is_autostart_launch() -> bool {
    let args: Vec<String> = std::env::args().collect();
    args.contains(&"--autostart".to_string())
}

struct PluginRegistry {
    pub paths: std::sync::Mutex<std::collections::HashMap<String, String>>,
}

#[tauri::command]
async fn register_plugin_path(
    state: tauri::State<'_, PluginRegistry>,
    plugin_id: String,
    path: String,
) -> Result<(), String> {
    let mut map = state.paths.lock().unwrap();
    println!("[Rust] Registered plugin path for {}: {}", plugin_id, path);
    map.insert(plugin_id, path);
    Ok(())
}

#[tauri::command]
async fn call_sidecar(
    handle: tauri::State<'_, SidecarHandle>,
    action: String,
    params: std::collections::HashMap<String, serde_json::Value>,
    caller_id: String,
) -> Result<SidecarResponse, String> {
    let mut child_guard = handle.child.lock().await;
    let mut rx_guard = handle.rx.lock().await;
    let (ref mut rx, ref mut buffer) = *rx_guard;

    let child = child_guard.as_mut().ok_or("Sidecar not initialized")?;

    let cmd = SidecarCommand { action: action.clone(), caller_id, params };
    let json_cmd = serde_json::to_string(&cmd).map_err(|e| e.to_string())? + "\r\n";

    // 1. Send command
    println!("[Rust Bridge] Sending: {} (Action: {})", json_cmd.trim(), action);
    if let Err(e) = child.write(json_cmd.as_bytes()) {
        eprintln!("[Rust Bridge] Error writing to sidecar: {}", e);
        return Err(e.to_string());
    }

    // 2. Wait for response in stdout channel
    // Сначала проверяем, нет ли уже готовой строки в буфере
    loop {
        while let Some(pos) = buffer.iter().position(|&b| b == b'\n') {
            let line_bytes = buffer.drain(..=pos).collect::<Vec<_>>();
            let line = String::from_utf8_lossy(&line_bytes);
            let trimmed = line.trim();
            if trimmed.is_empty() { continue; }

            println!("[Sidecar STDOUT] {}", trimmed);
            if let Ok(resp) = serde_json::from_str::<SidecarResponse>(trimmed) {
                return Ok(resp);
            }
        }

        if let Some(event) = rx.recv().await {
            match &event {
                CommandEvent::Stdout(bytes) => {
                    buffer.extend_from_slice(bytes);
                },
                CommandEvent::Stderr(bytes) => {
                    let line = String::from_utf8_lossy(bytes);
                    eprintln!("[Sidecar STDERR] {}", line.trim());
                },
                CommandEvent::Terminated(payload) => {
                    eprintln!("[Sidecar] Terminated unexpectedly: {:?}", payload);
                    return Err(format!("Sidecar terminated with code {:?}", payload.code));
                },
                _ => {}
            }
        } else {
            break;
        }
    }

    Err("Backend sidecar disconnected or timed out".to_string())
}

pub struct SidecarHandle {
    pub child: Mutex<Option<tauri_plugin_shell::process::CommandChild>>,
    pub rx: Mutex<(tauri::async_runtime::Receiver<CommandEvent>, Vec<u8>)>,
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  let start_time = std::time::Instant::now();

  tauri::Builder::default()
    .manage(StartupTime(start_time))
    .manage(PluginRegistry { paths: std::sync::Mutex::new(std::collections::HashMap::new()) })
    .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
        let _ = app.get_webview_window("main").map(|w| {
           let _ = w.show();
           let _ = w.unminimize();
           let _ = w.set_focus();
           let _ = w.emit("paws://window-show", ());
        });
    }))
    .plugin(tauri_plugin_autostart::init(tauri_plugin_autostart::MacosLauncher::LaunchAgent, Some(vec!["--autostart"])))
    .plugin(tauri_plugin_shell::init())
    .plugin(tauri_plugin_dialog::init())
    .register_uri_scheme_protocol("pawsapp", |_app, request| {
      let path = request.uri().path().trim_start_matches('/');
      
      // БЕЗОПАСНОСТЬ: Запрещаем выход из папки через '..'
      if path.contains("..") {
          return Response::builder().status(403).body(vec![]).unwrap();
      }
      
      println!("[pawsapp] Accessing path: {}", path);

      let bytes = match path {
          p if p.ends_with("paws-dark.css") => {
              Some(include_bytes!("../../src-ui/public/themes/paws-dark.css").to_vec())
          },
          p if p.ends_with("paws-light.css") => {
              Some(include_bytes!("../../src-ui/public/themes/paws-light.css").to_vec())
          },
          _ => None,
      };

      if let Some(css) = bytes {
          Response::builder()
              .header(header::CONTENT_TYPE, "text/css")
              .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
              .header("X-Content-Type-Options", "nosniff")
              .status(200)
              .body(css)
              .unwrap()
      } else {
          Response::builder()
              .status(404)
              .body("Not Found".as_bytes().to_vec())
              .unwrap()
      }
    })
    .register_uri_scheme_protocol("pawstheme", move |_app, request| {
      let hash = request.uri().path().trim_start_matches('/');
      
      // БЕЗОПАСНОСТЬ: Темы принимают ТУЛЬКО хеши (никаких слэшей или точек)
      if hash.contains('/') || hash.contains('\\') || hash.contains("..") {
          return Response::builder().status(403).body(vec![]).unwrap();
      }

      let data_dir = get_pawsdata_dir().join("data");
      let file_path = data_dir.join(hash);
      
      println!("[pawstheme] Local Read (Hash: {}). Path: {:?}", hash, file_path);

      if file_path.exists() {
          if let Ok(css_bytes) = std::fs::read(&file_path) {
              return Response::builder()
                  .header(header::CONTENT_TYPE, "text/css")
                  .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
                  .header("X-Content-Type-Options", "nosniff")
                  .status(200)
                  .body(css_bytes)
                  .unwrap();
          }
      }

      Response::builder()
          .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
          .status(404)
          .body("Blob not found".as_bytes().to_vec())
          .unwrap()
    })
    .register_uri_scheme_protocol("pawsplugin", move |app, request| {
      let full_path = request.uri().path().trim_start_matches('/');
      
      // БЕЗОПАСНОСТЬ
      if full_path.contains("..") {
          return Response::builder().status(403).body(vec![]).unwrap();
      }

      let parts: Vec<&str> = full_path.splitn(2, '/').collect();
      if parts.len() < 2 {
          return Response::builder().status(400).body("Bad URI format".as_bytes().to_vec()).unwrap();
      }
      
      let plugin_id = parts[0];
      let relative_path = parts[1];
      
      let registry = app.app_handle().state::<PluginRegistry>();
      let map = registry.paths.lock().unwrap();
      
      if let Some(base_path) = map.get(plugin_id) {
          // Ищем в папке ui/
          let file_path = std::path::Path::new(base_path).join("ui").join(relative_path);
          println!("[pawsplugin] Serving {:?} for {}", file_path, plugin_id);
          
          if file_path.exists() {
              if let Ok(file_bytes) = std::fs::read(&file_path) {
                  let mut content_type = "application/octet-stream";
                  if relative_path.ends_with(".html") { content_type = "text/html"; }
                  else if relative_path.ends_with(".js") || relative_path.ends_with(".mjs") { content_type = "application/javascript"; }
                  else if relative_path.ends_with(".css") { content_type = "text/css"; }
                  else if relative_path.ends_with(".svg") { content_type = "image/svg+xml"; }
                  
                  return Response::builder()
                      .header(header::CONTENT_TYPE, content_type)
                      .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
                      .status(200)
                      .body(file_bytes)
                      .unwrap();
              }
          }
      } else {
          println!("[pawsplugin] ID {} not found in PluginRegistry!", plugin_id);
      }

      Response::builder().status(404).body("Not found".as_bytes().to_vec()).unwrap()
    })
    .setup(|app| {
        use tauri::Manager;

        if cfg!(debug_assertions) {
          app.handle().plugin(
            tauri_plugin_log::Builder::default()
              .level(log::LevelFilter::Info)
              .build(),
          )?;
        }

        // --- Tray Icon Setup ---
        let menu = Menu::with_items(app, &[
            &MenuItem::with_id(app, "show", "Show Paws", true, None::<&str>)?,
            &MenuItem::with_id(app, "devtools", "Open DevTools", true, None::<&str>)?,
            &MenuItem::with_id(app, "quit", "Quit Paws", true, None::<&str>)?,
        ])?;

        let _tray = TrayIconBuilder::new()
            .menu(&menu)
            // Иконка обязательна для Windows, берем дефолтную
            .icon(app.default_window_icon().unwrap().clone())
            .tooltip("Paws")
            .on_menu_event(|app, event| match event.id.as_ref() {
                "quit" => {
                    // Пытаемся закрыть бэкенд мягко (через закрытие STDIN при дропе)
                    let handle = app.state::<SidecarHandle>();
                    {
                        let mut child_guard = tauri::async_runtime::block_on(handle.child.lock());
                        let _ = child_guard.take(); // Drop child -> close STDIN
                    }
                    // Даем бэкенду полсекунды на выход перед закрытием всего аппа
                    std::thread::sleep(std::time::Duration::from_millis(300));
                    app.exit(0);
                }
                "show" => {
                    if let Some(window) = app.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.unminimize();
                        let _ = window.set_focus();
                        let _ = window.emit("paws://window-show", ());
                    }
                }
                "devtools" => {
                    if let Some(window) = app.get_webview_window("main") {
                        window.open_devtools();
                    }
                }
                _ => {}
            })
            .on_tray_icon_event(|tray, event| {
                // Восстанавливаем окно при клике левой кнопкой
                if let TrayIconEvent::Click { button: MouseButton::Left, button_state: MouseButtonState::Up, .. } = event {
                    let app = tray.app_handle();
                    if let Some(window) = app.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            })
            .build(app)?;
        // --- End Tray Icon Setup ---

        let app_data = app.path().app_data_dir().expect("Failed to get app data dir");
        if !app_data.exists() {
            std::fs::create_dir_all(&app_data).expect("Failed to create app data dir");
        }
        println!("[Rust] Sidecar CWD will be: {}", app_data.display());

        // Запуск .NET Sidecar (paws-backend) через официальный API
        // Находим путь к бэкенду в ресурсах
        let resource_dir = app.path().resource_dir().expect("Failed to get resource dir");
        let backend_path = resource_dir.join("binaries").join("Paws.Sidecar.exe");
        
        println!("[Rust] Launching backend from resources: {:?}", backend_path);

        let (rx, child) = tauri_plugin_shell::ShellExt::shell(app).command(backend_path)
            .current_dir(app_data)
            .spawn()
            .expect("Failed to spawn .NET backend from resources");

        app.manage(SidecarHandle {
            child: Mutex::new(Some(child)),
            rx: Mutex::new((rx, Vec::new())),
        });

        Ok(())
    })
    .invoke_handler(tauri::generate_handler![call_sidecar, is_autostart_launch, get_startup_telemetry, register_plugin_path])
    .on_window_event(|window, event| {
        match event {
            WindowEvent::CloseRequested { api, .. } => {
                let _ = api.prevent_close();
                let _ = window.emit("paws://window-hide", ());
                let _ = window.hide();
            }
            WindowEvent::Destroyed => {
                let handle = window.state::<SidecarHandle>();
                let mut child_guard = tauri::async_runtime::block_on(handle.child.lock());
                if child_guard.is_some() {
                    println!("[Rust] Window destroyed. Dropping sidecar for graceful exit...");
                    let _ = child_guard.take(); // Drop child -> close STDIN
                }
            }
            _ => {}
        }
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
