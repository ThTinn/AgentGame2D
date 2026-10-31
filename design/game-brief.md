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
- Không dùng art thật: toàn bộ là hình khối đặt màu.

## Build order
1. Logic đếm xu và test.
2. Di chuyển và nhảy trong scene.
3. Nhặt xu, thắng, chơi lại.
4. HUD.

---
**Who it's for / what they feel:** Lập trình viên cần một dự án mẫu chạy được để phát triển tiếp.

**Art & audio direction:** Hình khối phẳng màu tươi, chưa có âm thanh.

**Reference game:** Super Mario Bros. — chỉ giữ phần chạy, nhảy và nhặt xu.
