# Game Brief: DungeonOnline (tên tạm)

**One-sentence pitch:** MMO hành động nhìn ngang 2D: gặp nhau ở thành chung, lập tổ 1–4 người vào phó bản hầm ngục, đánh quái và boss để nhặt đồ chỉ số ngẫu nhiên theo độ hiếm; chơi được trên PC và điện thoại.

## Core loop
- Ở **thành** (khu công cộng): gặp người khác, chat, mua bán với NPC, lập tổ đội, chỉnh trang bị và kỹ năng.
- Vào **phó bản** theo tổ: chạy, nhảy qua các phòng, diệt quái, tới phòng boss.
- Nhận **đồ rơi** và vàng, lên cấp, mở kỹ năng, quay về thành mạnh hơn.
- Sau MVP: **đấu trường** PvP riêng.

## Player goal & fail state — what "working" looks like
- Thắng một phó bản: hạ boss; mỗi người có phần đồ rơi riêng, không tranh nhặt.
- Chết trong phó bản: chờ 10 giây rồi hồi sinh ở điểm lưu gần nhất trong phó bản.
- Cả tổ chết cùng lúc: phó bản kết thúc, về thành; giữ đồ đã nhặt, không bị trừ gì.
- Ở thành không có chiến đấu.

## Điều khiển (PC và điện thoại ngay từ đầu)
- **PC:** trái/phải, nhảy, đánh thường, 4 phím skill; skill nhắm vùng dùng vị trí chuột. Có hỗ trợ tay cầm.
- **Điện thoại:** cần ảo trái/phải, nút nhảy, nút đánh, 4 nút skill; skill nhắm vùng thì kéo nút để nhắm, thả để dùng.

## Chiến đấu (kiểu lai)
- **Đánh thường tự nhắm:** tự quay về con quái gần nhất trong tầm (cả hai phía) và đánh nó.
- **Skill lớn nhắm tay:** loại bắn theo hướng nhân vật đang quay, hoặc loại đặt vào một vùng.
- **Server quyết định** trúng/trượt, sát thương, máu quái, chết và đồ rơi; client chỉ diễn animation trước cho mượt.

## Lớp nhân vật
- Nhiều lớp; mỗi lớp có cây kỹ năng riêng, chia nhiều nhánh.
- MVP có 3 lớp, chọn theo animation LPC có sẵn: **Chiến binh** (chém gần, slash), **Cung thủ** (bắn xa, shoot), **Pháp sư** (phép vùng, spellcast).
- Mỗi lớp trong MVP: đánh thường cộng 3 skill, cây kỹ năng có khung 2 nhánh. Số liệu chỉ cần chạy được; cân bằng làm sau.
- Mọi chỉ số lớp và skill nằm trong template dữ liệu do server phát, không viết cứng trong code.

## Đồ và độ hiếm
- Đồ có chỉ số ngẫu nhiên, giới hạn theo độ hiếm. 5 bậc:

  | Độ hiếm | Số dòng chỉ số phụ |
  |---|---|
  | Thường | 0 |
  | Tốt | 1 |
  | Hiếm | 2 |
  | Sử thi | 3 |
  | Huyền thoại | 4 |

- Mỗi dòng chỉ số có khoảng tối thiểu–tối đa theo bậc hiếm và cấp đồ; bảng khoảng giá trị nằm trong template.
- **Chỉ server quay ra đồ.** Client không đoán trước kết quả (luật NetKit cho kết quả ngẫu nhiên).
- Một số món đổi ngoại hình LPC (áo, nón, vũ khí); món còn lại chỉ đổi chỉ số.

## MVP — what must exist to be the game
- Đăng nhập, tạo nhân vật (chọn lớp và ngoại hình LPC).
- Một thành chung: thấy người khác đi lại và nhảy, đồng bộ ngoại hình, chat.
- Lập tổ 1–4 người, vào 1 phó bản: vài phòng, 2–3 loại quái, 1 boss.
- Chiến đấu kiểu lai, máu, chết, hồi sinh.
- Đồ rơi theo độ hiếm, túi đồ, mặc đồ, cấp nhân vật.
- 3 lớp với skill cơ bản.
- Điều khiển chạy được trên cả PC lẫn Android.

## Out of scope — not building this
- Đấu trường PvP (làm ngay sau MVP).
- Bang hội, giao dịch giữa người chơi, nhà đấu giá, chế tạo, thú cưỡi, thú nuôi.
- Cân bằng số liệu chi tiết, nhánh kỹ năng thứ hai đầy đủ.
- iOS.
- Bản địa hóa (chỉ tiếng Việt trước).

