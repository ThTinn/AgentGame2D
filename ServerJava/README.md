# ServerJava

Server viết bằng Java 11 trở lên, chép từ GameB và chỉnh cho game 2D đi ngang (DungeonOnline). Đọc thư mục `data/`. `GameServer` lưu tài khoản, hồ sơ và bảng Core ở MySQL 8 trở lên, driver ở `lib/` (`mysql-connector-j`, giấy phép GPLv2 kèm Universal FOSS Exception); chưa có MySQL thì chạy tạm trong RAM (xem dưới). `DataServer` không cần database.

| Lớp | Cổng mặc định | Làm gì | Chạy |
|---|---|---|---|
| `DataServer` | 18888 | Chỉ tải dữ liệu: `CLIENT_INFO`, `UPDATE_DATA`, `REQUEST_DATA`, `PING` | `run.bat`, `run.sh` |
| `GameServer` | 18889 | Server game, gồm cả phần tải dữ liệu qua lớp kế thừa `DataConnection` | `run-game.bat`, `run-game.sh` |

Tham số của `run-game`: thư mục dữ liệu (mặc định `data`), cổng, file cấu hình database (mặc định `db.properties`), số bot (mặc định 6, 0 là không có bot).

Client (Unity, `Assets/Scripts/Net`): panel Loading chọn server trong `Assets/Data/ServerList.asset` (mặc định `127.0.0.1:18889`) hoặc gõ IP, tải dữ liệu, rồi panel Login gửi `LOGIN`. Cả hai panel nằm trong scene `Main`.

## Dữ liệu và map 2D

```
data/
  manifest.csv            platform,type,id,version,size,file; dòng bắt đầu bằng # là chú thích
  templates/maps.json     template loại 5: các map 2D, client tải về để dựng map, server đọc cùng file
  core/world.json         map xuất phát, thông số chiến đấu, NPC
  core/start_profile.json hồ sơ của tài khoản mới
```

- Mỗi map trong `maps.json`: `id`, `name` ("vi|en"), `xMin` / `xMax` (khoảng đi được, bot không ra ngoài), `fallLimitY`, `spawn`, `solids` (hộp va chạm: tâm `x`, `y`, kích thước `w`, `h`, `visible` false là tường ẩn), `camps` (bãi quái: tâm `x`, `y`, nửa bề ngang `range`, máu, sát thương, cấp). Đơn vị mét.
- Sửa `maps.json` thì tăng cột version của dòng template 5 trong `manifest.csv` (và cột size), không thì client đã có bản cũ sẽ không tải lại. Khởi động lại server.
- Quy ước vị trí trên map 2D: gói vẫn ghi 3 số nguyên x100 (x, y, z). **x là trục ngang, z là trục dọc của map, y không dùng** (luôn `World.GROUND_Y`). Nhờ vậy khoảng cách (x, z) của chiến đấu, quái, nhiệm vụ giữ nguyên công thức. Client đổi ở `ServerCodec`.
- Quái và bot chỉ đi ngang, giữ độ cao của bãi / chỗ xuất hiện. Server không mô phỏng va chạm: bot có thể đi qua hố giữa hai nền.
- `world.json` thay `world.bin` / `mapobjects.bin` cũ của GameB (số chiến đấu lấy nguyên từ `world.bin`). Chưa có ghế, cổng (`placements` rỗng) và NPC.

## Chạy không MySQL (tạm)

Không có file `db.properties` thì `GameServer` in `chay KHONG MySQL` và dùng `AccountStore` trong RAM: tên chưa có thì tạo tài khoản ở lần đăng nhập đầu, mật khẩu vẫn kiểm, hồ sơ và vị trí giữ tới khi tắt server. Không có bảng `tpl_*` nên chưa có chiến đấu. Chỉ để chạy thử luồng Loading → Login → vào map; có người chơi thật thì phải cài MySQL.

## Cấu trúc code

Mỗi chức năng của game là một package trong `src/game`, chỉ phần module khác dùng mới để `public`.

