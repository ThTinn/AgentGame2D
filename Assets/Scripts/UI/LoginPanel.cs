using System;
using Game.UI;
using TMPro;
using UnityEngine;

namespace DungeonOnline.UI
{
    /// <summary>
    /// Second screen: name and password, then Login. Display only: it raises <see cref="LoginPressed"/> and shows the
    /// server's answer with <see cref="SetMessage"/>. Remembers the last name like GameB's LoginScene (RMS_USER).
    /// </summary>
    /// <example>
    /// <code>
    /// var login = LoginPanel.Create(canvas.transform);
    /// login.LoginPressed += (name, pass) => session.Login(name, pass);
    /// session.Message += login.SetMessage;
    /// </code>
    /// </example>
    public sealed class LoginPanel : MonoBehaviour
    {
        private const string PREF_USER = "login.user";

        private TMP_InputField _name;
        private TMP_InputField _password;
        private TextBtn _login;
        private TextMeshProUGUI _message;

        /// <summary>Login was pressed with a non-empty name and password.</summary>
        public event Action<string, string> LoginPressed;

        /// <summary>Builds the panel, hidden, under <paramref name="parent"/> (usually the GameCanvas).</summary>
        public static LoginPanel Create(Transform parent)
        {
            RectTransform root = PanelBuilder.FullScreen(parent, "LoginPanel", PanelBuilder.Backdrop);
            var panel = root.gameObject.AddComponent<LoginPanel>();
            panel.Build(root);
            root.gameObject.SetActive(false);
            return panel;
        }

        private void Build(RectTransform root)
        {
            RectTransform box = PanelBuilder.Box(root, new Vector2(560f, 440f));
            PanelBuilder.Label(box, Lang.GetSplitText("Đăng nhập|Log in"), 40f, 56f);
            _name = PanelBuilder.Input(box, Lang.GetSplitText("Tên nhân vật (3-24 ký tự)|Name (3-24 characters)"), false);
            _password = PanelBuilder.Input(box, Lang.GetSplitText("Mật khẩu|Password"), true);
            _login = PanelBuilder.Button(box, Lang.GetSplitText("Đăng nhập|Log in"), 64f, PressLogin);
            _message = PanelBuilder.Label(box, Lang.GetSplitText("Tên mới sẽ tự tạo tài khoản.|A new name creates an account."),
                22f, 60f);
            try { _name.text = PlayerPrefs.GetString(PREF_USER, ""); }
            catch (Exception) { }   // a missing preference is never fatal
            _password.onSubmit.AddListener(_ => PressLogin());
        }

        private void PressLogin()
        {
            string name = _name.text.Trim(), password = _password.text;
            if (name.Length == 0 || password.Length == 0)
            {
                SetMessage(Lang.GetSplitText("Nhập tên và mật khẩu.|Enter a name and a password."));
                return;
            }
            try { PlayerPrefs.SetString(PREF_USER, name); }
            catch (Exception) { }
            SetBusy(true);
            SetMessage(Lang.GetSplitText("Đang đăng nhập...|Logging in..."));
            LoginPressed?.Invoke(name, password);
        }

        /// <summary>Shows a line from the server (welcome, wrong password, busy) or a hint.</summary>
        public void SetMessage(string text)
        {
            if (_message != null) _message.text = text;
        }

        /// <summary>Locks the Login button while waiting for the server.</summary>
        public void SetBusy(bool busy) => _login.SetInteractable(!busy);

        /// <summary>Shows or hides the whole panel.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
