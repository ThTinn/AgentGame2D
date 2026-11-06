# Game Brief: Coin Hop

**One-sentence pitch:** Một game platformer 2D siêu ngắn: chạy, nhảy qua các bệ và nhặt hết xu trên một màn chơi.

## Core loop
- Di chuyển và nhảy giữa các bệ.
- Nhặt xu, bộ đếm trên HUD tăng lên.
- Rơi khỏi màn thì quay về điểm xuất phát, xu đã nhặt vẫn giữ nguyên.

## Player goal & fail state — what "working" looks like
- Thắng: nhặt đủ mọi xu trong màn, HUD hiện thông báo thắng và người chơi nhấn nhảy để chơi lại.
- Không có thua hẳn: rơi xuống dưới `PlayerConfig.FallLimitY` chỉ đưa người chơi về điểm xuất phát.

## MVP — what must exist to be the game
- Nhân vật di chuyển trái/phải và nhảy (bàn phím, gamepad).
- Một màn chơi có mặt đất, bệ và xu.
- Đếm xu và phát hiện thắng.
- Rơi khỏi màn thì hồi vị trí.
- HUD hiện số xu và thông báo thắng.

## Out of scope — not building this
- Một màn duy nhất, không có menu.
- Không kẻ địch, không lưu game, không âm thanh, không bản địa hoá.
- Ngoài nhân vật người chơi, không dùng art thật: bệ, xu và nền vẫn là hình khối đặt màu.
- Chưa có màn hình đổi trang phục hoàn chỉnh; chỉ có bảng thay đồ tạm để thử (tiếng Anh, chuột, chưa hỗ trợ tay cầm, chưa qua localization).

## Build order
1. Logic đếm xu và test.
2. Di chuyển và nhảy trong scene.
3. Nhặt xu, thắng, chơi lại.
4. HUD.

---
**Who it's for / what they feel:** Lập trình viên cần một dự án mẫu chạy được để phát triển tiếp.

**Art & audio direction:** Hình khối phẳng màu tươi, chưa có âm thanh. Riêng nhân vật người chơi dùng sprite pixel art LPC (xem mục dưới).

## Nhân vật và trang phục

- Nhân vật người chơi ghép từ các lớp sprite LPC (Universal LPC Spritesheet Character Generator), khung 64×64, chỉ dùng hàng hướng phải; quay trái thì lật ngang.
- Mỗi lớp là một món đồ đặt vào một ô: thân, đầu, mặt, tóc, áo, quần, giày, nón. Mỗi ô chỉ mặc một món; mặc món mới vào ô đã có thì thay món cũ, và có thể bỏ trống ô (ví dụ không đội nón).
- Mọi lớp dùng chung một khung hình nên thay đồ không làm gián đoạn animation.
- Animation theo chuyển động: đứng yên → idle, chạy trên mặt đất → run, đang bay lên → jump, đang rơi → fall. Ngưỡng tốc độ, khung và tốc độ khung nằm trong `Assets/Data/LpcAnimations.asset`.
- Bộ đồ mặc định: thân da sáng, đầu nam, mặt Neutral, tóc Plain nâu, áo thun đỏ, quần navy, giày nâu, nón da nâu.
- Đồ thay thế: tóc Buzzcut đen, tóc Curly short vàng; áo Longsleeve 2 xanh, áo Sleeveless 2 trắng; quần Shorts nâu, quần Long Pants xám; ủng Revised Boots đen; nón Tricorne nâu, nón Bonnie xanh lá; nón có thể bỏ trống.
- Tài nguyên: Tools > LPC > Build Parts cắt mỗi sheet xuống chỉ còn các khung hướng phải mà `LpcAnimations` dùng (cắt sát vùng có pixel) rồi gói mọi khung vào một sprite atlas; bản build chỉ chứa atlas. Việc cắt là một chiều: muốn dùng thêm khung hay hướng khác thì chép lại sheet gốc từ bản export của generator.
- Giấy phép: chỉ dùng món có OGA-BY 3.0 hoặc CC0. Credits nằm ở `Assets/Art/Characters/Lpc/CREDITS.txt` và phải hiện trong game trước khi phát hành.

**Reference game:** Super Mario Bros. — chỉ giữ phần chạy, nhảy và nhặt xu.
