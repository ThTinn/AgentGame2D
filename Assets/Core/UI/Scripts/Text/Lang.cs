using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Game.Items;

namespace Game.UI
{
    public class Lang
    {
        public static string GoBack;
        public static string Revive;
        public static string Wait;
        public static string Create;
        public static string TheWholeMap;
        public static string SummonDragon;
        public static string DelRms;
        public static string ThongBao;
        public static string MayChuBaoTri;
        public static string CheckAsset;
        public static string CheckData;
        public static string BackToLogin;
        public static string Connecting;
        public static string Disconnect;
        public static string Gioi_han;
        public static string Yes, No, Ok, Close, Done;
        public static string HP = "HP";
        public static string KI = "KI";
        public static string Email = "Email";
        public static string Code = "Code";

        public static string Thanh_cong;
        public static string That_bai;
        public static string Chon;
        public static string Xin_cho;

        #region MailBox Txt
        public static string Tu;//Từ
        public static string Nhan;//NHẬN
        public static string so_luong;//số lượng
        public static string Hop_Thu;//Hộp Thư
        public static string Doc_tat_ca;//ĐỌC TẤT CẢ
        public static string Chon_tat_ca;//CHỌN_TẤT_cẢ
        public static string Nhan_tat_ca;//NHẬN_TẤT_CẢ
        #endregion

        #region Inventory + Shop
        public static string Hanh_trang;//Hành_trang
        public static string Cua_hang;//Cửa_hàng
        public static string Thay_the;//Thay_thế
        public static string Mua;
        public static string Ban;//Bán
        public static string Nang_cap;//Nâng_cấp
        public static string Mac;//Mặc
        public static string Thao;//Tháo
        public static string Thuoc_tinh_se_mat;//Thuộc tính sẽ mất
        public static string Thuoc_tinh;//Thuộc tính
        public static string Yeu_cau;//Yêu cầu
        public static string Cap;//Cấp
        public static string Cap_ke_tiep;//Cấp kế tiếp
        public static string Han_su_dung;//Hạn sử dụng
        public static string nam;//năm
        public static string thang;//tháng
        public static string tuan;//tuần
        public static string ngay;//ngày
        public static string gio;//giờ
        public static string phut;//phút
        public static string giay;//giây
        public static string Het_han;//hết hạn
        public static string Ban_muon_mo_rong_hanh_trang;//Bạn muốn mở rộng hành trang ?
        public static string Chi_so;
        public static string Xac_nhan_nang_cap_trang_bi;//Xác nhận nâng cấp trang bị !
        public static string Thieu_da_bao_ve;//Thiếu đá bảo vệ, vật phẩm có thể bị rớt cấp độ !!
        public static string Ban_muon_dung_loai_tien_nao_de_mua;//"Bạn muốn mua bằng loại tiền nào ?
        public static string Hay_go_vat_pham_trong_o_nang_cap;//"Hãy gỡ vật phẩm trong ô nâng cấp ra trước
        public static string Nhap_gia_bat_dau;//Nhập giá bắt đầu
        public static string Nhap_gia_mua_ngay;//Nhập giá mua ngay
        public static string Dau_gia;//Auction
        public static string Nhap_so_luong;//Nhập số lượng
        public static string Nhap_gia;//Nhập giá
        public static string Mua_ngay;//Mua ngay
        public static string Chu_so_huu;//Chủ sở hữu
        public static string Gia_hien_tai;//Giá hiện tại
        public static string Gia_mua_ngay;//Giá mua ngay
        public static string Gia_khong_hop_le;//Giá không hợp lệ
        public static string So_luong_khong_hop_le;//Số lượng không hợp lệ
        public static string Xac_nhan_dau_gia;//Xác nhận đấu giá
        public static string Gia_bat_dau_khong_hop_le;//Giá bắt đầu không hợp lệ
        public static string Gia_mua_ngay_khong_hop_le;//Giá mua ngay không hợp lệ
        public static string Gia_bat_dau;//Giá bắt đầu
        public static string Xac_nhan_huy_theo_doi;//Xác nhận hủy theo dõi vật phẩm
        public static string Xac_nhan_mua_ngay;//Xác nhận mua ngay
        public static string Chua_kham;//Chưa khảm
        public static string Kham_ngoc;//Khảm ngọc
        public static string Go_ngoc;//Gỡ ngọc
        public static string Xac_nhan_go_ngoc;//Xác nhận gỡ ngọc 
        public static string Xac_nhan_duc_lo;//Xác nhận đục lỗ item !
        public static string Da_trang_bi;//Đã trang bị

