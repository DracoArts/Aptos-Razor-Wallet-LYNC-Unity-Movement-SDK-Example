using UnityEngine;
using TMPro;
using LYNC;
using UnityEngine.UI;
using System.Collections.Generic;

public class MovementExample : MonoBehaviour
{
    [Header("General settings")]
    public Button login;
    public Button logout, mint, view;
    public GameObject LoadingScreen;

    [Space]
    [Header("Firebase")]
    public Transform MovementContainer;
    public TMP_Text WalletAddressText, loginDateTxt, balance;
    public Button WalletAddressButton;

    [Space]
    [Header("Transactions")]
    public Transform transactionResultsParent;
    public GameObject transactionResultHolder;
    public GameObject ViewResultHolder;
    public Transaction mintTxn;
    public ViewTransaction viewTransaction;

    public static MovementExample Instance;

    private void OnEnable()
    {
        LyncManager.onLyncReady += LyncReady;
    }

    private void Awake()
    {
        Instance = this;
        LoadingScreen.SetActive(true);
        login.interactable = false;
        logout.interactable = false;
        mint.interactable = false;
        view.interactable = false;
        Application.targetFrameRate = 30;
    }

    private async void LyncReady(LyncManager Lync)
    {
        AuthBase authBase;
        try
        {
            authBase = await AuthBase.LoadSavedAuth();
            // Debug.Log(authBase.WalletConnected);
            if (authBase.WalletConnected)
            {
                OnWalletConnected(authBase);
            }
            else
            {
                login.interactable = true;
            }
            LoadingScreen.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            logout.interactable = true;
        }

        login.onClick.AddListener(() =>
        {
            Lync.WalletAuth.ConnectWallet((wallet) =>
            {
                OnWalletConnected(wallet);
            });
        });

        logout.onClick.AddListener(() =>
        {
            Lync.WalletAuth.Logout();
            login.interactable = true;
            logout.interactable = false;
            mint.interactable = false;
            foreach (Transform child in transactionResultsParent.transform)
            {
                // Debug.Log(child.name);
                Destroy(child.gameObject);
            }
            Populate();
        });

        WalletAddressButton.onClick.AddListener(() =>{
            string _address = PlayerPrefs.GetString("_publicAddress");
            if(string.IsNullOrEmpty(_address))
                return;
            if(LyncManager.Instance.Network == NETWORK.BARDOCK_TESTNET){
                Application.OpenURL("https://explorer.movementlabs.xyz/account/" + _address + "?network=bardock+testnet");
            }
            if(LyncManager.Instance.Network == NETWORK.MAINNET){
                Application.OpenURL("https://explorer.movementlabs.xyz/account/" + _address + "?network=mainnet");
            }
        });

        mint.onClick.AddListener(async () =>
        {
            if(AuthBase.AuthType == AUTH_TYPE.FIREBASE){
                LoadingScreen.SetActive(true);
                mint.interactable = false;
            }
            TransactionResult txData = await LyncManager.Instance.TransactionsManager.SendTransaction(
                mintTxn
            );
            if (txData.success){
                // Debug.Log(txData.hash);
                SuccessfulTransaction(txData.hash, "MINT");
            }
            else
                ErrorTransaction(txData.error);

            mint.interactable = true;
            LoadingScreen.SetActive(false);
        });

        view.onClick.AddListener(async () =>{
            LoadingScreen.SetActive(true);
            LyncManager.Instance.StartCoroutine(
                API.CoroutineViewTransaction(
                    viewTransaction,
                    tsxData => {
                        SuccessfulViewTransaction(tsxData.Substring(57));
                        LoadingScreen.SetActive(false);
                    },
                    errorData => {
                        LoadingScreen.SetActive(false);
                        SuccessfulViewTransaction("Error  ");
                    }
                )
            );
        });

    }

