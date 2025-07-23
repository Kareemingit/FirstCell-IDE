using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace FirstCell
{
    public class LiveReloadServer
    {
        private readonly HttpListener httpListener = new();
        private HttpListenerContext? lastHtmlContext;
        private readonly List<WebSocket> connectedSockets = new();
        private readonly string rootPath;
        private bool running = false;

        public LiveReloadServer(string projectRoot)
        {
            rootPath = projectRoot;
            httpListener.Prefixes.Add("http://localhost:8080/");
        }

        public async Task StartAsync()
        {
            if (running) return;
            running = true;
            httpListener.Start();

            _ = Task.Run(async () =>
            {
                while (running)
                {
                    var context = await httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                        _ = HandleWebSocketAsync(context);
                    else
                        _ = HandleHttpRequestAsync(context);
                }
            });
        }

        public void Stop()
        {
            running = false;
            httpListener.Stop();
            foreach (var ws in connectedSockets)
                if (ws.State == WebSocketState.Open)
                    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None).Wait();
        }

        private async Task HandleHttpRequestAsync(HttpListenerContext context)
        {
            string relativePath = context.Request.Url!.AbsolutePath.TrimStart('/');
            string filePath = System.IO.Path.Combine(rootPath, relativePath);

            if (!System.IO.File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            string contentType = filePath.EndsWith(".html") ? "text/html" :
                                 filePath.EndsWith(".js") ? "application/javascript" :
                                 filePath.EndsWith(".css") ? "text/css" : "application/octet-stream";

            byte[] content;
            if (filePath.EndsWith(".html"))
            {
                lastHtmlContext = context;

                string html;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    html = await reader.ReadToEndAsync();
                }

                if (!html.Contains("__livereload"))
                {
                    html = LiveReloadInjector.Inject(html);
                    
                }
                content = Encoding.UTF8.GetBytes(html);
            }
            else
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    await fs.CopyToAsync(ms);
                    content = ms.ToArray();
                }
            }
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = content.Length;
            await context.Response.OutputStream.WriteAsync(content, 0, content.Length);
            context.Response.Close();
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            var socket = wsContext.WebSocket;
            connectedSockets.Add(socket);

            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                }
            }
            connectedSockets.Remove(socket);
        }

        public async Task ReloadClientsAsync()
        {
            

            var message = Encoding.UTF8.GetBytes("reload");
            foreach (var socket in connectedSockets)
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }


    public static class LiveReloadInjector
    {
        private const string ScriptId = "__livereload";
        private const string InjectedScript =
            "<script id=\"__livereload\">" +
            "var ws = new WebSocket('ws://localhost:8080');" +
            "ws.onmessage = function(msg) { if (msg.data === 'reload') location.reload(); };" +
            "</script>";

        public static string Inject(string html)
        {
            if (html.Contains(ScriptId)) return html;
            return Regex.Replace(html, "</body>", InjectedScript + "</body>", RegexOptions.IgnoreCase);
        }

        public static string Remove(string html)
        {
            return Regex.Replace(html, $"<script[^>]*id=\"{ScriptId}\"[^>]*>.*?</script>", string.Empty, RegexOptions.Singleline);
        }

        public static void RemoveFromFile(string filePath)
        {
            string content = System.IO.File.ReadAllText(filePath);
            string cleaned = Remove(content);
            System.IO.File.WriteAllText(filePath, cleaned);
        }
    }

    public class FileWatcher
    {
        private FileSystemWatcher watcher;
        private string projectpath;
        private LiveReloadServer liveReloadServer;
        public FileWatcher(string projectPath, LiveReloadServer liveReloadServer)
        {
            this.liveReloadServer = liveReloadServer;

            watcher = new FileSystemWatcher(projectPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                Filter = "*.*"
            };

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            watcher.Changed += OnChanged;
        }

        private readonly Dictionary<string, DateTime> _lastChanged = new();

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.Now;

            if (_lastChanged.TryGetValue(e.FullPath, out DateTime lastTime))
            {
                if ((now - lastTime).TotalMilliseconds < 500)
                    return;
            }

            _lastChanged[e.FullPath] = now;

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    if (IsFileReady(e.FullPath))
                    {
                        await liveReloadServer.ReloadClientsAsync();
                        break;
                    }
                    await Task.Delay(100);
                }
            });
        }

        private bool IsFileReady(string path)
        {
            try
            {
                using var stream = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}