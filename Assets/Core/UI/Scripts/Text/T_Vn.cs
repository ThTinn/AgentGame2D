using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Items;

namespace Game.UI
{
    public class T_Vn : Lang
    {
        public static void Load()
        {
            GoBack = "Trở về";
            Revive = "Sống lại";
            Wait = "Chờ";
            Create = "Tạo";
            TheWholeMap = "Toàn bản đồ";
            SummonDragon = "Triệu hồi";
            DelRms = "Bạn có muốn xóa toàn bộ dữ liệu!";
            ThongBao = "Thông báo";
            MayChuBaoTri = "Máy chủ đang bảo trì. Vui lòng thử lại sau!";
            CheckData = "Kiểm tra dữ liệu!";
            CheckAsset = "Kiểm tra asset!";
            BackToLogin = "Trở về màn hình đăng nhập";
            Connecting = "Đang kết nối với máy chủ";
            Disconnect = "Mất kết nối với máy chủ";
            Gioi_han = "Giới hạn";
            Yes = "Yes";
            No = "No";
            Ok = "Ok";
            Close = "Đóng";
            Done = "Hoàn thành";
            Thanh_cong = "Thành công!";
            That_bai = "Thất bại!";
            Chon = "Chọn";
            Xin_cho = "Xin chờ";
            #region Mail Box
            Tu = "Từ";
            Nhan = "Nhận";
            so_luong = "Số lượng";
            Hop_Thu = "Hộp Thư";
            Doc_tat_ca = "Đọc tất cả";
            Chon_tat_ca = "Chọn tất cả";
            Nhan_tat_ca = "Nhận tất cả";
            #endregion

            #region Inventory + Shop
            Hanh_trang = "Hành trang";
            Cua_hang = "Cửa hàng";
            Thay_the = "Thay thế";
            Mua = "Mua";
            Ban = "Bán";
            Nang_cap = "Nâng cấp";
            Mac = "Mặc";
            Thao = "Tháo";
            Thuoc_tinh_se_mat = "Thuộc tính sẽ mất";
            Thuoc_tinh = "Thuộc tính";// thuộc tính
            Yeu_cau = "Yêu cầu";
            Cap = "Cấp";
            Cap_ke_tiep = "Cấp kế tiếp";//Cấp kế tiếp
            Han_su_dung = "Hạn sử dụng";
            nam = "năm";
            thang = "tháng";
            tuan = "tuần";
            ngay = "ngày";
            gio = "giờ";
            phut = "phút";
            giay = "giây";
            Het_han = "Hết hạn";
            Ban_muon_mo_rong_hanh_trang = "Bạn muốn mở rộng hành trang?";
            Chi_so = "Chỉ số";
            Xac_nhan_nang_cap_trang_bi = "Xác nhận nâng cấp trang bị !";
            Thieu_da_bao_ve = "Thiếu đá bảo vệ, vật phẩm có thể bị rớt cấp độ !!";
            Ban_muon_dung_loai_tien_nao_de_mua = "Bạn muốn sử dụng loại tiền tệ nào để mua ";
            Hay_go_vat_pham_trong_o_nang_cap = "Hãy gỡ vật phẩm trong ô nâng cấp ra trước!";
            Nhap_gia_bat_dau = "Nhập giá bắt đầu";
            Nhap_gia_mua_ngay = "Nhập giá mua ngay";
            Dau_gia = "Đấu giá";
            Nhap_so_luong = "Nhập số lượng";
            Nhap_gia = "Nhập giá";
            Mua_ngay = "Mua ngay";
            Chu_so_huu = "Chủ sở hữu";
            Gia_hien_tai = "Giá hiện tại";
            Gia_mua_ngay = "Giá mua ngay";
            Gia_khong_hop_le = "Giá không hợp lệ";
            Xac_nhan_dau_gia = "Xác nhận đấu giá";
            So_luong_khong_hop_le = "Số lượng không hợp lệ";
            Gia_bat_dau_khong_hop_le = "Giá bắt đầu không hợp lệ";
            Gia_mua_ngay_khong_hop_le = "Giá mua ngay không hợp lệ";
            Gia_bat_dau = "Giá bắt đầu";
            Xac_nhan_huy_theo_doi = "Xác nhận hủy theo dõi";
            Xac_nhan_mua_ngay = "Xác nhận mua ngay";
            Chua_kham = "Chưa khảm";
            Kham_ngoc = "Khảm ngọc";
            Go_ngoc = "Gỡ ngọc";
            Xac_nhan_go_ngoc = "Xác nhận gỡ ngọc!";
            Xac_nhan_duc_lo = "Xác nhận đục lỗ item !";
            Da_trang_bi = "Đã trang bị";
            #endregion;

            Ten_dang_nhap = "Tên đăng nhập";
            Tai_khoan = "Tài khoản";
            Mat_khau = "Mật khẩu";
            Thong_tin = "Thông tin";
            Dang_nhap_bang_Apple = "Đăng nhập bằng Apple";
            Toi_dong_y_voi_thoa_thuan = "Tôi đồng ý với \"Thỏa thuận người dùng\" và \"Quyền riêng tư\"";
            Dang_nhap_bang_Google = "Đăng nhập bằng Google";
            Xac_nhan_dang_xuat = "xác nhận đăng xuất";
            Dang_nhap = "Đăng nhập";
            Dang_ki = "Đăng kí";
            Phong_thu = "Phòng thủ";
            Tan_cong = "Tấn công";
            Hay_nhap_dung_dinh_dang_email = "Vui lòng nhập đúng định dạng email. ví dụ: abc@gmail.com";
            Hay_nhap_ten_dang_nhap = "Hãy nhập tên người dùng!";
            Hay_nhap_ma_code = "Hãy nhập mã code, mã phải có 6 ký tự trong hộp thư của bạn!!";
            Email_khong_hop_le = "Email không hợp lệ";
            Hay_kiem_tra_lai_mat_khau = "Hãy kiểm tra lại mật khẩu, mật khẩu không trùng nhau";
            Hay_nhap_mat_khau = "Hãy nhập mật khẩu!";
            Quay_lai = "Quay lại";
            Nhap_lai_mat_khau = "Nhập lại mật khẩu";
            Goi_code = "Gởi code";
            Lien_ket = "Liên kết";
            Nhat = "Nhặt";
            Do_hiem = "Độ hiếm";
            Vat_pham = "Vật phẩm";
            Ke_dich = "Kẻ địch";
            Pham_vi = "Phạm vi";
            Toc = "Tóc";
            #region Mission
            Nhiemvu = "Nhiệm vụ";//Nhiệm vụ
            HienNhacNho = "Hiện nhắc nhở";//Hiện nhắc nhở
            Nhan_Thuong = "Nhận Thưởng";//Nhận Thưởng
            Tiendo = "Tiến độ";//Tiến độ
            Dahoanthanh = "Đã hoàn thành";
            Chuong = "Chương";

            CHUCNANG = "CHỨC NĂNG";//CHỨC NĂNG
            CHUNG = "CHUNG";//CHUNG
            KETGIAO = "KẾT GIAO";//KẾT GIAO
            HOATDONG = "HOẠT ĐỘNG";//HOẠT ĐỘNG
            DaNhan = "Đã nhận";//Đã Nhận
            HOANTHANHNHIEMVU = "HOÀN THÀNH NHIỆM VỤ";//HOÀN THÀNH NHIỆM VỤ
            Hiennhacnho = "Hiện nhắc nhở";//Hiện nhắc nhở
            Tin_nhan = "Tin nhắn";
            Cai_dat = "Cài đặt";
            Tu_dong = "Tự động";
            Ban_be = "Bạn bè";
            Tat_nhac_nho_nhiem_vu = "Tắt nhắc nhở nhiệm vụ. Có thể vào giao diện chính của chức năng nhiệm vụ để bật lại.";
            Lien_he_gan_day = "Liên hệ gần đây";
            Chan = "Chặn";//Chặn
            Tuong_tac = "Tương tác";//Tương tác
            #endregion

            Suc_danh = "Sức đánh";
            Hoi_chieu = "Hồi chiêu";
            Diem_tiem_nang = "Điểm tiềm năng";
            Kich_hoat = "Kích hoạt";
            TANG_CAP_KY_NANG = "TĂNG CẤP KỸ NĂNG";
            Dung_diem_tiem_nang = "Dùng điểm tiềm năng để nâng cấp thuộc tính";
            hien_tai = "hiện tại";
            Tong_cap_hien_tai = "Tổng cấp hiện tại";
            Tong_cap_hien_tai_DESC = "Tổng cấp của các thuộc tính không được vượt quá giới hạn tối đa";

            TAO_NHAN_VAT = "TẠO NHÂN VẬT";
            Ten_nhan_vat = "Tên nhân vật";
            Tao_nhan_vat = "Tạo nhân vật";

            Tro_chuyen = "Trò chuyện";
            The_gioi = "Thế giới";
            Bang_hoi = "Bang hội";
            To_doi = "Tổ đội";
            Lan_can = "Lân cận";
            Chat_rieng = "Chat riêng";
            Nhap_tin_nhan = "Nhập tin nhắn...";
            Nguoi_nhan = "Người nhận";
            Huy_bo = "Hủy bỏ";
            Tao_phong = "Tạo phòng";
            Nhap_ten_hoac_ID = "Nhập tên hoặc ID...";
            Them_vao_ds_den = "Thêm vào danh sách đen";
            Cac_nhan_vat_trong_ds_den = " Các nhân vật trong danh sách đen sẽ không thể trò chuyện riêng với bạn. Bạn cũng không thấy nội dung của họ trên các kênh chung.";
            se_duoc_them_vao_ds_den = "sẽ được thêm vào danh sách đen.";
            se_duoc_xoa_khoi_ds_den = "sẽ được xóa khỏi danh sách đen.";
            Xac_nhan = "Xác nhận";

            Tao_bang = "Tạo bang";
            Doi_ten = "Đổi tên";
            Tim_bang = "Tìm bang";
            Hay_nhap_ten_moi_cho_bang_hoi = "Hãy nhập tên mới cho bang hội";
            Hay_nhap_ten_bang = "Hãy nhập tên bang hội";
            Xac_nhan_tao_bang = "Xác nhận tạo bang hội";
            Nhap_ten_bang = "Nhập tên bang";
            Nhap_mo_ta = "Nhập mô tả";
            Thanh_vien_s = "Thành viên";
            Yeu_cau2 = "Yêu cầu";
            Roi_khoi = "Rời khỏi";
            Gia_nhap = "Gia nhập";
            Lanh_dia = "Lãnh địa";
            truoc = "trước";
            truc_tuyen = "trực tuyến";
            Khong_co_ket_qua = "Không có kết quả";
            Nhan_de_xem_chi_tiet = "Nhấn để xem chi tiết";
            Doi_huy_hieu = "Đổi huy hiệu";
            Xac_nhan_cap_nhat_bang = "Xác nhận cập nhật bang";
            Xac_nhan_giai_tan_bang = "Xác nhận giải tán bang";
            Nhap_cap_toi_thieu = "Nhập cấp tối thiểu";
            Bao_mat = "Bảo mật";
            Chung_toc = "Chủng tộc";
            Cap_yeu_cau = "Cấp yêu cầu";
            Huy_hieu = "Huy hiệu";
            Mo = "Mở";
            Trai_dat = "Trái đất";
            Giai_tan = "Giải tán";
            Nhuong_chuc = "Nhường chức";
            Phong_pho_bang = "Phong phó bang";
            Giang_chuc = "Giáng chức";
            Duoi = "Đuổi";
            moi_ban_gia_nhap_bang = "mời bạn gia nhập bang";
            Gan = "Gán";
            Tim_ban = "Tìm bạn";
            Danh_sach_yeu_cau = "Danh sách yêu cầu";

            Xac_nhan_gia_nhap_bang = "Xác nhận gia nhập bang hội!";
            Xac_nhan_roi_bang = "Xác nhận rời bang!";
            Ban_duoc_gia_nhap_bang_hoi = "Bạn vừa gia nhập bang hội!";
            Xac_nhan_mua_mau_voi_gia = "Xác nhận mua màu với giá ";
            Hay_chon_huy_hieu = "Hãy chọn huy hiệu!";

            Bat_dau = "Bắt đầu";
            Thua_cuoc = "Thất bại";
            Chien_thang = "Chiến thắng";
            Cham_khoang_trong_de_dong = "Chạm khoảng trống để đóng";
            Su_kien = "Sự kiện";
            Bo_qua_xac_nhan = "Bỏ qua xác nhận";
            Xac_nhan_khoi_dong_lai_su_kien = "Xác nhận khởi động lại sự kiện, mọi tiến độ trước đó sẽ bị mất";
            Xac_nhan_mo_o_voi_gia = "Xác nhận mở ô với giá";
            Goi_rong = "Gọi rồng";
            Suc_manh="Sức mạnh";
            Xoa = "Xóa";
            Dich_chuyen = "Dịch chuyển";
            Khong_co_noi_dung = "Không có nội dung";
            Toi_da = "Tối đa";
            Cao = "Cao";
            Trung = "Trung";
            Thap = "Thấp";
            Bat = "Bật";
            Tat = "Tắt";
            Nhac_nen = "Nhạc nền";
            Am_thanh = "Âm thanh";
            Che_do_FPS_cao = "Khung hình cao";
            Chat_luong_ban_do = "Chất lượng bản đồ";
            Nhap_Giftcode = "Nhập Giftcode";
            content18 = "Chơi quá 180 phút một ngày sẽ ảnh hưởng xấu đến sức khỏe";
            Nhap_ten_hien_thi = "Nhập tên hiển thị";
            Ten_bang_hoi = "Tên bang hội";
            Ten_hien_thi = "Tên hiển thị";
            Mo_ta = "Mô tả";
            intros =  new string[][]
            {
                new string[] 
                { "Trong một lần vào rừng, ông Gohan đã phát hiện ra một con tàu vũ trụ.",
                   "Bên trong con tàu không có ai ngoài một đứa bé. Đó chính là #.",
                   "Đứa trẻ được ông Gohan nuôi dưỡng và dạy dỗ trong vài năm, cho đến khi ông qua đời. Từ đó, đứa bé phải tự xoay sở để tồn tại, một mình…"
                },
            };
            tutorials = new string[][]
            {
                new string[]
                { "Khu vực joystick điều khiển nhân vật!",
                   "Ẩn chiêu thức",
                   "Hiện chiêu thức",
                   "Đổi trang chiêu thức",
                   "Mở menu",
                   "Mở Hành trang",
                   "Chọn trang bị",
                   "Mặc trang bị",
                   "Tắt Hành trang",
                   "Xem nhiệm vụ hiện tại",
                   "Chỉ đường đến địa điểm nhiệm vụ",
                },
            };
            isLoadLang = true;
        }
    }
}
