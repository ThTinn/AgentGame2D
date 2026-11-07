using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Items;

namespace Game.UI
{
    public class T_Eng : Lang
    {
        public static void Load()
        {
            GoBack = "Go Back";
            Revive = "Revive";
            Wait = "Wait";
            Create = "Create";
            TheWholeMap = "The whole map";
            SummonDragon = "Summon";
            DelRms = "ReOpen";
            ThongBao = "Notification";
            MayChuBaoTri = "Server is under Maintenance. Please try again later!";
            CheckData = "Check Data!";
            CheckAsset = "Check Asset!";
            BackToLogin = "Back to login";

            Connecting = "Connecting";
            Disconnect = "Disconnect";
            Gioi_han = "Limit";
            Yes = "Yes";
            No = "No";
            Ok = "Ok";
            Close = "Close";
            Done = "Done";
            Thanh_cong = "Successfully!";
            That_bai = "failed!";
            Chon = "Sellect";
            Xin_cho = "Waiting";

            #region Mail Box
            Tu = "From";
            Nhan = "Receive";
            so_luong = "Amount";
            Hop_Thu = "MailBox";
            Doc_tat_ca = "Read all";
            Chon_tat_ca = "Sellect all";
            Nhan_tat_ca = "Claim all";
            #endregion

            #region Inventory + Shop
            Hanh_trang = "Inventory";
            Cua_hang = "Shop";
            Thay_the = "Replace";
            Mua = "Buy";
            Ban = "Sell";
            Nang_cap = "Upgrade";
            Mac = "Equip";
            Thao = "Unequip";
            Thuoc_tinh_se_mat = "The properties will be disabled";
            Thuoc_tinh = "Attribute";//Thuộc tính
            Yeu_cau = "Require";
            Cap = "Level";
            Cap_ke_tiep = "Next level";//Cấp kế tiếp
            Han_su_dung = "Exp date";//Hạn sử dụng
            nam = "Y";
            thang = "M";
            tuan = "w";
            ngay = "d";//ngày
            gio = "h";//giờ
            phut = "m";//phút
            giay = "s";
            Het_han = "Expired";
            Ban_muon_mo_rong_hanh_trang = "Do you want to expand your inventory?";
            Chi_so = "Parameters";
            Xac_nhan_nang_cap_trang_bi = "Confirm to upgrade item!";
            Thieu_da_bao_ve = "Without protection stone, item may drop level!!";
            Ban_muon_dung_loai_tien_nao_de_mua = "What currency would you like to use to buy";
            Hay_go_vat_pham_trong_o_nang_cap = "Please remove the item in the upgrade slot first!";
            Nhap_gia_bat_dau = "Enter starting price";
            Nhap_gia_mua_ngay = "Enter buy now price";
            Dau_gia = "Auction";
            Nhap_so_luong = "Enter amount";
            Nhap_gia = "Enter price";
            Mua_ngay = "Buy now";
            Chu_so_huu = "Owner";
            Gia_hien_tai = "Current price";
            Gia_mua_ngay = "Buy now price";
            Gia_khong_hop_le = "Invalid price";
            Xac_nhan_dau_gia = "Confirm auction";
            So_luong_khong_hop_le = "Invalid quantity";
            Gia_bat_dau_khong_hop_le = "Invalid starting price";
            Gia_mua_ngay_khong_hop_le = "Invalid buy now price";
            Gia_bat_dau = "Starting price";
            Xac_nhan_huy_theo_doi = "Confirm unfollow";
            Xac_nhan_mua_ngay = "Confirm buy now";
            Chua_kham = "Empty";//Rỗng
            Kham_ngoc = "Socket";
            Go_ngoc = "Unsocket";
            Xac_nhan_go_ngoc = "Confirm empty this socket !";
            Xac_nhan_duc_lo = "Confirm to create socket !";
            Da_trang_bi = "Equipped";

            #endregion;

            Dang_nhap = "Sign in";
            Dang_ki = "Sign up";
            Xac_nhan_dang_xuat = "Comfirm to log out";
            Thong_tin = "Infomation";
            Dang_nhap_bang_Apple = "Sign in with Apple";
            Toi_dong_y_voi_thoa_thuan = "I agree \"User Agreement\" and \"Privacy\"";
            Dang_nhap_bang_Google = "Sign in with Google";
            Mat_khau = "Password";
            Tai_khoan = "Account";
            Ten_dang_nhap = "Username";
            Phong_thu = "Defend";
            Tan_cong = "Attack";
            Email_khong_hop_le = "The email is invalid";
            Hay_nhap_dung_dinh_dang_email= "Please enter correct email format. eg: abc@gmail.com";
            Hay_nhap_ten_dang_nhap = "Please enter the user name!";
            Hay_nhap_ma_code = "Please enter the code, code must be 6 character in your mailbox!";
            Hay_kiem_tra_lai_mat_khau = "Please check your password, the passwords do not match.";
            Hay_nhap_mat_khau = "Please enter the password";
            Quay_lai="Back";
            Nhap_lai_mat_khau = "re-enter the password";
            Goi_code = "Send code";
            Lien_ket = "Link";
            Nhat = "Collect";
            Do_hiem = "Rare";
            Vat_pham = "Item";
            Ke_dich = "Monsters";
            Pham_vi = "Range";

            Toc = "Hair";//Tóc

            #region Mission + Function panel
            Nhiemvu = "Quest";//Nhiệm vụ
            HienNhacNho = "Show reminder";//Hiện nhắc nhở
            //Phanthuong = "Reward";//Phần thưởng
            Nhan_Thuong = "Received";//Đã nhận
            Tiendo = "Progress";//Tiến độ
            Dahoanthanh = "Completed";//Hoàn thành
            Chuong = "Chapter";

            CHUCNANG = "FUNTION";//CHỨC NĂNG
            CHUNG = "GENERAL";//CHUNG
            KETGIAO = "COMMUNITY";//KẾT GIAO - cộng đồng
            HOATDONG = "ACTIVITY";//HOẠT ĐỘNG
            DaNhan = "Claimed";//Đã Nhận
            HOANTHANHNHIEMVU = "QUEST COMPLETED";//HOÀN THÀNH NHIỆM VỤ
            Hiennhacnho = "Show reminder";//Hiện nhắc nhở
            Tin_nhan = "Messages";
            Cai_dat = "Setting";
            Tu_dong = "Auto";
            Ban_be = "Friend";
            Tat_nhac_nho_nhiem_vu = "Turn off task reminders. You can go to the main interface of the task function to turn it back on.";
            Lien_he_gan_day = "Recent contact";//Liên hệ gần dây
            Chan = "Block";//Chặn
            Tuong_tac = "Interact";//Tương tác
            #endregion
            Suc_danh = "Damage";
            Hoi_chieu = "Duration";
            Diem_tiem_nang = "Potential points";
            Kich_hoat = "Active";
            TANG_CAP_KY_NANG = "UPGRADE SKILL";
            Dung_diem_tiem_nang = "Use potential points to upgrade attributes";
            hien_tai = "current";
            Tong_cap_hien_tai = "Total current level";
            Tong_cap_hien_tai_DESC = "The total level of the attributes cannot exceed the maximum limit";
            TAO_NHAN_VAT = "CREATE CHARACTER";
            Ten_nhan_vat = "Character name";
            Tao_nhan_vat = "Create character";

            Tro_chuyen = "Messages";
            The_gioi = "World";
            Bang_hoi = "Clan";
            To_doi = "Party";
            Lan_can = "Nearby";
            Chat_rieng = "Private chat";
            Nhap_tin_nhan = "Enter a message...";
            Nguoi_nhan = "Receiver";
            Huy_bo = "Cancel";
            Tao_phong = "Create";
            Nhap_ten_hoac_ID = "Enter a name or ID...";
            Them_vao_ds_den = "Add to blacklist";
            Cac_nhan_vat_trong_ds_den = "Blacklisted characters will not be able to chat with you privately. You also don't see their content on public channels";
            se_duoc_them_vao_ds_den = "will be added to the blacklist.";
            se_duoc_xoa_khoi_ds_den = "will be removed from the blacklist.";
            Xac_nhan = "Confirm";

            Tao_bang = "Create Clan";
            Tim_bang = "Search Clan";
            Doi_ten = "Change name";
            Hay_nhap_ten_moi_cho_bang_hoi = "Please enter a new name for the clan";
            Hay_nhap_ten_bang = "Please enter name of the clan";
            Xac_nhan_tao_bang = "Confirm to create clan";
            Nhap_ten_bang = "Enter name of the clan";
            Nhap_mo_ta = "Enter description";
            Thanh_vien_s = "Members";
            Yeu_cau2 = "Requests";
            Roi_khoi = "Leave";
            Gia_nhap = "Join";
            Lanh_dia = "Area";
            truoc = "ago";
            truc_tuyen = "online";
            Khong_co_ket_qua = "No result";
            Nhan_de_xem_chi_tiet = "Tap to view detail";
            Doi_huy_hieu = "Change the badge";
            Xac_nhan_cap_nhat_bang = "Confirm to edit clan";
            Xac_nhan_giai_tan_bang = "Confirm to disband clan";
            Nhap_cap_toi_thieu = "Enter min level";
            Bao_mat = "Sercurity";
            Chung_toc = "Race";
            Cap_yeu_cau = "Level require";
            Huy_hieu = "Badge";
            Mo = "Open";
            Trai_dat = "Earth";
            Giai_tan = "Disband";
            Nhuong_chuc = "Cede";
            Phong_pho_bang = "Promoted Elder";
            Giang_chuc = "Demoted";
            Gan = "Assign";
            Duoi = "Kick out";
            Tim_ban = "Search Friend";
            Danh_sach_yeu_cau = "Request list";

            moi_ban_gia_nhap_bang = "invite you to join clan";
            Xac_nhan_gia_nhap_bang = "Confirm to join this clan!";
            Xac_nhan_roi_bang= "Confirm to leave clan!";
            Ban_duoc_gia_nhap_bang_hoi = "You are joined the guild!";
            Xac_nhan_mua_mau_voi_gia = "Confirmed to buy this color for ";
            Hay_chon_huy_hieu = "Please choose badge!";

            Bat_dau = "Start";
            Thua_cuoc = "Defeated";
            Chien_thang = "Victory";
            Cham_khoang_trong_de_dong = "Tap any where to close";
            Su_kien = "Events";
            Bo_qua_xac_nhan = "Skip confirm";
            Xac_nhan_khoi_dong_lai_su_kien = "Confirm to restart this event, current progress will be lost";
            Xac_nhan_mo_o_voi_gia = "Confirm to open slot by";
            Goi_rong = "Summon dragon";
            Suc_manh = "Power";
            Xoa = "Delete";
            Dich_chuyen = "Teleport";
            Khong_co_noi_dung = "No content";
            Toi_da = "Max";
            Cao="High";
            Trung="Normal";
            Thap="Low";
            Bat="On";
            Tat="Off";
            Nhac_nen = "Music";
            Am_thanh = "Sound";
            Che_do_FPS_cao = "high FPS";
            Chat_luong_ban_do = "Map's quality";
            Nhap_Giftcode = "Enter Giftcode";
            content18 = "Playing more than 180 minutes a day will adversely affect health";
            Nhap_ten_hien_thi = "Enter the display name";
            Ten_bang_hoi = "The clan's name";
            Ten_hien_thi = "Display name";
            Mo_ta = "Descriptions";
            intros = new string[][]
             {
                new string[]
                { "Once in the forest, Mr. Gohan discovered a spaceship.",
                   "Inside the ship was only a child. That was #.",
                   "The child was raised and educated by Mr. Gohan for several years, until he passed away. From then on, the child had to fend for himself, alone…"
                },
            };
            tutorials = new string[][]
    {
                new string[]
                {
                    "Use the joystick to move your character.",
                    "Hide your skill panel.",
                    "Show your skill panel.",
                    "Switch to another skill page.",
                    "Open the main menu.",
                    "Open your inventory.",
                    "Select an item.",
                    "Equip the selected item.",
                    "Close your inventory.",
                    "View your current quest.",
                    "Navigate to the quest location."
                }
                };

            isLoadLang = true;
        }
    }
}