        #endregion
        public static string Thong_tin;//Thông_tin
        public static string Tai_khoan;//Tài khoản
        public static string Dang_nhap_bang_Apple;//Đăng nhập bằng Apple
        public static string Dang_nhap_bang_Google;//Đăng nhập bằng Google
        public static string Toi_dong_y_voi_thoa_thuan;//Tôi đồng ý với "Thỏa thuận người dùng" và "Quyền riêng tư"
        public static string Dang_nhap;//Đăng nhập
        public static string Dang_ki;// Đăng kí
        public static string Ten_dang_nhap;//Tên đăng nhập
        public static string Mat_khau;//Mật_khẩu
        public static string Nhap_lai_mat_khau;//Nhập lại mật khẩu
        public static string Goi_code;//Gởi code
        public static string Xac_nhan_dang_xuat;//Đăng xuất thành công
        public static string Phong_thu;//Phòng thủ
        public static string Tan_cong;//Tấn công
        public static string Email_khong_hop_le;// Email không hợp lệ
        public static string Hay_nhap_dung_dinh_dang_email;// Hãy nhập đúng định dạng email. ví dụ: abc@gmail.com
        public static string Hay_nhap_ten_dang_nhap;//Hãy nhập tên người dùng!
        public static string Hay_nhap_ma_code;//Hãy nhập mã, mã phải có 6 ký tự trong hộp thư của bạn!!
        public static string Hay_kiem_tra_lai_mat_khau;//Hãy kiểm tra lại mật khẩu, mật khẩu không trùng nhau
        public static string Hay_nhap_mat_khau;//Hãy nhập mật khẩu
        public static string Quay_lai;//Quay lại
        public static string Lien_ket;//Liên kết
        public static string Nhat;//Nhặt
        public static string Do_hiem;//Độ hiếm
        public static string Vat_pham;//Vật phẩm
        public static string Ke_dich;//Kẻ địch
        public static string Pham_vi;//Phạm vi

        public static string Toc;//Tóc
        #region Mission + Function Panel
        public static string Nhiemvu;//Nhiệm vụ
        public static string HienNhacNho;//Hiện nhắc nhở
        //public static string Phanthuong;//Phần thưởng
        public static string Nhan_Thuong;//Nhận
        public static object Tiendo;//Tiến độ
        public static string Dahoanthanh;//Đã hoàn thành
        public static object Chuong;//Chương
        public static string CHUCNANG;//CHỨC NĂNG
        public static string CHUNG;//CHUNG
        public static string KETGIAO;//KẾT GIAO
        public static string HOATDONG;//HOẠT ĐỘNG
        public static string DaNhan;//Đã Nhận
        public static string HOANTHANHNHIEMVU;//HOÀN THÀNH NHIỆM VỤ
        public static string Chinh;//Chính
        public static string Hiennhacnho;//Hiện nhắc nhở
        public static string Tin_nhan;//Tin nhắn
        public static string Cai_dat;//Cài đặt
        public static string Tu_dong;//Tự động
        public static string Ban_be;//Bạn bè
        public static string Tat_nhac_nho_nhiem_vu;//Tắt nhắc nhở nhiệm vụ. Có thể vào giao diện chính của chức năng nhiệm vụ để bật lại.
        public static string Lien_he_gan_day;//Liên hệ gần dây
        public static string Chan;//Chặn
        public static string Tuong_tac;//Tương tác

        #endregion
        #region Info
        public static string Suc_danh;//Sức đánh
        public static string Hoi_chieu;//Hồi chiêu
        public static string Diem_tiem_nang;//Điểm tiềm năng
        public static string Kich_hoat;//Kích hoạt
        public static string TANG_CAP_KY_NANG;//TĂNG CẤP KỸ NĂNG
        public static string Dung_diem_tiem_nang;//Dùng điểm tiềm năng để nâng cấp thuộc tính
        public static string hien_tai;//hiện tại
        public static string Tong_cap_hien_tai;//Tổng cấp hiện tại
        public static string Tong_cap_hien_tai_DESC;//Tổng cấp của các thuộc tính không được vượt quá giới hạn tối đa

        #endregion
        #region Create char
        public static string TAO_NHAN_VAT;//TẠO NHÂN VẬT
        public static string Ten_nhan_vat;//Tên nhân vật
        public static string Tao_nhan_vat;//Tạo nhân vật

        #endregion