    private void OnWalletConnected(AuthBase _authBase)
    {
        EnableAppropriateComponents(AuthBase.AuthType);

        if (AuthBase.AuthType == AUTH_TYPE.FIREBASE)
        {
            Populate(_authBase as FirebaseAuth);
        }

        if (AuthBase.AuthType == AUTH_TYPE.Wallet)
        {
            WalletAddressText.text = AbbreviateWalletAddressHex(_authBase.accountAddress);
        }
        StartCoroutine(API.CoroutineGetBalance(_authBase.accountAddress, res =>
        {
            balance.text = res.ToString();
        }, err =>
        {
            Debug.Log("Error");
        }));

        login.interactable = false;
        logout.interactable = true;
        mint.interactable = true;
        view.interactable = true;
    }

    private void EnableAppropriateComponents(AUTH_TYPE authType)
    {
        if (authType == AUTH_TYPE.FIREBASE)
        {
            MovementContainer.gameObject.SetActive(true);
            // StarKeyContainer.gameObject.SetActive(false);
        }
        if (authType == AUTH_TYPE.Wallet)
        {
            // StarKeyContainer.gameObject.SetActive(true);
            MovementContainer.gameObject.SetActive(false);
        }
    }

    private void SuccessfulTransaction(string hash, string txnTitle = "")
    {
        var go = Instantiate(transactionResultHolder, transactionResultsParent);

        if (!string.IsNullOrEmpty(hash))
        {
            go.transform.GetComponentInChildren<TMP_Text>().text = (txnTitle != "" ? ("(" + txnTitle + ")") : "") + " Success, hash = " + hash.Substring(0, 5) + "..." + hash.Substring(hash.Length - 5) + "<color=\"green\"> Check on Movement EXPLORER<color=\"green\">";
            Button button = go.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                Debug.Log("Opening explorer...");
                if(LyncManager.Instance.Network == NETWORK.BARDOCK_TESTNET){
                    Application.OpenURL($"https://explorer.movementlabs.xyz/txn/{hash}?network=bardock+testnet");
                }
                if(LyncManager.Instance.Network == NETWORK.MAINNET){
                    Application.OpenURL($"https://explorer.movementlabs.xyz/txn/{hash}?network=mainnet");
                }
            });
        }
        else
        {
            // Pontem mobile transactions doesnt contain a hash
            go.transform.GetComponentInChildren<TMP_Text>().text = (txnTitle != "" ? ("(" + txnTitle + ")") : "") + " Successfull transaction";
        }
    }

    private void SuccessfulViewTransaction(string result){
        var Holder = Instantiate(ViewResultHolder, transactionResultsParent);
        Holder.transform.GetComponentInChildren<TMP_Text>().text = $"ViewResults: {result.Substring(0, result.Length - 2)}";
    }

    private void ErrorTransaction(string error, string txnTitle = "")
    {
        var go = Instantiate(transactionResultHolder, transactionResultsParent);
        go.transform.GetComponentInChildren<TMP_Text>().text = txnTitle + " <color=\"red\">TXN ERROR:</color=\"red\"> " + error;
    }

    public void Populate(FirebaseAuth firebaseAuth = null)
    {
        WalletAddressText.text = (firebaseAuth == null ? "Disconnected" : AbbreviateWalletAddressHex(firebaseAuth.supraFirebaseAuthDetails.accountAddress));
        loginDateTxt.text = "Login Date = " + (firebaseAuth == null ? "" : firebaseAuth.LoginDate.ToString());
        balance.text = (firebaseAuth == null ? "0" : firebaseAuth.supraFirebaseAuthDetails.balance) + " APT";
    }

    public string AbbreviateWalletAddressHex(string hexString, int prefixLength = 4, int suffixLength = 3)
    {
        if (hexString.Length <= prefixLength + suffixLength)
        {
            return hexString; // No need for abbreviation
        }

        string prefix = hexString.Substring(0, prefixLength);
        string suffix = hexString.Substring(hexString.Length - suffixLength);

        return prefix + "..." + suffix;
    }
}
