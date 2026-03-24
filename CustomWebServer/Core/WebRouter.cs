using System;
using System.Collections.Generic;
using CustomWebServer.Data;

namespace CustomWebServer.Core
{
    public class WebRouter
    {
        // STEP 5: Cấu trúc dữ liệu ghi nhận hoạt động của phòng
        // Giả lập sẵn 2 phòng: room1 online, room2 đã offline do quá 5 phút
        private static Dictionary<string, DateTime> _roomActivities = new Dictionary<string, DateTime>
        {
            { "room1", DateTime.Now },
            { "room2", DateTime.Now.AddMinutes(-5) }
        };

        public HttpResponse Route(HttpRequest request)
        {
            if (request == null) return HttpResponse.HtmlResponse("<h1>400 Bad Request</h1>", 400);

            string path = request.Url.Split('?')[0];

            if (request.Method == "GET" && path == "/") return HandleHome(request);
            if (request.Method == "GET" && path == "/login") return HandleGetLogin(request);
            if (request.Method == "POST" && path == "/login") return HandlePostLogin(request);
            if (request.Method == "POST" && path == "/register") return HandlePostRegister(request);

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

            string html = @"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Danh sách phòng Chat</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { 
            font-family: 'Outfit', sans-serif; 
            background: linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%);
            min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 20px;
        }
        .dashboard-card {
            background: rgba(255, 255, 255, 0.95); width: 100%; max-width: 500px; padding: 35px; 
            border-radius: 24px; box-shadow: 0 15px 35px rgba(0,0,0,0.15); backdrop-filter: blur(10px);
        }
        .dashboard-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 25px; }
        .dashboard-header h1 { color: #2d3436; font-size: 1.5rem; font-weight: 600; }
        .btn-logout { background: #ff7675; color: white; padding: 8px 15px; border-radius: 8px; text-decoration: none; font-size: 0.9rem; font-weight: 600; transition: background 0.3s; }
        .btn-logout:hover { background: #d63031; }
        .room-list { list-style: none; display: flex; flex-direction: column; gap: 12px; }
        .room-item { 
            display: flex; justify-content: space-between; align-items: center; 
            padding: 18px 20px; background: white; border: 1px solid #dfe6e9; border-radius: 12px;
            transition: transform 0.2s, box-shadow 0.2s;
        }
        .room-item:hover { transform: translateY(-3px); box-shadow: 0 8px 20px rgba(0,0,0,0.08); border-color:#a18cd1; }
        .room-link { text-decoration: none; color: #6c5ce7; font-weight: 600; font-size: 1.1rem; }
        .room-link:hover { text-decoration: underline; }
        .status { padding: 5px 12px; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
        .status.online { background: #dff9fb; color: #009432; }
        .status.offline { background: #ffefea; color: #d63031; }
    </style>
</head>
<body>
    <div class='dashboard-card'>
        <div class='dashboard-header'>
            <h1>Danh sách phòng Chat</h1>
            <a href='/login' class='btn-logout'>Đăng xuất</a>
        </div>
        <ul class='room-list'>";
            
            var keys = new List<string>(_roomActivities.Keys);
            foreach (var roomId in keys)
            {
                var lastActivity = _roomActivities[roomId];
                
                // Toán tử kiểm tra sự kiện: DateTime.Now - LastActivityTime > 3 phút
                if ((DateTime.Now - lastActivity).TotalMinutes > 3)
                {
                    // Dọn dẹp JSON
                    JsonDataManager.Instance.ClearRoomHistory(roomId);
                    html += $"<li class='room-item'><a href='/chat/{roomId}' class='room-link'>Phòng {roomId}</a> <span class='status offline'>Offline (Xóa dữ liệu)</span></li>";
                }
                else
                {
                    html += $"<li class='room-item'><a href='/chat/{roomId}' class='room-link'>Phòng {roomId}</a> <span class='status online'>Đang hoạt động</span></li>";
                }
            }
            
            html += @"
        </ul>
    </div>
</body>
</html>";
            return HttpResponse.HtmlResponse(html);
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
            }

            // Redirect ngược lại endpoint lấy tin nhắn GET /chat/:id
            return HttpResponse.Redirect($"/chat/{roomId}");
        }

        private HttpResponse HandleGetChatRoom(HttpRequest request, string roomId)
        {
            if (!IsLoggedIn(request)) return HttpResponse.Redirect("/login");
            
            var history = JsonDataManager.Instance.GetChatHistory(roomId);
            
            // BƯỚC 7: ÁP DỤNG MẪU THIẾT KẾ BUILDER PATTERN
            var builder = new CustomWebServer.Views.ChatHtmlBuilder();
            string html = builder
                .SetTheme("#6c5ce7") // Màu sắc Theme Custom
                .AddHeader($"Phòng Trò Chuyện {roomId}")
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
            // Trích xuất email được gửi từ Firebase qua JS Client
            var postData = ParseUrlEncodedBody(request.Body);
            string email = postData.ContainsKey("email") ? postData["email"] : "";

            if (!string.IsNullOrEmpty(email))
            {
                // Cấp Token Server nội bộ cho User Firebase
                string token = SessionManager.CreateSession(email);
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
            background: linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%);
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
            border-left: 4px solid #6c5ce7; box-shadow: 0 2px 5px rgba(0,0,0,0.02);
        }
        .info-list strong { color: #2d3436; font-weight: 600; display: inline-block; min-width: 90px; }
        .btn-login {
            display: inline-block; width: 100%; padding: 14px; background: #6c5ce7; 
            color: white; border: none; border-radius: 12px; text-decoration: none;
            font-size: 1.05rem; font-weight: 600; cursor: pointer; 
            transition: all 0.3s; box-shadow: 0 4px 15px rgba(108, 92, 231, 0.3);
        }
        .btn-login:hover { background: #5f27cd; transform: translateY(-2px); box-shadow: 0 6px 20px rgba(108, 92, 231, 0.4); }
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
                if (kv.Length == 2) dict[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
            return dict;
        }
    }
}