        #region Clan
        public static string Tao_bang;
        public static string Tim_bang;
        public static string Doi_ten;
        public static string Hay_nhap_ten_moi_cho_bang_hoi;
        public static string Hay_nhap_ten_bang;
        public static string Xac_nhan_tao_bang;
        public static string Nhap_ten_bang;
        public static string Nhap_mo_ta;
        public static string Thanh_vien_s;
        public static string Yeu_cau2;//request
        public static string Roi_khoi;
        public static string Gia_nhap;
        public static string Lanh_dia;
        public static string truoc;
        public static string truc_tuyen;
        public static string Khong_co_ket_qua;
        public static string Nhan_de_xem_chi_tiet;
        public static string Doi_huy_hieu;
        public static string Xac_nhan_cap_nhat_bang;
        public static string Xac_nhan_giai_tan_bang;
        public static string Nhap_cap_toi_thieu;
        public static string Bao_mat;
        public static string Chung_toc;
        public static string Cap_yeu_cau;
        public static string Huy_hieu;
        public static string Mo;
        public static string Trai_dat;
        public static string Namec = "Namec";
        public static string Xayda = "Xayda";
        public static string Giai_tan;
        public static string Nhuong_chuc;
        public static string Phong_pho_bang;
        public static string Giang_chuc;
        public static string Duoi;
        public static string moi_ban_gia_nhap_bang;//invite you join clan;
        public static string Xac_nhan_gia_nhap_bang;//Confirm to join this clan
        public static string Xac_nhan_roi_bang;//= "Confirm to leave clan!";
        public static string Ban_duoc_gia_nhap_bang_hoi;//= "Bạn được gia nhập bang hội!";
        public static string Xac_nhan_mua_mau_voi_gia;//= "Xác nhận mua màu với giá ";
        public static string Hay_chon_huy_hieu;//= "Hãy chọn huy hiệu ";


        #endregion

        #region Messages
        public static string Tro_chuyen;
        public static string The_gioi;
        public static string Bang_hoi;
        public static string To_doi;//tổ đội
        public static string Lan_can;
        public static string Chat_rieng;
        public static string Nhap_tin_nhan;

        public static string Nguoi_nhan;
        public static string Huy_bo;
        public static string Tao_phong;
        public static string Nhap_ten_hoac_ID;

        public static string Them_vao_ds_den;
        public static string Cac_nhan_vat_trong_ds_den;//Các nhân vật trong danh sách đen sẽ không thể trò chuyện riêng với bạn. Bạn cũng không thấy nội dung của họ trên các kênh chung.
        public static string se_duoc_them_vao_ds_den;
        public static string se_duoc_xoa_khoi_ds_den;
        public static string Xac_nhan;
        public static string Gan;//Gán
        public static string Tim_ban;//Tim ban
        public static string Danh_sach_yeu_cau;//Danh sách yêu cầu



        #endregion
        public static string Bat_dau;//Bắt đầu
        public static string Chien_thang;//Chiến thắng
        public static string Thua_cuoc;//Thua cuộc
        public static string Cham_khoang_trong_de_dong;//Chạm khoảng trống để đóng
        public static string Su_kien;
        public static string Bo_qua_xac_nhan;
        public static string Xac_nhan_khoi_dong_lai_su_kien;
        public static string Xac_nhan_mo_o_voi_gia;
        public static string Goi_rong;
        public static string Suc_manh;
        public static string Xoa;
        public static string Dich_chuyen;
        public static string Khong_co_noi_dung;
        public static string Toi_da;
        public static string Cao;
        public static string Trung;
        public static string Thap;
        public static string Bat;
        public static string Tat;
        public static string Nhac_nen;
        public static string Am_thanh;
        public static string Che_do_FPS_cao;
        public static string Chat_luong_ban_do;
        public static string Nhap_Giftcode;
        public static string content18;
        public static string Nhap_ten_hien_thi;
        public static string Ten_bang_hoi;
        public static string Ten_hien_thi;
        public static string Mo_ta;

        public static string[][] intros;
        public static string[][] tutorials;

    

        public const string RMS_LANGUAGE = "Language";
        public static int curLange = -1;
        public const int T_VIET = 0, T_ENG = 1;
        public static string[] langeStr = new string[] { "Tiếng Việt", "English" };
        public static bool isLoadLang = false;

        public static string GiftCode = "GiftCode";

        /// <summary>Bắn sau khi đổi ngôn ngữ lúc chạy (SetLanguage). Panel / popup nghe sự kiện này để vẽ lại chữ.</summary>
        public static event Action OnChanged;

        // Nạp chuỗi trước khi scene đầu tiên chạy để không bao giờ gặp Lang.xxx == null.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            Init();
        }

        static LangTable table;

        /// <summary>Bảng chữ dựng từ Docs/Lang_Data.xlsx; null khi chưa dựng (khi đó chỉ dùng T_Vn / T_Eng và chuỗi "VN|EN").</summary>
        public static LangTable Table => table;