| Package | Làm gì | Lớp chính |
|---|---|---|
| `game` | Ghép các module: khởi động, chia lệnh, nhịp thế giới | `GameServer`, `GameConnection`, `WorldLoop` |
| `game.net` | Khung gói, mã lệnh chính | `Cmd`, `Packet`, `PacketConnection` |
| `game.data` | Tải dữ liệu cho client, không cần database | `DataServer`, `DataConnection` |
| `game.db` | MySQL, JSON, đẩy bảng Core | `Database`, `AccountStore`, `TemplateStore`, `TemplateImport`, `Json` |
| `game.tables` | Bảng luật `tpl_*`, chỉ đọc sau khi khởi động | `Tables`, `TableLoader` |
| `game.account` | Đăng nhập, tạo tài khoản, giới hạn theo IP | `Login`, `LoginGuard`, `Accounts`, `Account` |
| `game.world` | Trạng thái chung, map, khu, bàn ghế, cổng, di chuyển | `World`, `WorldLoader`, `GameMap`, `Zones`, `MapObjects`, `Movement` |
| `game.player` | Người chơi online, hồ sơ, gói mô tả người chơi | `Player`, `Profile`, `PlayerPackets` |
| `game.chat` | Chat map, thế giới, riêng | `Chat` |
| `game.bot` | Bot | `Bots` |
| `game.combat` | Tung đòn, đòn đang bay, người chơi trúng đòn, chết, hồi sinh | `Casting`, `Hits`, `Targeting`, `Life`, `CombatConfig` |
| `game.mob` | Quái | `Mob`, `MobAI` |
| `game.loot` | Đồ rơi trên đất | `Loot` |
| `game.item` | Túi đồ, đồ mặc, bình và thuốc, cường hóa, khảm đá, lệnh `ITEM` | `Inventory`, `Equipment`, `Consumables`, `GearForge`, `ItemStack`, `ItemCommands` |
| `game.build` | Skill trên nút, nâng cấp vũ khí, cây nhân vật, chỉ số, lệnh `BUILD` | `Build`, `Stats`, `WeaponStats`, `BuildCommands` |
| `game.story` | Thân thiện, nhiệm vụ, lệnh `NPC`, `QUEST` | `Story`, `QuestLog`, `NpcCommands`, `QuestCommands` |

Thêm lệnh mới: khai mã ở `Cmd`, viết xử lý ở module của nó, nối vào `GameConnection.game`. Trạng thái thế giới chỉ đụng tới khi giữ `World.LOCK`.

Quy ước code: mỗi hàm có comment tiếng Việt nói làm gì, điều kiện từ chối và giá trị trả về; hàm dài thì tách theo từng bước (ví dụ `MobAI.tickWander` / `tickChase` / `tickReturn`, `TableLoader.load*` mỗi bảng một hàm). Dòng tối đa 140 ký tự. Tầng `game.db` không gọi ngược lên luật: hồ sơ mới truyền vào `AccountStore.create` bằng hàm.

Biên dịch dùng `-sourcepath src` từ lớp điểm vào, javac tự tìm các lớp phụ thuộc. Cần một JDK từ 11 trở lên, không cần build tool.

## Cài MySQL

Tạo database và user riêng. Server tự tạo bảng ở lần chạy đầu.

```sql
CREATE DATABASE gameb CHARACTER SET utf8mb4;
CREATE USER 'gameb'@'localhost' IDENTIFIED BY '<mật_khẩu>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON gameb.* TO 'gameb'@'localhost';
```

Chép `db.properties.example` thành `db.properties` rồi điền mật khẩu. Đặt `bind-address = 127.0.0.1` và không mở cổng 3306 ra Internet: chỉ `GameServer` nói chuyện với MySQL, client không bao giờ kết nối tới đây. Sao lưu bằng `mysqldump gameb`.

Dữ liệu người chơi nằm ở `accounts` (tài khoản, salt, hash) và `players` (hồ sơ JSON, map và vị trí cuối). Đồ sở hữu, nâng cấp, cấp, vàng nằm trong chuỗi JSON của `players.profile`, định dạng `PlayerData` của client. Bundle và 3 template cũ (anim, map, quái) vẫn là file trong `ServerData`.

## Bảng Core trong MySQL

Vật phẩm, skill, vũ khí, option, nhiệm vụ, NPC, đồ rơi, cây nâng cấp, cường hóa và object map nằm ở các table `tpl_*`. Mỗi phần tử một hàng, trường đơn là cột, trường lồng là cột JSON. Trường đơn của cả bảng (ví dụ `luckyBonus`) nằm ở `tpl_settings`, phiên bản ở `tpl_meta`.

