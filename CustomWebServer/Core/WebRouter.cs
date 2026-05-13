using System;
using System.Collections.Generic;
using CustomWebServer.Data;

namespace CustomWebServer.Core
{
    public class WebRouter
    {
        // STEP 5: Cấu trúc dữ liệu ghi nhận hoạt động của phòng
        // Cấu trúc dữ liệu ghi nhận hoạt động của phòng
        private static Dictionary<string, DateTime> _roomActivities = new Dictionary<string, DateTime> { { "all", DateTime.Now } };
        private static Dictionary<string, string> _roomNames = new Dictionary<string, string> { { "all", "Phòng Toàn Cầu (All)" } };
        private static Dictionary<string, HashSet<string>> _roomUsers = new Dictionary<string, HashSet<string>>();

        class RoomMeta
        {
            public Dictionary<string, DateTime> Activities { get; set; }
            public Dictionary<string, string> Names { get; set; }
            public Dictionary<string, HashSet<string>> Users { get; set; }
        }

        static WebRouter()
        {
            try
            {
                if (System.IO.File.Exists("Data/rooms_meta.json"))
                {
                    var json = System.IO.File.ReadAllText("Data/rooms_meta.json");
                    var meta = System.Text.Json.JsonSerializer.Deserialize<RoomMeta>(json);
                    if (meta != null)
                    {
                        if (meta.Activities != null) _roomActivities = meta.Activities;
                        if (meta.Names != null) _roomNames = meta.Names;
                        if (meta.Users != null) _roomUsers = meta.Users;
                    }
                }
            }
            catch { }
        }

        private static void SaveRoomsMeta()
        {
            try
            {
                var meta = new RoomMeta { Activities = _roomActivities, Names = _roomNames, Users = _roomUsers };
                System.IO.File.WriteAllText("Data/rooms_meta.json", System.Text.Json.JsonSerializer.Serialize(meta));
            }
            catch { }
        }

        public HttpResponse Route(HttpRequest request)
        {
            if (request == null) return HttpResponse.HtmlResponse("<h1>400 Bad Request</h1>", 400);

            string path = request.Url.Split('?')[0];

            if (request.Method == "GET" && path == "/") return HandleHome(request);
            if (request.Method == "GET" && path == "/login") return HandleGetLogin(request);
            if (request.Method == "POST" && path == "/login") return HandlePostLogin(request);
            if (request.Method == "POST" && path == "/register") return HandlePostRegister(request);
            if (request.Method == "POST" && path == "/create-room") return HandlePostCreateRoom(request);

            // STEP 6 (Firebase): Endpoint nhận thông tin từ Client Firebase
            if (request.Method == "POST" && path == "/api/auth/google") return HandleFirebaseGoogleLogin(request);

            // Cho phép serve file tĩnh như config JS (firebase-config.js)
            if (request.Method == "GET" && path.EndsWith(".js"))
            {
                string filePath = "HTML" + path;
                if (System.IO.File.Exists(filePath))
                {
                    var res = new HttpResponse { StatusCode = 200, StatusMessage = "OK" };
                    res.Headers["Content-Type"] = "application/javascript";
                    res.Body = System.Text.Encoding.UTF8.GetBytes(System.IO.File.ReadAllText(filePath));
                    return res;
                }
            }

            // STEP 5: Các logic xử lý Chat
            if (request.Method == "GET" && path == "/chat") return HandleGetChatList(request);

            if (request.Method == "GET" && path.StartsWith("/chat/") && path.Length > 6)
            {
                return HandleGetChatRoom(request, path.Substring(6));
            }

            if (request.Method == "POST" && path.StartsWith("/chat/") && path.Length > 6)
            {
                return HandlePostChatMessage(request, path.Substring(6));
            }

            return HttpResponse.HtmlResponse("<h1>404 Not Found</h1>", 404);
        }

        // -------------------------------------------------------------------------------- //
        // BƯỚC 5: LOGIC DỌN DẸP PHÒNG CHAT 3 PHÚT 
        // -------------------------------------------------------------------------------- //