## Quyết định kỹ thuật
- **Server Java**, dựa trên NetKit (`D:/WorkPlace/Project/T5/TeamB/GameB/Game/NetKit`); tham khảo `game/combat`, `game/bot` của GameB.
- Luật cả hai bên đều cần (sát thương, công thức chỉ số) viết hai bản C# và Java; mỗi bản có test với cùng bộ số đầu vào/đầu ra để giữ khớp.
- **Di chuyển:** client mô phỏng chạy nhảy bằng vật lý Unity; server kiểm tốc độ và vị trí so với hình học bệ xuất từ Tilemap (gửi dạng template), không chạy vật lý đầy đủ.
- **Quái:** AI chạy trên server trong `WorldLoop`. MVP: đi tuần trên bệ, đuổi người chơi trên cùng bệ, đánh khi tới tầm.
- **Thành** dùng chia khu sẵn có của NetKit; **phó bản** là phòng riêng cho từng tổ (phải viết thêm).
- Tọa độ: x Unity → x của gói, y Unity → z của gói (server NetKit chỉ lưu x, z).
- Trước khi mở cho người ngoài: bật TLS.

## Build order
1. Chạy NetKit Demo với 2 client.
2. Thành online: nhân vật LPC đi lại và nhảy, đồng bộ ngoại hình, chat.
3. Server kiểm di chuyển theo hình học bệ.
4. Chiến đấu cơ bản: 1 lớp, 1 loại quái, máu, chết, hồi sinh.
5. Tổ đội và phòng phó bản, boss.
6. Đồ rơi theo độ hiếm, túi đồ, mặc đồ.
7. Đủ 3 lớp và khung cây kỹ năng.
8. Điều khiển cảm ứng cho Android.

---
**Who it's for / what they feel:** người thích cày phó bản cùng bạn bè trong phiên chơi ngắn (mỗi lần vào phó bản khoảng 10–15 phút).

**Art & audio direction:** nhân vật pixel art LPC; map tilemap pixel art (chưa chọn bộ). Âm thanh để sau MVP.

## Nhân vật và trang phục

- Nhân vật người chơi ghép từ các lớp sprite LPC (Universal LPC Spritesheet Character Generator), khung 64×64, chỉ dùng hàng hướng phải; quay trái thì lật ngang.
- Mỗi lớp là một món đồ đặt vào một ô: thân, đầu, mặt, tóc, áo, quần, giày, nón. Mỗi ô chỉ mặc một món; mặc món mới vào ô đã có thì thay món cũ, và có thể bỏ trống ô (ví dụ không đội nón).
- Mọi lớp dùng chung một khung hình nên thay đồ không làm gián đoạn animation.
- Animation theo chuyển động: đứng yên → idle, chạy trên mặt đất → run, đang bay lên → jump, đang rơi → fall. Ngưỡng tốc độ, khung và tốc độ khung nằm trong `Assets/Data/LpcAnimations.asset`.
- Cần thêm cho chiến đấu: animation slash, shoot, spellcast, hurt; thêm ô vũ khí.
- Bộ đồ mặc định: thân da sáng, đầu nam, mặt Neutral, tóc Plain nâu, áo thun đỏ, quần navy, giày nâu, nón da nâu.
- Đồ thay thế: tóc Buzzcut đen, tóc Curly short vàng; áo Longsleeve 2 xanh, áo Sleeveless 2 trắng; quần Shorts nâu, quần Long Pants xám; ủng Revised Boots đen; nón Tricorne nâu, nón Bonnie xanh lá; nón có thể bỏ trống.
- Tài nguyên: Tools > LPC > Build Parts cắt mỗi sheet xuống chỉ còn các khung hướng phải mà `LpcAnimations` dùng (cắt sát vùng có pixel) rồi gói mọi khung vào một sprite atlas; bản build chỉ chứa atlas. Việc cắt là một chiều: muốn dùng thêm khung hay hướng khác thì chép lại sheet gốc từ bản export của generator.
- Giấy phép: chỉ dùng món có OGA-BY 3.0 hoặc CC0. Credits nằm ở `Assets/Art/Characters/Lpc/CREDITS.txt` và phải hiện trong game trước khi phát hành.

**Reference game:** MapleStory — chỉ giữ cách nhìn ngang, thành chung và phó bản tổ đội.