Quy định chỉ số vật phẩm: mọi chỉ số là một hàng ở `tpl_item_options` (`item_id`, `option_id`, `value`), khóa ngoại trỏ tới `tpl_options`. Tên, cách ghi số và trần lấy từ option, vật phẩm chỉ giữ giá trị. Option chưa khai thì MySQL từ chối.

Vũ khí theo cùng quy định: sát thương, hồi, tầm, mục tiêu gốc của đòn đánh thường là option 20-23 của item vũ khí. `levels` của skill loại Basic chỉ ghi phần tăng thêm theo cấp, sát thương đòn = option + phần tăng (`Casting.castOf`). `effects` trong `tpl_weapons.upgrades` cũng dùng id option.

Xem chỉ số kèm tên:

```sql
SELECT i.name, o.name AS option_key, io.value
FROM tpl_item_options io
JOIN tpl_items i ON i.id = io.item_id
JOIN tpl_options o ON o.id = io.option_id
ORDER BY i.id, io.row_no;
```

- Nguồn dữ liệu là Excel và asset Unity. Đổi xong chạy `Tools/Core/Push Templates To MySQL` trong Unity, rồi khởi động lại `GameServer`. Menu gọi `push-templates.bat`, chỉ cần `db.properties`, user không cần quyền DROP.
- Sửa tay trong MySQL có tác dụng để thử, nhưng lần đẩy sau sẽ ghi đè. Sửa tay thì tăng `tpl_meta.version`, không thì client đã có bản cũ sẽ không tải lại.
- `GameServer` đọc các table lúc khởi động, đóng gói thành template loại 3 và gửi qua `UPDATE_DATA`. Client lưu bản đó, chỉ tải lại khi phiên bản đổi, rồi ghi đè theo khóa lên bản sao của bảng trong build (`CoreTemplates`). Icon, prefab không nằm trong MySQL nên món có hình mới vẫn phải kèm asset.
- `GameServer` dùng các table này làm luật (`game.tables`): chỉ số vật phẩm, vũ khí, skill, cây nhân vật, cường hóa, đồ rơi, NPC, nhiệm vụ. Bàn ghế, cổng, bãi quái và thông số đánh (`baseDamage`, tốc đạn...) của server vẫn đọc `mapobjects.bin`, `world.bin` nên menu xuất lại chúng cùng lúc.
- Chưa đẩy lần nào thì client dùng số liệu trong bản build, còn server không có chiến đấu.

## Khu

Mỗi map chia nhiều khu, ở khác khu thì không thấy nhau và không chat map được với nhau. Mỗi khu có quái và ghế riêng.

- Tối đa 50 người một khu, tính cả bot (`ZONE_PLAYERS`).
- Vào map thì vào khu số nhỏ nhất còn chỗ. Khu không được lưu: đăng nhập lại server xếp lại từ đầu.
- Mỗi map luôn có 5 khu (`ZONE_MIN`). Mọi khu đều đầy thì tự mở khu tiếp theo, khu thừa hết người thì danh sách giảm về 5. Người đang ở khu cao không bị dồn xuống.
- Người chơi đổi khu ở mốc chuyển khu của map (`ZoneBoard` ở client). Đổi khu giữ nguyên chỗ đứng, khu đầy thì báo toast.

## Bot

`GameServer` tự tạo bot lúc khởi động và tự điều khiển, client chỉ thấy chúng như người chơi khác. Cách đi giống `reference/LocalServer/ServerBots.cs`.

- Bot tên `Bot001`, `Bot002`... có tài khoản và hồ sơ trong MySQL như người thật, chưa có thì server tự tạo, có rồi thì dùng lại. Tăng số bot rồi khởi động lại là có thêm.
- Bot số lẻ (`Bot001`, `Bot003`...) ở map xuất hiện, bot số chẵn rải đều các map còn lại. Mỗi lần khởi động bot về gần chỗ xuất hiện, không lưu vị trí.
- Bot đi dạo và thỉnh thoảng chat, chỉ khi map có người thật: không ai xem thì đứng yên.
- Tên dạng `bot` + số dành riêng cho bot, không ai đăng nhập hay tạo tài khoản bằng tên đó được.
- Bot chưa đánh quái, quái cũng không đuổi bot.

## GameServer đã có