        private HttpResponse HandleGetChatList(HttpRequest request)
        {
            if (!IsLoggedIn(request)) return HttpResponse.Redirect("/login");
            string currentUser = GetUsername(request);

            string html = @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Sảnh chờ - Socket Server</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { 
            font-family: 'Outfit', sans-serif; 
            background-color: #f9f6f0;
            background-image: linear-gradient(to right, #e8e4db 1px, transparent 1px), linear-gradient(to bottom, #e8e4db 1px, transparent 1px);
            background-size: 30px 30px;
            min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 20px;
        }
        .top-right { position: fixed; top: 25px; right: 25px; }
        .btn-logout { 
            background: rgba(255,255,255,0.25); color: white; padding: 10px 20px; 
            border-radius: 12px; text-decoration: none; font-weight: 600; 
            border: 1px solid rgba(255,255,255,0.3); backdrop-filter: blur(5px); transition: all 0.3s; 
        }
        .btn-logout:hover { background: rgba(255,255,255,0.45); }
        
        .dashboard-card {
            background: rgba(255, 255, 255, 0.95); width: 100%; max-width: 500px; padding: 40px; 
            border-radius: 24px; box-shadow: 0 15px 35px rgba(0,0,0,0.15); backdrop-filter: blur(10px);
        }
        .dashboard-header { text-align: center; margin-bottom: 30px; }
        .dashboard-header h1 { color: #2d3436; font-size: 1.6rem; font-weight: 600; margin-bottom: 6px; }
        .dashboard-header p { color: #636e72; font-size: 0.95rem; }
        
        .action-cards { display: flex; flex-direction: column; gap: 15px; margin-bottom: 25px; }
        .action-card { background: #f8f9fa; padding: 18px; border-radius: 16px; border: 1px solid #dfe6e9; text-align: left; }
        .action-card h3 { font-size: 1.05rem; color: #2d3436; margin-bottom: 12px; font-weight: 600; }
        .action-form { display: flex; gap: 10px; }
        .action-form input { 
            flex: 1; padding: 12px 16px; border: 1px solid #dfe6e9; border-radius: 10px; 
            font-size: 0.95rem; outline: none; transition: border-color 0.3s; font-family: 'Outfit', sans-serif; background: #fff;
        }
        .action-form input:focus { border-color: #a68a64; box-shadow: 0 0 0 3px rgba(166, 138, 100, 0.1); }
        .action-form button { 
            background: #a68a64; color: white; border: none; padding: 12px 20px; 
            border-radius: 10px; font-weight: 600; cursor: pointer; transition: all 0.3s; 
            font-family: 'Outfit', sans-serif; white-space: nowrap;
        }
        .action-form button.btn-secondary { background: #00b894; }
        .action-form button:hover { transform: translateY(-2px); box-shadow: 0 5px 15px rgba(0,0,0,0.1); }
        
        .section-title { font-size: 1rem; color: #2d3436; margin-bottom: 15px; font-weight: 600; border-bottom: 2px solid #f1f2f6; padding-bottom: 10px; }
        .room-list { list-style: none; display: flex; flex-direction: column; gap: 12px; }
        .room-item { 
            display: flex; justify-content: space-between; align-items: center; 
            padding: 18px 20px; background: white; border: 1px solid #dfe6e9; border-radius: 12px;
            transition: transform 0.2s, box-shadow 0.2s;
        }
        .room-item:hover { transform: translateY(-3px); box-shadow: 0 8px 20px rgba(0,0,0,0.08); border-color:#a68a64; }
        .room-link { text-decoration: none; color: #a68a64; font-weight: 600; font-size: 1.1rem; }
        .room-link:hover { text-decoration: underline; }
        .status { padding: 5px 12px; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
        .status.online { background: #dff9fb; color: #009432; }
    </style>
</head>
<body>
    <div class='top-right'>
        <a href='/login' class='btn-logout'>Đăng xuất</a>
    </div>

    <div class='dashboard-card'>
        <div class='dashboard-header'>
            <h1>Xin chào, <span style='color: #a68a64'>" + currentUser + @"</span>!</h1>
        </div>
        
        <div class='action-cards'>
            <div class='action-card'>
                <h3>Tạo phòng trò chuyện mới</h3>
                <form method='POST' action='/create-room' class='action-form'>
                    <input type='text' name='roomName' placeholder='Tên phòng (Vd: Nhóm học tập)...' required autocomplete='off' />
                    <button type='submit'>Tạo ngay</button>
                </form>
            </div>
            <div class='action-card'>
                <h3>Vào phòng có sẵn bằng ID</h3>
                <form class='action-form' onsubmit=""window.location.href='/chat/' + encodeURIComponent(document.getElementById('room-id-input').value.trim()); return false;"">
                    <input type='text' id='room-id-input' placeholder='Nhập ID phòng...' required autocomplete='off' />
                    <button type='submit' class='btn-secondary'>Tham gia</button>
                </form>
            </div>
        </div>

        <div class='section-title'>Các phòng khả dụng</div>
        <ul class='room-list'>";
            
            var keys = new List<string>(_roomActivities.Keys);
            bool hasActive = false;
            foreach (var roomId in keys)
            {
                var lastActivity = _roomActivities[roomId];
                bool isOnline = true;
                
                // Toán tử kiểm tra sự kiện: DateTime.Now - LastActivityTime > 3 phút
                if (roomId != "all" && (DateTime.Now - lastActivity).TotalMinutes > 3)
                {
                    isOnline = false;
                    // Dọn dẹp JSON
                    JsonDataManager.Instance.ClearRoomHistory(roomId);
                }

                // Chỉ hiển thị nếu user có trong danh sách _roomUsers hoặc là phòng "all"
                bool isJoined = roomId == "all" || (_roomUsers.ContainsKey(roomId) && _roomUsers[roomId].Contains(currentUser));
                if (!isJoined) continue;

                hasActive = true;
                string roomName = _roomNames.ContainsKey(roomId) ? _roomNames[roomId] : $"Phòng {roomId}";
                html += $"<li class='room-item'>";
                html += $"<div><a href='/chat/{roomId}' class='room-link'>{roomName}</a><div style='font-size: 0.8rem; color: #a4b0be; margin-top: 5px;'>ID: {roomId}</div></div>";
                if (isOnline)
                {
                    html += $"<span class='status online'>Hoạt động</span></li>";
                }
                else
                {
                    html += $"<span class='status offline' style='background: #f5f6fa; color: #7f8fa6;'>Ngoại tuyến</span></li>";
                }
            }

            if (!hasActive)
            {
                html += "<li style='text-align:center; color:#b2bec3; padding: 15px;'><small>Chưa có phòng nào. Hãy tạo phòng mới ở khung trên!</small></li>";
            }
            
            html += @"
        </ul>
    </div>
</body>
</html>";
            return HttpResponse.HtmlResponse(html);
        }

        private HttpResponse HandlePostCreateRoom(HttpRequest request)
        {
            if (!IsLoggedIn(request)) return HttpResponse.Redirect("/login");
            var postData = ParseUrlEncodedBody(request.Body);
            string roomName = postData.ContainsKey("roomName") ? postData["roomName"] : "Phòng Mới";
            if (string.IsNullOrWhiteSpace(roomName)) roomName = "Phòng Mới";
            
            // Generate a random 6-character room ID
            string roomId = Guid.NewGuid().ToString("N").Substring(0, 6);
            
            _roomNames[roomId] = roomName;
            _roomActivities[roomId] = DateTime.Now;

            string currentUser = GetUsername(request);
            if (currentUser != null)
            {
                if (!_roomUsers.ContainsKey(roomId)) _roomUsers[roomId] = new HashSet<string>();
                _roomUsers[roomId].Add(currentUser);
            }
            
            SaveRoomsMeta();
            return HttpResponse.Redirect($"/chat/{roomId}");
        }

        private HttpResponse HandlePostChatMessage(HttpRequest request, string roomId)
        {
            string username = GetUsername(request);
            if (username == null) return HttpResponse.Redirect("/login");

            var postData = ParseUrlEncodedBody(request.Body);
            if (postData.TryGetValue("message", out string message) && !string.IsNullOrWhiteSpace(message))
            {
                // Lưu tin nhắn vào JSON thông qua Singleton
                JsonDataManager.Instance.SaveChatMessage(roomId, username, message);
                
                // Cập nhật hoạt động: Đưa status phòng đó về lại bằng DateTime.Now
                _roomActivities[roomId] = DateTime.Now;
                SaveRoomsMeta();
            }

            // Redirect ngược lại endpoint lấy tin nhắn GET /chat/:id
            return HttpResponse.Redirect($"/chat/{roomId}");
        }

        private HttpResponse HandleGetChatRoom(HttpRequest request, string roomId)
        {
            string currentUser = GetUsername(request);
            if (currentUser == null) return HttpResponse.Redirect("/login");
            
            // Kiểm tra xem phòng có tồn tại không
            if (roomId != "all" && !_roomActivities.ContainsKey(roomId))
            {
                return HttpResponse.HtmlResponse("<h1>Lỗi</h1><p>Phòng chat không tồn tại hoặc đã hết hạn!</p><a href='/chat'>Quay lại sảnh chờ</a>", 404);
            }

            // Ghi nhận User này đã truy cập vào phòng (Join Room)
            if (!_roomUsers.ContainsKey(roomId)) _roomUsers[roomId] = new HashSet<string>();
            _roomUsers[roomId].Add(currentUser);

            // Cập nhật lại Activity để phòng sống lâu hơn nếu có người mới quan tâm
            _roomActivities[roomId] = DateTime.Now;
            SaveRoomsMeta();

            var history = JsonDataManager.Instance.GetChatHistory(roomId);
            
            // BƯỚC 7: ÁP DỤNG MẪU THIẾT KẾ BUILDER PATTERN
            var builder = new CustomWebServer.Views.ChatHtmlBuilder();
            string roomName = _roomNames.ContainsKey(roomId) ? _roomNames[roomId] : $"Phòng {roomId}";
            string headerTitle = $"{roomName} <small style='font-size: 0.8rem; font-weight: 400; opacity: 0.85; margin-left: 8px; background: rgba(0,0,0,0.15); padding: 3px 8px; border-radius: 10px;'>ID: {roomId}</small>";
            
            string html = builder
                .SetTheme("#a68a64") // Màu sắc Theme Custom
                .SetCurrentUser(currentUser)
                .AddHeader(headerTitle)
                .AddMessageList(history)
                .AddInputForm(roomId) // Tích hợp JavaScript Interval và AJAX tích hợp sẵn
                .Build();

            return HttpResponse.HtmlResponse(html);
        }

        // -------------------------------------------------------------------------------- //
        // BƯỚC 6: TÍCH HỢP FIREBASE POPUP CLENT-SIDE
        // -------------------------------------------------------------------------------- //

        private HttpResponse HandleFirebaseGoogleLogin(HttpRequest request)
        {
            // Trích xuất Tên (DisplayName) được gửi từ Firebase qua JS Client
            var postData = ParseUrlEncodedBody(request.Body);
            string username = postData.ContainsKey("username") ? postData["username"] : "";

            if (!string.IsNullOrEmpty(username))
            {
                // Cấp Token Server nội bộ cho User Firebase
                string token = SessionManager.CreateSession(username);
                var response = HttpResponse.JsonResponse("{\"success\":true}");
                response.SetCookie("auth_token", token, 180); 
                return response;
            }
            
            return HttpResponse.JsonResponse("{\"success\":false}", 401);
        }

        // -------------------------------------------------------------------------------- //
        // CÁC HÀM XỬ LÝ KHÁC 
        // -------------------------------------------------------------------------------- //

        private HttpResponse HandleHome(HttpRequest request)
        {
            string html = @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông tin sinh viên - Socket Server</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { 
            font-family: 'Outfit', sans-serif; 
            background-color: #f9f6f0;
            background-image: linear-gradient(to right, #e8e4db 1px, transparent 1px), linear-gradient(to bottom, #e8e4db 1px, transparent 1px);
            background-size: 30px 30px;
            height: 100vh; display: flex; align-items: center; justify-content: center;
        }
        .info-card {
            background: rgba(255, 255, 255, 0.95); width: 400px; padding: 40px; 
            border-radius: 24px; box-shadow: 0 15px 35px rgba(0,0,0,0.15);
            text-align: center; backdrop-filter: blur(10px);
        }
        .info-card h1 { margin-bottom: 25px; color: #2d3436; font-size: 1.8rem; font-weight: 600; }
        .info-list { text-align: left; margin-bottom: 30px; list-style: none; }
        .info-list li { 
            margin-bottom: 12px; font-size: 1.05rem; color: #636e72; 
            padding: 12px 15px; background: #fdfdfd; border-radius: 8px; 
            border-left: 4px solid #a68a64; box-shadow: 0 2px 5px rgba(0,0,0,0.02);
        }
        .info-list strong { color: #2d3436; font-weight: 600; display: inline-block; min-width: 90px; }
        .btn-login {
            display: inline-block; width: 100%; padding: 14px; background: #a68a64; 
            color: white; border: none; border-radius: 12px; text-decoration: none;
            font-size: 1.05rem; font-weight: 600; cursor: pointer; 
            transition: all 0.3s; box-shadow: 0 4px 15px rgba(166, 138, 100, 0.3);
        }
        .btn-login:hover { background: #8e724f; transform: translateY(-2px); box-shadow: 0 6px 20px rgba(166, 138, 100, 0.4); }
    </style>
</head>
<body>
    <div class='info-card'>
        <h1>Test (Coding)</h1>
        <ul class='info-list'>
            <li><strong>MSSV:</strong> 22301270</li>
            <li><strong>Họ Tên:</strong> Hà Hiệp Thanh</li>
            <li><strong>PC's no:</strong> 01</li>
        </ul>
        <a href='/login' class='btn-login'>Tiến tới Đăng nhập</a>
    </div>
</body>
</html>";
            return HttpResponse.HtmlResponse(html);
        }

        private HttpResponse HandleGetLogin(HttpRequest request)
        {
            // BƯỚC 8: ĐỌC FILE LÊN TỪ Ổ ĐĨA ("HTML/login.html")
            string filePath = "HTML/login.html";
            if (System.IO.File.Exists(filePath))
            {
                return HttpResponse.HtmlResponse(System.IO.File.ReadAllText(filePath));
            }

            // Fallback nếu chưa tạo file
            string html = @"
                <h1>Đăng nhập nội bộ</h1>
                <form method='POST' action='/login'>
                    <input type='text' name='username' placeholder='nntu'/><br/>
                    <input type='password' name='password' placeholder='56789'/><br/>
                    <button type='submit'>Đăng nhập</button>
                </form>
                <hr/><a href='/auth/google/login'>Mock Google Login Bypass</a>";
            return HttpResponse.HtmlResponse(html);
        }

        // --- LOGIC TẠO TÀI KHOẢN (REGISTER) ---
        private HttpResponse HandlePostRegister(HttpRequest request)
        {
            var postData = ParseUrlEncodedBody(request.Body);
            string username = postData.ContainsKey("username") ? postData["username"] : "";
            string password = postData.ContainsKey("password") ? postData["password"] : "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return HttpResponse.HtmlResponse("<h1>Lỗi</h1><p>Dữ liệu trống, vui lòng thử lại.</p><a href='/login'>Quay lại</a>", 400);
            }

            // Gọi JsonDataManager để kiểm tra và lưu user
            bool isSuccess = JsonDataManager.Instance.RegisterUser(username, password);

            if (isSuccess)
            {
                // Đăng ký xong, tự động đăng nhập và redirect về chat
                string token = SessionManager.CreateSession(username);
                var response = HttpResponse.Redirect("/chat");
                response.SetCookie("auth_token", token, 180);
                return response;
            }
            else
            {
                // Trả về lỗi 409 Conflict
                return HttpResponse.HtmlResponse("<h1>Đăng ký thất bại</h1><p>Tên tài khoản này đã được sử dụng!</p><a href='/login'>Quay lại trang Đăng nhập</a>", 409);
            }
        }

        private HttpResponse HandlePostLogin(HttpRequest request)
        {
            var postData = ParseUrlEncodedBody(request.Body);
            string username = postData.ContainsKey("username") ? postData["username"] : "";
            string password = postData.ContainsKey("password") ? postData["password"] : "";

            if (JsonDataManager.Instance.ValidateUser(username, password))
            {
                string token = SessionManager.CreateSession(username);
                var response = HttpResponse.Redirect("/chat");
                response.SetCookie("auth_token", token, 180);
                return response;
            }
            return HttpResponse.HtmlResponse("<h1>Sai thông tin!</h1><a href='/login'>Thử lại</a>", 401);
        }

        private bool IsLoggedIn(HttpRequest request) => GetUsername(request) != null;

        private string GetUsername(HttpRequest request)
        {
            if (request.Cookies.TryGetValue("auth_token", out string token))
            {
                return SessionManager.GetUsername(token);
            }
            return null;
        }

        private Dictionary<string, string> ParseUrlEncodedBody(string body)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(body)) return dict;
            foreach (var pair in body.Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2) dict[Uri.UnescapeDataString(kv[0]).Replace('+', ' ')] = Uri.UnescapeDataString(kv[1].Replace('+', ' '));
            }
            return dict;
        }
    }
}
