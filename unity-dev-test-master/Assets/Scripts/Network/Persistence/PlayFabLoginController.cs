using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using PlayFab.DataModels;
using PlayFab.ProfilesModels;
using System.Collections.Generic;
using PlayFab.Json;
using Patterns.Creational.Singleton;

public class PlayFabLoginController : Singleton<PlayFabLoginController>
{

    //Mock results
    private string userEmail;
    private string userPassword;
    private string userName;
    public GameObject loginPanel;
    public GameObject addLoginPanel;
    public GameObject recoverButton;
    public GameObject blackCanvas;

    // Use this for initialization
    void Start () {
        //Note: Setting title Id here can be skipped if you have set the value in Editor Extensions already.
        if (string.IsNullOrEmpty(PlayFabSettings.TitleId))
        {
            PlayFabSettings.TitleId = "F02D2"; // Please change this value to your own titleId from PlayFab Game Manager
        }

        //var request = new LoginWithCustomIDRequest { CustomId = "GettingStartedGuide", CreateAccount = true };
        //PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);

        //If the email key already exists we get the user email and password and save in variables.
        if (PlayerPrefs.HasKey("EMAIL"))
        {
            userEmail = PlayerPrefs.GetString("EMAIL");
            userPassword = PlayerPrefs.GetString("PASSWORD");
            var request = new LoginWithEmailAddressRequest { Email = userEmail, Password = userPassword };
            PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnLoginFailure);
        }
        else //for anonymous login
        {
#if UNITY_ANDROID
            var requestAndroid = new LoginWithAndroidDeviceIDRequest { AndroidDeviceId = ReturnMobileId(), CreateAccount = true };
            PlayFabClientAPI.LoginWithAndroidDeviceID(requestAndroid, OnLoginMobileSuccess, OnLoginMobileFailure);
#endif

#if UNITY_IOS
            var requestIOS = new LoginWithIOSDeviceIDRequest { DeviceId = ReturnMobileId(), CreateAccount = true };
            PlayFabClientAPI.LoginWithIOSDeviceID(requestIOS, OnLoginMobileSuccess, OnLoginMobileFailure); 
#endif

        }
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("Congratulations, you made your first successful API call!");
        PlayerPrefs.SetString("EMAIL", userEmail);
        PlayerPrefs.SetString("PASSWORD", userPassword);
        loginPanel.SetActive(false);    //Make it solid
        blackCanvas.SetActive(false);
        //recoverButton.SetActive(true);
        GetStatistics();    //Invert dependence
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        Debug.Log("Registration successful!");
        PlayerPrefs.SetString("EMAIL", userEmail);
        PlayerPrefs.SetString("PASSWORD", userPassword);
        GetStatistics();
        loginPanel.SetActive(false);
        blackCanvas.SetActive(false);
    }
    
    private void OnLoginFailure(PlayFabError error)
    {
        var registerRequest = new RegisterPlayFabUserRequest { Email = userEmail, Password = userPassword, Username = userName };
        PlayFabClientAPI.RegisterPlayFabUser(registerRequest, OnRegisterSuccess, OnRegisterFailure);
    }
    
    private void OnRegisterFailure(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }

    public void GetUserEmail(string emailIn)
    {
        userEmail = emailIn;
    }

    public void GetUserName(string userNameIn)
    {
        userName = userNameIn;
    }

    public void GetUserPassword(string passwordIn)
    {
        userPassword = passwordIn;
    }

    public void OnClickLogin()
    {
        var request = new LoginWithEmailAddressRequest { Email = userEmail, Password = userPassword };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnLoginFailure);
    }
    
    public void OpenAddLogin()
    {
        addLoginPanel.SetActive(true);
        blackCanvas.SetActive(true);
    }

    public void OnClickAddLogin()
    {
        var addLoginRequest = new AddUsernamePasswordRequest { Email = userEmail, Password = userPassword, Username = userName };
        PlayFabClientAPI.AddUsernamePassword(addLoginRequest, OnAddLoginSuccess, OnRegisterFailure);
    }

    private void OnAddLoginSuccess(AddUsernamePasswordResult result)
    {
        Debug.Log("Login successful!");
        PlayerPrefs.SetString("EMAIL", userEmail);
        PlayerPrefs.SetString("PASSWORD", userPassword);
        addLoginPanel.SetActive(false);
    }

    #region PlayerStats
    public double money;
    public int shafts;

    public void SetPlayerStats()
    {
        PlayFabClientAPI.UpdatePlayerStatistics(new UpdatePlayerStatisticsRequest
        {
            // request.Statistics is a list, so multiple StatisticUpdate objects can be defined if required.
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate {StatisticName = "Money", Value = (int)FindObjectOfType<FinanceManager>().TotalMoney },
                new StatisticUpdate {StatisticName = "Shafts", Value = shafts }
            }
        },
        result => { Debug.Log("User statistics updated"); },
        error => { Debug.LogError(error.GenerateErrorReport()); });
    }

    void GetStatistics()
    {
        PlayFabClientAPI.GetPlayerStatistics(
            new GetPlayerStatisticsRequest(),
            OnGetStatistics,
            error => Debug.LogError(error.GenerateErrorReport())
        );
    }

    void OnGetStatistics(GetPlayerStatisticsResult result)
    {
        Debug.Log("Received the following Statistics:");
        foreach (var eachStat in result.Statistics)
        {
            Debug.Log("Statistic (" + eachStat.StatisticName + "): " + eachStat.Value);
            switch (eachStat.StatisticName)
            {
                case "Money":
                    money = eachStat.Value;
                    break;
                case "Shafts":
                    shafts = eachStat.Value;
                    break;
            }
        }
    }

    private static void StartCloudUpdatePlayerStats()
    {
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest()
        {
            FunctionName = "UpdatePlayerStats", // Arbitrary function name (must exist in your uploaded cloud.js file)
            FunctionParameter = new { Money = FindObjectOfType<FinanceManager>().TotalMoney, Shafts = 1 }, // The parameter provided to your function
            GeneratePlayStreamEvent = true, // Optional - Shows this event in PlayStream
        }, OnCloudUpdateStats, OnErrorShared);
    }

    private static void OnCloudUpdateStats(ExecuteCloudScriptResult result)
    {
        // Cloud Script returns arbitrary results, so you have to evaluate them one step and one parameter at a time
        PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer).SerializeObject(result.FunctionResult);
        
        JsonObject jsonResult = (JsonObject)result.FunctionResult;
        object messageValue;
        jsonResult.TryGetValue("messageValue", out messageValue); // note how "messageValue" directly corresponds to the JSON values set in Cloud Script
        Debug.Log((string)messageValue);
    }

    private static void OnErrorShared(PlayFabError error)
    {
        Debug.Log(error.GenerateErrorReport());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            SetPlayerStats();
        if (Input.GetKeyDown(KeyCode.Delete))
            PlayerPrefs.DeleteAll();
        if (Input.GetKeyDown(KeyCode.F1))
            StartCloudUpdatePlayerStats();
    }

    #endregion

}