- Tài khoản: tên chưa có thì tạo ở lần đăng nhập đầu, mật khẩu băm PBKDF2. Tên không phân biệt hoa thường nhưng phân biệt dấu. Sai 5 lần trên một kết nối thì ngắt. Đăng nhập chỗ khác hoặc đăng nhập lại trên cùng kết nối thì đóng phiên cũ, lưu rồi mới đọc hồ sơ từ MySQL (`Login.takeOver`). MySQL lỗi thì báo máy chủ bận, không ngắt kết nối.
- Chống chiếm server (`LoginGuard`, theo IP, 127.0.0.1 không tính): 20 kết nối cùng lúc, 30 lần đăng nhập mỗi phút, 10 tài khoản mới mỗi giờ. Kết nối im lặng 60 giây thì ngắt, đang chơi là 120 giây; client ping mỗi giây ngay từ lúc kết nối. Quán net dùng chung một IP thì nới các hằng số ở `LoginGuard`.
- Tắt server bằng Ctrl+C, đóng cửa sổ hoặc `kill` thì lưu mọi người và chờ MySQL ghi xong. `kill -9`, End Task, mất điện thì mất phần chưa tới lần lưu 30 giây.
- Hồ sơ tạo từ `core/start_profile.json` cùng lúc với tài khoản. Vào lại đứng ở map và vị trí lúc thoát.
- Vào game, vào map, thấy người chơi khác (kèm tư thế, cấp, máu), `MAP READY` (NPC, bàn ghế, cổng).
- Di chuyển, chat map / thế giới / riêng, `PLAYER_INFO`, đổi map, qua cổng, ngồi ghế, chia khu và đổi khu.
- Chiến đấu (`game.combat`): đòn đánh thường theo vũ khí và skill ô 1-4, hồi chiêu, mana, chí mạng, mọi nâng cấp vũ khí. Client không còn bản C# của luật này (chế độ chơi local đã bỏ). Mỗi đòn chờ `hitDelay` của skill (cột `tpl_skills.hitDelay`, khung vung tay đo từ clip; 0 thì theo `hitDelay` chung của `world.bin`) rồi đạn mới rời tay / cận chiến, vùng mới trúng, cùng mốc client hiện hiệu ứng. Đo lại khung vung tay thì chạy `Push Templates To MySQL` và khởi động lại `GameServer`.
- Quái: đi dạo, đuổi, đánh, về bãi, chết, hồi sinh. Chỉ chạy khi khu có người thật.
- Người chơi: trúng đòn, chết, hồi sinh, hồi máu theo chỉ số, hồi mana.
- Thưởng khi hạ quái: EXP, vàng, đồ rơi xuống đất theo `tpl_drops`.
- Hồ sơ (`game.player`, `game.item`, `game.build`): túi đồ, đồ mặc, ô dùng nhanh, bình và thuốc, skill trên nút, nâng cấp vũ khí, cây nhân vật, cường hóa, khoét lỗ, khảm đá. Server luôn gửi `PROFILE.SYNC` ghi đè bản client đoán trước, ghi `players.profile` mỗi 30 giây và lúc thoát.

- Cốt truyện (`game.story`): thân thiện, nhiệm vụ, cờ, câu đáp, kết cục, kẻ thù, quà tặng, giá NPC theo thân thiện, việc hằng ngày, theo dõi việc. Luật chung với `QuestSystem.cs` của client. Mục tiêu hạ quái server tự đếm, mục tiêu tới nơi server tính theo `MOVE`, thông báo tiến độ gửi bằng `NOTIFY.TOAST`.

Chỉ số người chơi là mảng theo id trong `tpl_options`, cộng từ cây, Chuyên tinh, đồ, đá khảm, thuốc. Thêm option mới chỉ cần sửa `Tables` (hằng số) và chỗ đọc chỉ số đó.

## Chưa có

- Nâng cấp skill: chưa có lệnh, cấp skill giữ nguyên như trong hồ sơ.
- Tổ đội, bang, tư thế (`ANIM_SET`), người chơi đánh nhau.
- Kiểm tra gian lận: server tin vị trí và chỗ tung đòn client gửi.
- Mã hóa: `Session` dùng `key = {0}` nên mật khẩu đi qua mạng ở dạng đọc được. Phải có TLS hoặc mã hóa trước khi dùng tài khoản thật trên Internet.

Khi thêm phần mới, đối chiếu định dạng gói ở `Cmd_Message.cs` và luật ở `reference/LocalServer` (server giả lập cũ của client, chỉ để đọc, không biên dịch).
