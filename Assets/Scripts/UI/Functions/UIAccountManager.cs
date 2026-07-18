using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIAccountManager : MonoBehaviour
    {
        [Header("UI References")]
        public InputField usernameField;
        public InputField passwordField;
        public Button loginButton;
        public Button registerButton;
        public Text statusText;
        
        [Header("Account Data")]
        public string username = "";
        public string password = "";

        private void Start()
        {
            InitializeAccountManager();
        }

        public void InitializeAccountManager()
        {
            // Add listeners
            loginButton.onClick.AddListener(AttemptLogin);
            registerButton.onClick.AddListener(AttemptRegister);
        }

        public void AttemptLogin()
        {
            username = usernameField.text;
            password = passwordField.text;
            
            // Validate credentials
            if (ValidateCredentials(username, password))
            {
                statusText.text = "Login successful";
                // Navigate to main menu
            }
            else
            {
                statusText.text = "Invalid credentials";
            }
        }

        public void AttemptRegister()
        {
            username = usernameField.text;
            password = passwordField.text;
            
            // Register new account
            if (RegisterAccount(username, password))
            {
                statusText.text = "Registration successful";
                // Navigate to main menu
            }
            else
            {
                statusText.text = "Registration failed";
            }
        }

        private bool ValidateCredentials(string user, string pass)
        {
            // Placeholder for actual validation logic
            return user == "testuser" && pass == "testpass";
        }

        private bool RegisterAccount(string user, string pass)
        {
            // Placeholder for actual registration logic
            return true;
        }
    }
}