        /// <summary>
        /// Đọc ngôn ngữ đã lưu (mặc định Tiếng Việt) rồi nạp chuỗi. Bản 3D lưu qua RMS, đây lưu bằng PlayerPrefs.
        /// Có <see cref="LangTable"/> thì danh sách ngôn ngữ (<see cref="langeStr"/>) lấy theo bảng: thêm cột trong Excel là có thêm ngôn ngữ.
        /// </summary>
        public static void Init()
        {
            table = Resources.Load<LangTable>(LangTable.ResourcePath);
            if (table != null && table.languages.Count > 0)
            {
                langeStr = new string[table.languages.Count];
                for (int i = 0; i < langeStr.Length; i++) langeStr[i] = table.languages[i].name;
            }

            curLange = PlayerPrefs.GetInt(RMS_LANGUAGE, -1);
            if (curLange < 0 || curLange >= langeStr.Length)
            {
                curLange = T_VIET;
            }
            SwitchLang();
        }

        static void SwitchLang()
        {
            isLoadLang = false;
            // ngôn ngữ thêm trong Excel: nền là tiếng Anh, bảng đè lên phần đã dịch
            if (curLange == T_VIET) T_Vn.Load();
            else T_Eng.Load();
            ApplyTable();
            isLoadLang = true;
        }

        /// <summary>Ghi chữ của ngôn ngữ đang chọn trong bảng (sheet Lang) đè lên các field. Ô trống giữ chữ của T_Vn / T_Eng.</summary>
        static void ApplyTable()
        {
            if (table == null) return;
            foreach (LangTable.Row row in table.fields)
            {
                string value = LangTable.ValueOf(row, curLange);
                // ngôn ngữ thêm chưa dịch ô này: lấy cột English của bảng (có thể đã sửa khác T_Eng)
                if (string.IsNullOrEmpty(value) && curLange > T_ENG) value = LangTable.ValueOf(row, T_ENG);
                if (string.IsNullOrEmpty(value) || row == null) continue;
                FieldInfo field = Field(row.key);
                if (field != null) field.SetValue(null, value);
            }
        }

        /// <summary>Đổi ngôn ngữ ngay lúc chạy, lưu lại và báo cho UI đang mở (OnChanged).</summary>
        public static void SetLanguage(int lang)
        {
            if (lang < 0 || lang >= langeStr.Length || lang == curLange) return;

            curLange = lang;
            SaveLange((short)lang);
            SwitchLang();
            OnChanged?.Invoke();
        }

        public static void SaveLange(short lang)
        {
            PlayerPrefs.SetInt(RMS_LANGUAGE, lang);
            PlayerPrefs.Save();
        }

        static Dictionary<string, FieldInfo> fieldCache;

        /// <summary>Field chuỗi public static của Lang tên <paramref name="key"/>; null (và cảnh báo một lần) khi không có.</summary>
        static FieldInfo Field(string key)
        {
            if (fieldCache == null) fieldCache = new Dictionary<string, FieldInfo>();
            if (!fieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = typeof(Lang).GetField(key, BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.FieldType != typeof(string) || field.IsLiteral)
                {
                    Debug.LogWarning("Lang: không có chuỗi '" + key + "'");
                    field = null;
                }
                fieldCache[key] = field;
            }
            return field;
        }

        /// <summary>
        /// Chữ của chuỗi song ngữ "Tiếng Việt|English" ở ngôn ngữ hiện tại: có bản dịch trong <see cref="LangTable"/> (Excel)
        /// thì lấy bản dịch, không thì tách chuỗi lấy phần của ngôn ngữ đó (ngôn ngữ thêm trong Excel chưa dịch thì lấy tiếng Anh).
        /// </summary>
        public static string GetSplitText(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            if (table != null)
            {
                if (table.TryText(name, curLange, out string translated)) return translated;
                if (curLange > T_ENG && table.TryText(name, T_ENG, out translated)) return translated;
            }
            return SplitPart(name);
        }

        static string SplitPart(string name)
        {
            string[] parts = name.Split('|');
            int i = curLange < 0 ? 0 : curLange < parts.Length ? curLange : Math.Min(T_ENG, parts.Length - 1);
            return parts[i];
        }

        /// <summary>
        /// <see cref="GetSplitText"/> rồi string.Format, cho chữ có số / tên chèn vào, vd
        /// <c>Lang.Format("Bỏ {0}?|Discard {0}?", label)</c>. Viết mẫu cố định thay vì ghép chuỗi để Excel có đúng một dòng cho nó.
        /// Bản dịch ghi sai ngoặc {} thì dùng chữ gốc trong code.
        /// </summary>
        public static string Format(string name, params object[] args)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            try
            {
                return string.Format(GetSplitText(name), args);
            }
            catch (FormatException)
            {
                Debug.LogWarning("Lang: bản dịch của '" + name + "' sai định dạng {0}, dùng chữ gốc.");
                return string.Format(SplitPart(name), args);
            }
        }

    }
}
