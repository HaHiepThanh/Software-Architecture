using System.Collections.Generic;
using System.Text;
using CustomWebServer.Models;

namespace CustomWebServer.Views
{
    public class ChatHtmlBuilder
    {
        private StringBuilder _html;
        private string _themeColor = "#3498db"; 

        public ChatHtmlBuilder()
        {
            _html = new StringBuilder();
            _html.Append("<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'>");
            _html.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            _html.Append("<title>Trò chuyện - Custom Web Server</title>");
            
            // Xây dựng CSS đẹp mắt đáp ứng yêu cầu UI Gọn Gàng
            _html.Append(@"
                <style>
                    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap');
                    body { font-family: 'Inter', sans-serif; background: #eef2f5; margin: 0; display: flex; justify-content: center; align-items: center; height: 100vh; }
                    .chat-container { width: 100%; max-width: 600px; height: 85vh; background: #fff; border-radius: 16px; box-shadow: 0 10px 30px rgba(0,0,0,0.08); display: flex; flex-direction: column; overflow: hidden; }
                    .chat-header { background: THEME_COLOR; color: white; padding: 20px; text-align: center; font-size: 1.25rem; font-weight: 600; box-shadow: 0 2px 5px rgba(0,0,0,0.1); z-index: 10; display: flex; justify-content: space-between; align-items: center; }
                    .back-btn { color: white; text-decoration: none; font-size: 0.9rem; opacity: 0.8; transition: opacity 0.2s; }
                    .back-btn:hover { opacity: 1; }
                    .chat-messages { flex: 1; padding: 20px; overflow-y: auto; background: #fdfdfd; display: flex; flex-direction: column; gap: 15px; }
                    .message { padding: 12px 18px; border-radius: 20px; max-width: 75%; position: relative; font-size: 0.95rem; line-height: 1.5; box-shadow: 0 2px 8px rgba(0,0,0,0.04); }
                    .message.system { align-self: center; background: #f1f2f6; color: #7f8fa6; border-radius: 8px; font-size: 0.85rem; box-shadow: none; padding: 8px 15px; }
                    .message.user { align-self: flex-start; background: #ffffff; border: 1px solid #eee; color: #2f3542; border-bottom-left-radius: 4px; }
                    .message-username { font-weight: 600; color: THEME_COLOR; margin-bottom: 4px; display: block; font-size: 0.85rem; }
                    .message-time { font-size: 0.7rem; color: #a4b0be; position: absolute; right: -40px; bottom: 5px; }
                    .chat-form { display: flex; padding: 15px 20px; background: white; border-top: 1px solid #f1f2f6; align-items: center; gap: 10px; }
                    .chat-form input { flex: 1; padding: 14px 20px; border: 1px solid #dfe4ea; border-radius: 30px; outline: none; transition: border-color 0.3s; background: #f8f9fa; font-family: 'Inter', sans-serif;}
                    .chat-form input:focus { border-color: THEME_COLOR; background: white; }
                    .chat-form button { background: THEME_COLOR; color: white; border: none; padding: 14px 24px; border-radius: 30px; font-weight: 600; cursor: pointer; transition: transform 0.2s, box-shadow 0.2s; font-family: 'Inter', sans-serif;}
                    .chat-form button:hover { transform: translateY(-2px); box-shadow: 0 5px 15px rgba(0,0,0,0.1); }
                </style>");
            _html.Append("</head><body><div class='chat-container'>");
        }

        public ChatHtmlBuilder SetTheme(string color)
        {
            _themeColor = color;
            _html.Replace("THEME_COLOR", color); // Replaces all CSS placeholders dynamically
            return this;
        }

        public ChatHtmlBuilder AddHeader(string roomName)
        {
            _html.Append($@"
                <div class='chat-header' style='background: {_themeColor}'>
                    <a href='/chat' class='back-btn'>&larr; Khác</a>
                    <span>{roomName}</span>
                    <span style='width: 40px'></span> <!-- Spacer for centering -->
                </div>");
            return this;
        }

        public ChatHtmlBuilder AddMessageList(List<ChatMessage> messages)
        {
            _html.Append("<div class='chat-messages' id='chat-box'>");
            if (messages == null || messages.Count == 0)
            {
                _html.Append("<div class='message system'>Phòng rỗng. Hãy là người đầu tiên nhắn tin!</div>");
            }
            else
            {
                foreach (var msg in messages)
                {
                    _html.Append("<div class='message user'>");
                    _html.Append($"<span class='message-username'>{msg.Username}</span>");
                    _html.Append($"<span class='message-text'>{msg.Message}</span>");
                    _html.Append($"<span class='message-time'>{msg.Time.ToLocalTime():HH:mm}</span>");
                    _html.Append("</div>");
                }
            }
            _html.Append("</div>");
            return this;
        }

        public ChatHtmlBuilder AddInputForm(string roomId)
        {
            _html.Append($@"
                <form class='chat-form' id='chat-form'>
                    <input type='text' id='messageInput' placeholder='Soạn tin nhắn...' required autocomplete='off' />
                    <button type='submit'>Gửi</button>
                </form>
            ");

            // BƯỚC 8: Thêm Javascript Polling và AJAX POST vào đây
            _html.Append($@"
                <script>
                    const roomId = '{roomId}';
                    const chatBox = document.getElementById('chat-box');
                    const chatForm = document.getElementById('chat-form');
                    const messageInput = document.getElementById('messageInput');

                    function scrollToBottom() {{
                        chatBox.scrollTop = chatBox.scrollHeight;
                    }}
                    scrollToBottom();

                    // --- AJAX POST KHI GỬI TIN NHẮN ---
                    chatForm.addEventListener('submit', function(e) {{
                        e.preventDefault();
                        const msg = messageInput.value.trim();
                        if(!msg) return;

                        fetch('/chat/' + roomId, {{
                            method: 'POST',
                            headers: {{ 'Content-Type': 'application/x-www-form-urlencoded' }},
                            body: 'message=' + encodeURIComponent(msg)
                        }}).then(res => {{
                            messageInput.value = ''; // Xóa sạch ô nhập
                            fetchMessages(); // Tải lại ngay lập tức thay vì đợi poll
                        }});
                    }});

                    // --- LONG POLLING: GỌI MỖI 2 GIÂY ĐỂ ĐỒNG BỘ TIN NHẮN REALTIME ---
                    function fetchMessages() {{
                        fetch('/chat/' + roomId)
                            .then(res => res.text())
                            .then(html => {{
                                // Chỉ trích xuất phần #chat-box từ HTML trả về
                                const parser = new DOMParser();
                                const doc = parser.parseFromString(html, 'text/html');
                                const newChatBox = doc.getElementById('chat-box');
                                
                                if (newChatBox) {{
                                    // Kiểm tra xem user có đang lướt lên trên không
                                    const isScrolledToBottom = Math.abs((chatBox.scrollHeight - chatBox.clientHeight) - chatBox.scrollTop) < 50;
                                    chatBox.innerHTML = newChatBox.innerHTML;
                                    if (isScrolledToBottom) scrollToBottom();
                                }}
                            }})
                            .catch(err => console.log('Lỗi Polling:', err));
                    }}

                    // Chạy timer lặp lại Get Data từ Server
                    setInterval(fetchMessages, 2000);
                </script>
            ");
            return this;
        }

        public string Build()
        {
            _html.Append("</div></body></html>");
            return _html.ToString();
        }
    }
}
